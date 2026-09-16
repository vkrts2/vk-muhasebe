using System;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using System.IO;
using System.Text.Json;
using ErmayMuhasebe.Models;
using System.Collections.Generic;

namespace ErmayMuhasebe.Services
{
    public class CloudConfig
    {
        public string BaseUrl { get; set; } = "";
        public string AuthSecret { get; set; } = "";
        public string GoogleApiKey { get; set; } = "";
        public string GoogleClientId { get; set; } = "";
        public string GoogleClientSecret { get; set; } = "";
        public bool IsActive { get; set; } = false;
        public bool IsAutoSyncEnabled { get; set; } = true;
    }

    public class CloudSyncService
    {
        private FirebaseClient? _firebase;
        private CloudConfig _config = new();
        private readonly string _configPath;
        private readonly IYearContext _yearContext;

        public FirebaseClient? Client => _firebase;
        public CloudConfig Config => _config;
        public bool IsConnected => _firebase != null && _config.IsActive;
        public string BaseUrl => _config.BaseUrl;
        public string AuthSecret => _config.AuthSecret;
        public bool IsAutoSyncEnabled => _config.IsAutoSyncEnabled;

        public void Disconnect()
        {
            _config.IsActive = false;
            _config.BaseUrl = "";
            _firebase = null;
        }

        public void EnableAutoSync(bool enable)
        {
            _config.IsAutoSyncEnabled = enable;
            try
            {
                var json = JsonSerializer.Serialize(_config);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving auto-sync config: {ex.Message}");
            }
        }

        public CloudSyncService(IYearContext yearContext)
        {
            _yearContext = yearContext;
            _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ermay_cloud_config.json");
            LoadConfig();
        }

        public string GetYearlyPath(string resourceName)
        {
            return $"companies/default/years/{_yearContext.CurrentYear}/{resourceName}";
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    _config = JsonSerializer.Deserialize<CloudConfig>(json) ?? new CloudConfig();
                    InitializeFirebase();
                }
                else
                {
                    _config = new CloudConfig();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cloud Config Load Error: {ex.Message}");
                _config = new CloudConfig();
            }
        }

        public void SaveConfig(string url, string secret)
        {
            _config.BaseUrl = url;
            _config.AuthSecret = secret;
            _config.IsActive = !string.IsNullOrEmpty(url);
            
            var json = JsonSerializer.Serialize(_config);
            File.WriteAllText(_configPath, json);
            
            InitializeFirebase();
        }

        private string? _cachedIdToken;
        private DateTime _tokenExpiresAt = DateTime.MinValue;
        private static readonly System.Net.Http.HttpClient _authHttpClient = new System.Net.Http.HttpClient();

        private async Task<string> GetFirebaseAuthTokenAsync()
        {
            if (!string.IsNullOrEmpty(_cachedIdToken) && DateTime.UtcNow < _tokenExpiresAt)
            {
                return _cachedIdToken;
            }

            if (!string.IsNullOrWhiteSpace(_config.GoogleApiKey))
            {
                try
                {
                    var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_config.GoogleApiKey}";
                    var content = new System.Net.Http.StringContent("{\"returnSecureToken\":true}", System.Text.Encoding.UTF8, "application/json");
                    var res = await _authHttpClient.PostAsync(url, content);
                    if (res.IsSuccessStatusCode)
                    {
                        var respJson = await res.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(respJson);
                        if (doc.RootElement.TryGetProperty("idToken", out var tokenProp))
                        {
                            _cachedIdToken = tokenProp.GetString();
                            int expiresIn = 3600;
                            if (doc.RootElement.TryGetProperty("expiresIn", out var expProp) && int.TryParse(expProp.GetString(), out var exp))
                            {
                                expiresIn = exp;
                            }
                            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 300);
                            return _cachedIdToken ?? "";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Firebase Auth REST Token Error: {ex.Message}");
                }
            }

            // Fallback: Eski secret (varsa)
            return _config.AuthSecret ?? "";
        }

        private void InitializeFirebase()
        {
            if (string.IsNullOrEmpty(_config.BaseUrl)) 
            {
                _firebase = null;
                return;
            }

            try
            {
                var options = new FirebaseOptions();
                if (!string.IsNullOrEmpty(_config.GoogleApiKey) || !string.IsNullOrEmpty(_config.AuthSecret))
                {
                    options.AuthTokenAsyncFactory = () => GetFirebaseAuthTokenAsync();
                }

                _firebase = new FirebaseClient(_config.BaseUrl, options);
                StartRealtimeStreamListener();
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Firebase Init Error: {ex.Message}");
            }
        }

        // --- SYNC METHODS ---
        
        // Helper to ignore errors and not block the main thread
        private async Task SafeRun(Func<Task> action)
        {
            if (!IsConnected) return;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                // Log but don't crash
                Console.WriteLine($"Cloud Sync Error: {ex.Message}");
            }
        }

        public async Task ClearCloudTablesAsync(string tenantId = "default")
        {
            await SafeRun(async () =>
            {
                if (_firebase != null)
                {
                    int year = _yearContext?.CurrentYear ?? DateTime.Now.Year;
                    await _firebase.Child("companies").Child(tenantId).Child("years").Child(year.ToString()).DeleteAsync();
                }
            });
        }

        public async Task SyncCariAsync(CariKart cari)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Cariler")).Child(cari.Id.ToString()).PutAsync(cari);
            });
            
            // Temel bilgileri diğer yıllara da yansıt (Mali bakiyeler hariç)
            await SyncCariToAllYearsAsync(cari);
        }

        public async Task UpdateFutureBalancesAsync(string entityType, int entityId, decimal borcDelta, decimal alacakDelta)
        {
            await SafeRun(async () =>
            {
                try 
                {
                    var yearsNode = await _firebase!.Child("companies/default/years").OnceAsync<object>();
                    var currentYearInt = _yearContext.CurrentYear;

                    var sortedYears = yearsNode
                        .Select(y => int.TryParse(y.Key, out int val) ? val : 0)
                        .Where(y => y > currentYearInt)
                        .OrderBy(y => y)
                        .ToList();

                    foreach (var year in sortedYears)
                    {
                        var path = $"companies/default/years/{year}/{entityType}/{entityId}";
                        var entityNode = await _firebase!.Child(path).OnceSingleAsync<Newtonsoft.Json.Linq.JObject>();
                        
                        if (entityNode != null)
                        {
                            decimal currentDevirBorc = (decimal?)entityNode["DevirBorc"] ?? 0;
                            decimal currentDevirAlacak = (decimal?)entityNode["DevirAlacak"] ?? 0;
                            decimal currentAcilis = (decimal?)entityNode["AcilisBakiyesi"] ?? 0;
                            
                            var patchData = new Dictionary<string, object>();

                            if (entityType == "Cariler")
                            {
                                patchData.Add("DevirBorc", currentDevirBorc + borcDelta);
                                patchData.Add("DevirAlacak", currentDevirAlacak + alacakDelta);
                            }
                            else if (entityType == "Bankalar" || entityType == "Kasalar" || entityType == "Portfoy")
                            {
                                // Kasa ve Banka için tek bakiye alanı (AcilisBakiyesi) kullanılıyor
                                // borcDelta = giren, alacakDelta = cikan
                                patchData.Add("AcilisBakiyesi", currentAcilis + (borcDelta - alacakDelta));
                            }

                            await _firebase!.Child(path).PatchAsync(patchData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Future balance sync error: {ex.Message}");
                }
            });
        }

        public async Task SyncCariToAllYearsAsync(CariKart cari)
        {
            await SafeRun(async () => 
            {
                try
                {
                    var yearsNode = await _firebase!.Child("companies/default/years").OnceAsync<object>();
                    var currentYear = _yearContext.CurrentYear.ToString();

                    var patchData = new Dictionary<string, object>
                    {
                        { "Unvan", cari.Unvan ?? "" },
                        { "CariKod", cari.CariKod ?? "" },
                        { "Yetkili", cari.Yetkili ?? "" },
                        { "Grup", cari.Grup ?? "" },
                        { "Tur", cari.Tur ?? "Alici" },
                        { "Telefon", cari.Telefon ?? "" },
                        { "CepTelefon", cari.CepTelefon ?? "" },
                        { "Eposta", cari.Email ?? "" },
                        { "WebAdresi", cari.WebAdresi ?? "" },
                        { "VergiDairesi", cari.VergiDairesi ?? "" },
                        { "VergiNo", cari.VergiNo ?? "" },
                        { "Iban", cari.IBAN ?? "" },
                        { "Adres", cari.Adres ?? "" },
                        { "Il", cari.Il ?? "" },
                        { "Ilce", cari.Ilce ?? "" },
                        { "Ulke", cari.Ulke ?? "" }
                    };

                    foreach (var year in yearsNode)
                    {
                        if (year.Key != currentYear)
                        {
                            var cariNode = await _firebase!.Child($"companies/default/years/{year.Key}/Cariler").Child(cari.Id.ToString()).OnceSingleAsync<object>();
                            if (cariNode != null)
                            {
                                await _firebase!.Child($"companies/default/years/{year.Key}/Cariler").Child(cari.Id.ToString()).PatchAsync(patchData);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Cross-year sync error: {ex.Message}");
                }
            });
        }
        
        public async Task DeleteCariAsync(int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Cariler")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncCariHareketAsync(CariHareket hareket)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("CariHareketler")).Child(hareket.Id.ToString()).PutAsync(hareket);
            });
        }

        public async Task DeleteCariHareketAsync(int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("CariHareketler")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncNoteAsync(Note note)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Notes")).Child(note.Id.ToString()).PutAsync(note);
            });
        }

        public async Task SyncFaturaTasarimiAsync(FaturaTasarimi tasarim)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("FaturaTasarimi")).Child("1").PutAsync(tasarim);
            });
        }

        // Firma profili (logo dahil) kök düğümde ve şirket ayarlarında tutulur — mobil Ayarlar ekranı
        // ve PDF servisleri farklı Firebase kurallarında dahi logoya kesinlikle erişebilsin diye çoklu yola yazılır.
        // Firma profili (logo dahil) kök düğümde ve şirket ayarlarında tutulur — mobil Ayarlar ekranı
        // ve PDF servisleri farklı Firebase kurallarında dahi logoya kesinlikle erişebilsin diye çoklu yola yazılır.
        public async Task SyncFirmaProfiliAsync(FirmaProfili profil)
        {
            if (_firebase == null)
            {
                LoadConfig();
            }

            await SafeRun(async () => 
            {
                if (_firebase == null) return;

                int year = _yearContext?.CurrentYear ?? DateTime.Now.Year;
                bool hasLogo = !string.IsNullOrEmpty(profil.LogoBase64);

                // 1. Kök düğüme yaz (Mobil doğrudan buradan okur)
                try
                {
                    await _firebase.Child("FirmaProfili").Child("1").PutAsync(profil);
                    if (hasLogo)
                    {
                        await _firebase.Child("FirmaProfili").Child("1").Child("logoBase64").PutAsync(profil.LogoBase64);
                    }
                    else
                    {
                        try { await _firebase.Child("FirmaProfili").Child("1").Child("LogoBase64").DeleteAsync(); } catch { }
                        try { await _firebase.Child("FirmaProfili").Child("1").Child("logoBase64").DeleteAsync(); } catch { }
                    }
                    Console.WriteLine("[CloudSync] FirmaProfili/1 basariyla Firebase'e yazildi.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CloudSync] FirmaProfili/1 yazma hatasi: {ex.Message}");
                }

                // 2. companies/default/FirmaProfili/1 yoluna yaz (Tenant yapısı için)
                try
                {
                    await _firebase.Child("companies").Child("default").Child("FirmaProfili").Child("1").PutAsync(profil);
                    if (hasLogo)
                    {
                        await _firebase.Child("companies").Child("default").Child("FirmaProfili").Child("1").Child("logoBase64").PutAsync(profil.LogoBase64);
                    }
                    else
                    {
                        try { await _firebase.Child("companies").Child("default").Child("FirmaProfili").Child("1").Child("LogoBase64").DeleteAsync(); } catch { }
                        try { await _firebase.Child("companies").Child("default").Child("FirmaProfili").Child("1").Child("logoBase64").DeleteAsync(); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CloudSync] companies/default/FirmaProfili/1 yazma hatasi: {ex.Message}");
                }

                // 3. Yillik yola yaz (companies/default/years/{year}/FirmaProfili/1)
                try
                {
                    await _firebase.Child("companies").Child("default").Child("years").Child(year.ToString()).Child("FirmaProfili").Child("1").PutAsync(profil);
                    if (hasLogo)
                    {
                        await _firebase.Child("companies").Child("default").Child("years").Child(year.ToString()).Child("FirmaProfili").Child("1").Child("logoBase64").PutAsync(profil.LogoBase64);
                    }
                    else
                    {
                        try { await _firebase.Child("companies").Child("default").Child("years").Child(year.ToString()).Child("FirmaProfili").Child("1").Child("LogoBase64").DeleteAsync(); } catch { }
                        try { await _firebase.Child("companies").Child("default").Child("years").Child(year.ToString()).Child("FirmaProfili").Child("1").Child("logoBase64").DeleteAsync(); } catch { }
                    }
                }
                catch { }

                // 4. Bulut/Web ayar yoluna yaz (companies/default/settings/company_logo)
                try
                {
                    if (hasLogo)
                    {
                        await _firebase.Child("companies").Child("default").Child("settings").Child("company_logo").PutAsync(profil.LogoBase64);
                    }
                    else
                    {
                        await _firebase.Child("companies").Child("default").Child("settings").Child("company_logo").DeleteAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CloudSync] company_logo ayar yazma hatasi: {ex.Message}");
                }
            });
        }

        public async Task<FirmaProfili?> PullFirmaProfiliAsync()
        {
            if (!IsConnected || _firebase == null) return null;
            try
            {
                var item = await _firebase.Child("FirmaProfili").Child("1").OnceSingleAsync<FirmaProfili>();
                if (item != null) return item;
                
                return await _firebase.Child("companies").Child("default").Child("FirmaProfili").Child("1").OnceSingleAsync<FirmaProfili>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CloudSync] PullFirmaProfiliAsync Error: {ex.Message}");
                return null;
            }
        }

        public async Task DeleteNoteAsync(int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Notes")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncStokAsync(StokKart stok)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Stoklar")).Child(stok.Id.ToString()).PutAsync(stok);
            });
        }

        public async Task DeleteStokAsync(int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Stoklar")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncStokGrupAsync(StokGrupDef grup)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("StokGruplar")).Child(grup.Id.ToString()).PutAsync(grup);
            });
        }

        public async Task DeleteStokGrupAsync(int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("StokGruplar")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncStokHareketAsync(StokHareket hareket)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("StokHareketler")).Child(hareket.Id.ToString()).PutAsync(hareket);
            });
        }

        public async Task DeleteStokHareketAsync(int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("StokHareketler")).Child(id.ToString()).DeleteAsync();
            });
        }
        
        public async Task DeleteStokHareketByFaturaIdAsync(int faturaId, string? evrakNo = null)
        {
            await SafeRun(async () =>
            {
                var items = await _firebase!.Child(GetYearlyPath("StokHareketler")).OnceAsync<StokHareket>();
                string cleanNo = evrakNo?.Trim() ?? "";

                foreach(var item in items)
                {
                     if (item != null && item.Object != null)
                     {
                         bool match = false;
                         if (faturaId > 0 && item.Object.FaturaId == faturaId) match = true;
                         else if (!string.IsNullOrEmpty(cleanNo) && !string.IsNullOrEmpty(item.Object.EvrakNo) &&
                                  string.Equals(item.Object.EvrakNo.Trim(), cleanNo, StringComparison.OrdinalIgnoreCase)) match = true;

                         if (match && !string.IsNullOrEmpty(item.Key))
                             await _firebase!.Child(GetYearlyPath("StokHareketler")).Child(item.Key).DeleteAsync();
                     }
                }
            });
        }

        public async Task DeleteCariHareketByFaturaIdAsync(int faturaId, string? evrakNo = null)
        {
            await SafeRun(async () =>
            {
                var items = await _firebase!.Child(GetYearlyPath("CariHareketler")).OnceAsync<CariHareket>();
                string cleanNo = evrakNo?.Trim() ?? "";
                string kplNo = !string.IsNullOrEmpty(cleanNo) ? "KPL-" + cleanNo : "";

                foreach(var item in items)
                {
                     if (item != null && item.Object != null)
                     {
                         bool match = false;
                         if (faturaId > 0 && item.Object.FaturaId == faturaId) match = true;
                         else if (!string.IsNullOrEmpty(cleanNo) && !string.IsNullOrEmpty(item.Object.EvrakNo))
                         {
                             string itemEvrak = item.Object.EvrakNo.Trim();
                             if (string.Equals(itemEvrak, cleanNo, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(itemEvrak, kplNo, StringComparison.OrdinalIgnoreCase))
                             {
                                 match = true;
                             }
                         }

                         if (match && !string.IsNullOrEmpty(item.Key))
                             await _firebase!.Child(GetYearlyPath("CariHareketler")).Child(item.Key).DeleteAsync();
                     }
                }
            });
        }

        public async Task SyncFaturaAsync(Fatura fatura)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Faturalar")).Child(fatura.Id.ToString()).PutAsync(fatura);
            });
        }

        public async Task DeleteFaturaAsync(int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Faturalar")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncFaturaDetaylarAsync(int faturaId, List<FaturaDetay> detaylar)
        {
            await SafeRun(async () => 
            {
                // Push the list directly under FaturaDetaylar/FaturaId
                await _firebase!.Child(GetYearlyPath("FaturaDetaylar")).Child(faturaId.ToString()).PutAsync(detaylar);
            });
        }

        public async Task DeleteFaturaDetaylarAsync(int faturaId)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("FaturaDetaylar")).Child(faturaId.ToString()).DeleteAsync();
            });
        }
        
        public async Task SyncSiparisAsync(Siparis siparis)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Siparisler")).Child(siparis.Id.ToString()).PutAsync(siparis);
            });
        }

        public async Task DeleteSiparisAsync(int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Siparisler")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncTeklifAsync(Teklif teklif)
        {
             await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Teklifler")).Child(teklif.Id.ToString()).PutAsync(teklif);
            });
        }

        public async Task DeleteTeklifAsync(int id)
        {
             await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Teklifler")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncSiparisDetaylarAsync(int id, List<SiparisDetay> detaylar)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("SiparisDetaylar")).Child(id.ToString()).PutAsync(detaylar);
            });
        }

        public async Task DeleteSiparisDetaylarAsync(int id)
        {
            await SafeRun(async () => 
            {
                 await _firebase!.Child(GetYearlyPath("SiparisDetaylar")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncTeklifDetaylarAsync(int id, List<TeklifDetay> detaylar)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("TeklifDetaylar")).Child(id.ToString()).PutAsync(detaylar);
            });
        }

        public async Task DeleteTeklifDetaylarAsync(int id)
        {
            await SafeRun(async () => 
            {
                 await _firebase!.Child(GetYearlyPath("TeklifDetaylar")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncBankaAsync(BankaKart banka)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Bankalar")).Child(banka.Id.ToString()).PutAsync(banka);
            });
        }

        public async Task DeleteBankaAsync(int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("Bankalar")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncKasaHareketAsync(KasaHareket hareket)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("KasaHareketler")).Child(hareket.Id.ToString()).PutAsync(hareket);
            });
        }

        public async Task DeleteKasaHareketAsync(int id)
        {
             await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("KasaHareketler")).Child(id.ToString()).DeleteAsync();
            });
        }

        public async Task SyncBankaHareketAsync(BankaHareket hareket)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("BankaHareketler")).Child(hareket.Id.ToString()).PutAsync(hareket);
            });
        }

        public async Task DeleteBankaHareketAsync(int id)
        {
             await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("BankaHareketler")).Child(id.ToString()).DeleteAsync();
            });
        }
        
        public async Task SyncCekAsync(Cek cek) => await SyncGenericAsync("Cekler", cek, cek.Id);
        public async Task SyncSenetAsync(Senet senet) => await SyncGenericAsync("Senetler", senet, senet.Id);
        public async Task SyncKrediKartiIslemAsync(KrediKartiIslem islem) => await SyncGenericAsync("KrediKartlari", islem, islem.Id);
        public async Task SyncEftIslemAsync(EftIslem islem) => await SyncGenericAsync("EftIslemleri", islem, islem.Id);
        public async Task SyncStokSayimFisiAsync(StokSayimFisi fis) => await SyncGenericAsync("StokSayimlar", fis, fis.Id);
        public async Task SyncStokSayimDetaylarAsync(int fisId, List<StokSayimDetay> detaylar)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath("StokSayimDetaylar")).Child(fisId.ToString()).PutAsync(detaylar);
            });
        }

        public async Task DeleteCekAsync(int id) => await DeleteGenericAsync("Cekler", id);
        public async Task DeleteSenetAsync(int id) => await DeleteGenericAsync("Senetler", id);
        public async Task DeleteKrediKartiIslemAsync(int id) => await DeleteGenericAsync("KrediKartlari", id);
        public async Task DeleteEftIslemAsync(int id) => await DeleteGenericAsync("EftIslemleri", id);
        public async Task DeleteStokSayimFisiAsync(int id) => await DeleteGenericAsync("StokSayimlar", id);

        public async Task SyncGorevAsync(Gorev gorev) => await SyncGenericAsync("Gorevler", gorev, gorev.Id);
        public async Task DeleteGorevAsync(int id) => await DeleteGenericAsync("Gorevler", id);
        public async Task<List<Gorev>> PullGorevlerAsync() => await GlobalGetAllAsync<Gorev>("Gorevler");

        public async Task SyncPersonelAsync(Personel personel) => await SyncGenericAsync("Personeller", personel, personel.Id);
        public async Task DeletePersonelAsync(int id) => await DeleteGenericAsync("Personeller", id);
        public async Task<List<Personel>> PullPersonellerAsync() => await GlobalGetAllAsync<Personel>("Personeller");

        public async Task SyncSatisHedefiAsync(SatisHedefi hedef) => await SyncGenericAsync("SatisHedefleri", hedef, hedef.Id);
        public async Task<List<SatisHedefi>> PullSatisHedefleriAsync() => await GlobalGetAllAsync<SatisHedefi>("SatisHedefleri");

        public async Task SyncHaftalikSatisHedefiAsync(HaftalikSatisHedefi hedef) => await SyncGenericAsync("HaftalikSatisHedefleri", hedef, hedef.Id);
        public async Task<List<HaftalikSatisHedefi>> PullHaftalikSatisHedefleriAsync() => await GlobalGetAllAsync<HaftalikSatisHedefi>("HaftalikSatisHedefleri");

        public async Task SyncYillikSatisHedefiAsync(YillikSatisHedefi hedef) => await SyncGenericAsync("YillikSatisHedefleri", hedef, hedef.Id);
        public async Task<List<YillikSatisHedefi>> PullYillikSatisHedefleriAsync() => await GlobalGetAllAsync<YillikSatisHedefi>("YillikSatisHedefleri");

        public async Task SyncPortfoyAsync(PortfoyKart portfoy) => await SyncGenericAsync("PortfoyKartlari", portfoy, portfoy.Id);
        public async Task DeletePortfoyAsync(int id) => await DeleteGenericAsync("PortfoyKartlari", id);
        public async Task<List<PortfoyKart>> PullPortfoyAsync() => await GlobalGetAllAsync<PortfoyKart>("PortfoyKartlari");

        public async Task<List<StokSayimFisi>> PullStokSayimlarAsync() => await GlobalGetAllAsync<StokSayimFisi>("StokSayimlar");
        public async Task<List<StokSayimDetay>> PullStokSayimDetaylarAsync(int fisId)
        {
            if (!IsConnected) return new List<StokSayimDetay>();
            try {
                var list = await _firebase!.Child(GetYearlyPath("StokSayimDetaylar")).Child(fisId.ToString()).OnceSingleAsync<List<StokSayimDetay>>();
                return list ?? new List<StokSayimDetay>();
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine($"[CloudSync] PullStokSayimDetaylarAsync Error: {ex.Message}");
                return new List<StokSayimDetay>();
            }
        }

        public async Task<List<Note>> PullNotesAsync() => await GlobalGetAllAsync<Note>("Notes");

        public async Task PushAllDataAsync(
            List<CariKart> cariler, 
            List<CariHareket> cariHareketler, 
            List<StokKart> stoklar, 
            List<StokHareket> stokHareketler, 
            List<Fatura> faturalar, 
            List<FaturaDetay> faturaDetaylar, 
            List<Siparis> siparisler, 
            List<SiparisDetay> siparisDetaylar, 
            List<Teklif> teklifler, 
            List<TeklifDetay> teklifDetaylar, 
            List<BankaKart> bankalar, 
            List<KasaHareket> kasaHareketler, 
            List<BankaHareket> bankaHareketler, 
            List<Cek> cekler, 
            List<Senet> senetler, 
            List<KrediKartiIslem> kkIslemler, 
            List<EftIslem> eftIslemler, 
            List<DovizKur> kurlar, 
            List<BelgeArsiv> belgeler,
            List<Note>? notes = null,
            List<Gorev>? gorevler = null,
            List<Personel>? personeller = null,
            List<SatisHedefi>? hedefler = null,
            List<HaftalikSatisHedefi>? haftalikHedefler = null,
            List<YillikSatisHedefi>? yillikHedefler = null,
            List<StokSayimFisi>? stokSayimlar = null,
            List<StokSayimDetay>? stokSayimDetaylar = null,
            List<PortfoyKart>? portfoyler = null)
        {
            if (!IsConnected) return;

            await SafeRun(async () =>
            {
                // We map lists to dictionaries to preserve key structure in Firebase while overwriting parent nodes entirely
                var carilerDict = new Dictionary<string, CariKart>();
                foreach (var c in cariler ?? new()) if (c != null) carilerDict[c.Id.ToString()] = c;
                await _firebase!.Child(GetYearlyPath("Cariler")).PutAsync(carilerDict);

                var cariHareketlerDict = new Dictionary<string, CariHareket>();
                foreach (var ch in cariHareketler ?? new()) if (ch != null) cariHareketlerDict[ch.Id.ToString()] = ch;
                await _firebase!.Child(GetYearlyPath("CariHareketler")).PutAsync(cariHareketlerDict);

                var stoklarDict = new Dictionary<string, StokKart>();
                foreach (var s in stoklar ?? new()) if (s != null) stoklarDict[s.Id.ToString()] = s;
                await _firebase!.Child(GetYearlyPath("Stoklar")).PutAsync(stoklarDict);

                var stokHareketlerDict = new Dictionary<string, StokHareket>();
                foreach (var sh in stokHareketler ?? new()) if (sh != null) stokHareketlerDict[sh.Id.ToString()] = sh;
                await _firebase!.Child(GetYearlyPath("StokHareketler")).PutAsync(stokHareketlerDict);

                var faturalarDict = new Dictionary<string, Fatura>();
                foreach (var f in faturalar ?? new()) if (f != null) faturalarDict[f.Id.ToString()] = f;
                await _firebase!.Child(GetYearlyPath("Faturalar")).PutAsync(faturalarDict);

                // FaturaDetaylar grouped by FaturaId
                var faturaDetaylarDict = new Dictionary<string, List<FaturaDetay>>();
                foreach (var d in faturaDetaylar ?? new())
                {
                    if (d == null) continue;
                    var key = d.FaturaId.ToString();
                    if (!faturaDetaylarDict.ContainsKey(key)) faturaDetaylarDict[key] = new List<FaturaDetay>();
                    faturaDetaylarDict[key].Add(d);
                }
                await _firebase!.Child(GetYearlyPath("FaturaDetaylar")).PutAsync(faturaDetaylarDict);

                var siparislerDict = new Dictionary<string, Siparis>();
                foreach (var sp in siparisler ?? new()) if (sp != null) siparislerDict[sp.Id.ToString()] = sp;
                await _firebase!.Child(GetYearlyPath("Siparisler")).PutAsync(siparislerDict);

                // SiparisDetaylar grouped by SiparisId
                var siparisDetaylarDict = new Dictionary<string, List<SiparisDetay>>();
                foreach (var d in siparisDetaylar ?? new())
                {
                    if (d == null) continue;
                    var key = d.SiparisId.ToString();
                    if (!siparisDetaylarDict.ContainsKey(key)) siparisDetaylarDict[key] = new List<SiparisDetay>();
                    siparisDetaylarDict[key].Add(d);
                }
                await _firebase!.Child(GetYearlyPath("SiparisDetaylar")).PutAsync(siparisDetaylarDict);

                var tekliflerDict = new Dictionary<string, Teklif>();
                foreach (var t in teklifler ?? new()) if (t != null) tekliflerDict[t.Id.ToString()] = t;
                await _firebase!.Child(GetYearlyPath("Teklifler")).PutAsync(tekliflerDict);

                // TeklifDetaylar grouped by TeklifId
                var teklifDetaylarDict = new Dictionary<string, List<TeklifDetay>>();
                foreach (var d in teklifDetaylar ?? new())
                {
                    if (d == null) continue;
                    var key = d.TeklifId.ToString();
                    if (!teklifDetaylarDict.ContainsKey(key)) teklifDetaylarDict[key] = new List<TeklifDetay>();
                    teklifDetaylarDict[key].Add(d);
                }
                await _firebase!.Child(GetYearlyPath("TeklifDetaylar")).PutAsync(teklifDetaylarDict);

                var bankalarDict = new Dictionary<string, BankaKart>();
                foreach (var b in bankalar ?? new()) if (b != null) bankalarDict[b.Id.ToString()] = b;
                await _firebase!.Child(GetYearlyPath("Bankalar")).PutAsync(bankalarDict);

                var kasaHareketlerDict = new Dictionary<string, KasaHareket>();
                foreach (var kh in kasaHareketler ?? new()) if (kh != null) kasaHareketlerDict[kh.Id.ToString()] = kh;
                await _firebase!.Child(GetYearlyPath("KasaHareketler")).PutAsync(kasaHareketlerDict);

                var bankaHareketlerDict = new Dictionary<string, BankaHareket>();
                foreach (var bh in bankaHareketler ?? new()) if (bh != null) bankaHareketlerDict[bh.Id.ToString()] = bh;
                await _firebase!.Child(GetYearlyPath("BankaHareketler")).PutAsync(bankaHareketlerDict);

                var ceklerDict = new Dictionary<string, Cek>();
                foreach (var ck in cekler ?? new()) if (ck != null) ceklerDict[ck.Id.ToString()] = ck;
                await _firebase!.Child(GetYearlyPath("Cekler")).PutAsync(ceklerDict);

                var senetlerDict = new Dictionary<string, Senet>();
                foreach (var sn in senetler ?? new()) if (sn != null) senetlerDict[sn.Id.ToString()] = sn;
                await _firebase!.Child(GetYearlyPath("Senetler")).PutAsync(senetlerDict);

                var kkIslemlerDict = new Dictionary<string, KrediKartiIslem>();
                foreach (var kk in kkIslemler ?? new()) if (kk != null) kkIslemlerDict[kk.Id.ToString()] = kk;
                await _firebase!.Child(GetYearlyPath("KrediKartlari")).PutAsync(kkIslemlerDict);

                var eftIslemlerDict = new Dictionary<string, EftIslem>();
                foreach (var eft in eftIslemler ?? new()) if (eft != null) eftIslemlerDict[eft.Id.ToString()] = eft;
                await _firebase!.Child(GetYearlyPath("EftIslemleri")).PutAsync(eftIslemlerDict);

                var kurlarDict = new Dictionary<string, DovizKur>();
                foreach (var k in kurlar ?? new()) if (k != null) kurlarDict[k.Id.ToString()] = k;
                await _firebase!.Child(GetYearlyPath("DovizKurlari")).PutAsync(kurlarDict);

                var belgelerDict = new Dictionary<string, BelgeArsiv>();
                foreach (var b in belgeler ?? new()) if (b != null) belgelerDict[b.Id.ToString()] = b;
                await _firebase!.Child(GetYearlyPath("BelgeArsiv")).PutAsync(belgelerDict);

                if (notes != null)
                {
                    var notesDict = new Dictionary<string, Note>();
                    foreach (var n in notes) if (n != null) notesDict[n.Id.ToString()] = n;
                    await _firebase!.Child(GetYearlyPath("Notes")).PutAsync(notesDict);
                }

                if (gorevler != null)
                {
                    var gorevlerDict = new Dictionary<string, Gorev>();
                    foreach (var g in gorevler) if (g != null) gorevlerDict[g.Id.ToString()] = g;
                    await _firebase!.Child(GetYearlyPath("Gorevler")).PutAsync(gorevlerDict);
                }

                if (personeller != null)
                {
                    var personellerDict = new Dictionary<string, Personel>();
                    foreach (var p in personeller) if (p != null) personellerDict[p.Id.ToString()] = p;
                    await _firebase!.Child(GetYearlyPath("Personeller")).PutAsync(personellerDict);
                }

                if (hedefler != null)
                {
                    var hedeflerDict = new Dictionary<string, SatisHedefi>();
                    foreach (var h in hedefler) if (h != null) hedeflerDict[h.Id.ToString()] = h;
                    await _firebase!.Child(GetYearlyPath("SatisHedefleri")).PutAsync(hedeflerDict);
                }

                if (haftalikHedefler != null)
                {
                    var haftalikDict = new Dictionary<string, HaftalikSatisHedefi>();
                    foreach (var hh in haftalikHedefler) if (hh != null) haftalikDict[hh.Id.ToString()] = hh;
                    await _firebase!.Child(GetYearlyPath("HaftalikSatisHedefleri")).PutAsync(haftalikDict);
                }

                if (yillikHedefler != null)
                {
                    var yillikDict = new Dictionary<string, YillikSatisHedefi>();
                    foreach (var yh in yillikHedefler) if (yh != null) yillikDict[yh.Id.ToString()] = yh;
                    await _firebase!.Child(GetYearlyPath("YillikSatisHedefleri")).PutAsync(yillikDict);
                }

                if (stokSayimlar != null)
                {
                    var sayimDict = new Dictionary<string, StokSayimFisi>();
                    foreach (var sf in stokSayimlar) if (sf != null) sayimDict[sf.Id.ToString()] = sf;
                    await _firebase!.Child(GetYearlyPath("StokSayimlar")).PutAsync(sayimDict);
                }

                if (stokSayimDetaylar != null)
                {
                    var sayimDetayDict = new Dictionary<string, List<StokSayimDetay>>();
                    foreach (var sd in stokSayimDetaylar)
                    {
                        if (sd == null) continue;
                        var key = sd.FisId.ToString();
                        if (!sayimDetayDict.ContainsKey(key)) sayimDetayDict[key] = new List<StokSayimDetay>();
                        sayimDetayDict[key].Add(sd);
                    }
                    await _firebase!.Child(GetYearlyPath("StokSayimDetaylar")).PutAsync(sayimDetayDict);
                }

                if (portfoyler != null)
                {
                    var portfoyDict = new Dictionary<string, PortfoyKart>();
                    foreach (var pf in portfoyler) if (pf != null) portfoyDict[pf.Id.ToString()] = pf;
                    await _firebase!.Child(GetYearlyPath("PortfoyKartlari")).PutAsync(portfoyDict);
                }
            });
        }

        // --- PULL METHODS ---
        private async Task<List<T>?> GlobalGetAllAsync<T>(string resourceName) where T : class
        {
            if (!IsConnected) return null;
            try 
            {
                string cleanUrl = _config.BaseUrl.TrimEnd('/');
                string token = await GetFirebaseAuthTokenAsync();
                string authQuery = !string.IsNullOrEmpty(token) ? $"?auth={token}" : "";
                string url = $"{cleanUrl}/{GetYearlyPath(resourceName)}.json{authQuery}";

                using var response = await _authHttpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[CloudSync] GlobalGetAllAsync HTTP {(int)response.StatusCode} for {resourceName}");
                    return new List<T>();
                }

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
                {
                    return new List<T>();
                }

                json = json.Trim();
                var list = new List<T>();
                var idProp = typeof(T).GetProperty("Id");

                if (json.StartsWith("["))
                {
                    // Firebase returned a JSON Array (e.g. [null, {...}, null, {...}])
                    var jArray = Newtonsoft.Json.Linq.JArray.Parse(json);
                    for (int idx = 0; idx < jArray.Count; idx++)
                    {
                        var tokenItem = jArray[idx];
                        if (tokenItem == null || tokenItem.Type == Newtonsoft.Json.Linq.JTokenType.Null) continue;
                        
                        var obj = tokenItem.ToObject<T>();
                        if (obj != null)
                        {
                            if (idProp != null && idProp.CanWrite)
                            {
                                var currentVal = (int)(idProp.GetValue(obj) ?? 0);
                                if (currentVal == 0)
                                {
                                    idProp.SetValue(obj, idx);
                                }
                            }
                            list.Add(obj);
                        }
                    }
                }
                else if (json.StartsWith("{"))
                {
                    // Firebase returned a JSON Object (e.g. {"1": {...}, "2": {...}})
                    var jObj = Newtonsoft.Json.Linq.JObject.Parse(json);
                    foreach (var prop in jObj.Properties())
                    {
                        if (prop.Value == null || prop.Value.Type == Newtonsoft.Json.Linq.JTokenType.Null) continue;

                        var obj = prop.Value.ToObject<T>();
                        if (obj != null)
                        {
                            if (idProp != null && idProp.CanWrite && int.TryParse(prop.Name, out int idVal))
                            {
                                var currentVal = (int)(idProp.GetValue(obj) ?? 0);
                                if (currentVal == 0)
                                {
                                    idProp.SetValue(obj, idVal);
                                }
                            }
                            list.Add(obj);
                        }
                    }
                }

                return list;
            } 
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"[CloudSync] GlobalGetAllAsync error for {resourceName}: {ex.Message}");
                return null;
            }
        }

        public async Task<List<DovizKur>?> PullDovizKurlariAsync() => await GlobalGetAllAsync<DovizKur>("DovizKurlari");

        public async Task<List<BelgeArsiv>?> PullBelgeArsivAsync() => await GlobalGetAllAsync<BelgeArsiv>("BelgeArsiv");

        public async Task<List<Siparis>?> PullSiparislerAsync() => await GlobalGetAllAsync<Siparis>("Siparisler");

        public async Task<List<SiparisDetay>> PullSiparisDetaylarAsync(int siparisId)
        {
             if (!IsConnected) return new List<SiparisDetay>();
             try {
                var list = await _firebase!.Child(GetYearlyPath("SiparisDetaylar")).Child(siparisId.ToString()).OnceSingleAsync<List<SiparisDetay>>();
                return list ?? new List<SiparisDetay>();
             } catch (Exception ex) {
                 System.Diagnostics.Debug.WriteLine($"[CloudSync] PullSiparisDetaylarAsync Error: {ex.Message}");
                 return new List<SiparisDetay>();
             }
        }

        public async Task<List<Teklif>?> PullTekliflerAsync() => await GlobalGetAllAsync<Teklif>("Teklifler");

        public async Task<List<TeklifDetay>> PullTeklifDetaylarAsync(int teklifId)
        {
             if (!IsConnected) return new List<TeklifDetay>();
             try {
                var list = await _firebase!.Child(GetYearlyPath("TeklifDetaylar")).Child(teklifId.ToString()).OnceSingleAsync<List<TeklifDetay>>();
                return list ?? new List<TeklifDetay>();
             } catch (Exception ex) {
                 System.Diagnostics.Debug.WriteLine($"[CloudSync] PullTeklifDetaylarAsync Error: {ex.Message}");
                 return new List<TeklifDetay>();
             }
        }
        public async Task<List<CariKart>?> PullCarilerAsync() => await GlobalGetAllAsync<CariKart>("Cariler");
        public async Task<List<CariHareket>?> PullCariHareketlerAsync() => await GlobalGetAllAsync<CariHareket>("CariHareketler");
        public async Task<List<StokKart>?> PullStoklarAsync() => await GlobalGetAllAsync<StokKart>("Stoklar");
        public async Task<List<StokHareket>?> PullStokHareketlerAsync() => await GlobalGetAllAsync<StokHareket>("StokHareketler");
        public async Task<List<Fatura>?> PullFaturalarAsync() => await GlobalGetAllAsync<Fatura>("Faturalar");
        
        public async Task<List<FaturaDetay>> PullFaturaDetaylarAsync(int faturaId)
        {
             if (!IsConnected) return new List<FaturaDetay>();
             try 
             {
                 string cleanUrl = _config.BaseUrl.TrimEnd('/');
                 string token = await GetFirebaseAuthTokenAsync();
                 string authQuery = !string.IsNullOrEmpty(token) ? $"?auth={token}" : "";
                 string url = $"{cleanUrl}/{GetYearlyPath("FaturaDetaylar")}/{faturaId}.json{authQuery}";

                 using var response = await _authHttpClient.GetAsync(url);
                 if (!response.IsSuccessStatusCode) return new List<FaturaDetay>();

                 string json = await response.Content.ReadAsStringAsync();
                 if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null") return new List<FaturaDetay>();

                 json = json.Trim();
                 var list = new List<FaturaDetay>();

                 if (json.StartsWith("["))
                 {
                     var jArray = Newtonsoft.Json.Linq.JArray.Parse(json);
                     for (int idx = 0; idx < jArray.Count; idx++)
                     {
                         var item = jArray[idx];
                         if (item == null || item.Type == Newtonsoft.Json.Linq.JTokenType.Null) continue;
                         var det = item.ToObject<FaturaDetay>();
                         if (det != null)
                         {
                             if (det.Id == 0) det.Id = idx;
                             if (det.FaturaId == 0) det.FaturaId = faturaId;
                             list.Add(det);
                         }
                     }
                 }
                 else if (json.StartsWith("{"))
                 {
                     var jObj = Newtonsoft.Json.Linq.JObject.Parse(json);
                     foreach (var prop in jObj.Properties())
                     {
                         if (prop.Value == null || prop.Value.Type == Newtonsoft.Json.Linq.JTokenType.Null) continue;
                         var det = prop.Value.ToObject<FaturaDetay>();
                         if (det != null)
                         {
                             if (det.Id == 0 && int.TryParse(prop.Name, out int parsedId)) det.Id = parsedId;
                             if (det.FaturaId == 0) det.FaturaId = faturaId;
                             list.Add(det);
                         }
                     }
                 }
                 return list;
             } 
             catch (Exception ex) 
             {
                 System.Diagnostics.Debug.WriteLine($"[CloudSync] PullFaturaDetaylarAsync Error: {ex.Message}");
                 return new List<FaturaDetay>();
             }
        }

        public async Task<List<BankaKart>?> PullBankalarAsync() => await GlobalGetAllAsync<BankaKart>("Bankalar");
        public async Task<List<KrediKartiIslem>?> PullKrediKartlariAsync() => await GlobalGetAllAsync<KrediKartiIslem>("KrediKartlari");
        public async Task<List<EftIslem>?> PullEftIslemleriAsync() => await GlobalGetAllAsync<EftIslem>("EftIslemleri");
        public async Task<List<KasaHareket>?> PullKasaHareketlerAsync() => await GlobalGetAllAsync<KasaHareket>("KasaHareketler");
        public async Task<List<BankaHareket>?> PullBankaHareketlerAsync() => await GlobalGetAllAsync<BankaHareket>("BankaHareketler");
        public async Task<List<Cek>?> PullCeklerAsync() => await GlobalGetAllAsync<Cek>("Cekler");
        public async Task<List<Senet>?> PullSenetlerAsync() => await GlobalGetAllAsync<Senet>("Senetler");
        public async Task<List<MusteriTakipKlasor>?> PullMusteriTakipKlasorlerAsync() => await GlobalGetAllAsync<MusteriTakipKlasor>("MusteriTakipKlasorler");
        public async Task<List<MusteriTakipDetay>?> PullMusteriTakipDetaylarAsync() => await GlobalGetAllAsync<MusteriTakipDetay>("MusteriTakipDetaylar");

        public async Task SyncGenericAsync<T>(string resourceName, T item, int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath(resourceName)).Child(id.ToString()).PutAsync(item);
            });
        }

        public async Task DeleteGenericAsync(string resourceName, int id)
        {
            await SafeRun(async () => 
            {
                await _firebase!.Child(GetYearlyPath(resourceName)).Child(id.ToString()).DeleteAsync();
            });
        }

        // --- USER SYNC METHODS (GLOBAL / MULTI-DEVICE) ---
        public async Task SyncUserAsync(User user)
        {
            await SafeRun(async () =>
            {
                if (_firebase != null && user != null)
                {
                    var key = user.Id > 0 ? user.Id.ToString() : (user.Username?.ToLower().Trim() ?? "1");
                    await _firebase.Child("users").Child(key).PutAsync(user);
                }
            });
        }

        public async Task DeleteUserFromCloudAsync(int userId, string? username = null)
        {
            await SafeRun(async () =>
            {
                if (_firebase != null)
                {
                    if (userId > 0)
                    {
                        await _firebase.Child("users").Child(userId.ToString()).DeleteAsync();
                    }
                    if (!string.IsNullOrEmpty(username))
                    {
                        await _firebase.Child("users").Child(username.ToLower().Trim()).DeleteAsync();
                    }
                }
            });
        }

        public async Task<List<User>> PullUsersAsync()
        {
            if (!IsConnected) return new List<User>();
            try
            {
                var collection = await _firebase!.Child("users").OnceAsync<User>();
                var list = new List<User>();
                foreach (var item in collection)
                {
                    if (item?.Object != null)
                    {
                        var u = item.Object;
                        if (u.Id == 0 && int.TryParse(item.Key, out int idVal))
                        {
                            u.Id = idVal;
                        }
                        list.Add(u);
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CloudSync] PullUsersAsync Error: {ex.Message}");
                return new List<User>();
            }
        }

        // --- REAL-TIME STREAMING (SSE / SERVER-SENT EVENTS) LISTENER ---
        private System.Threading.CancellationTokenSource? _streamCts;
        public event Action<string, string>? OnCloudStreamDataReceived;

        public void StartRealtimeStreamListener()
        {
            StopRealtimeStreamListener();
            if (!IsConnected || string.IsNullOrEmpty(_config.BaseUrl)) return;

            _streamCts = new System.Threading.CancellationTokenSource();
            var token = _streamCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var cleanUrl = _config.BaseUrl.TrimEnd('/');
                        var authToken = await GetFirebaseAuthTokenAsync();
                        var year = _yearContext?.CurrentYear ?? DateTime.Now.Year;
                        var streamUrl = $"{cleanUrl}/companies/default/years/{year}.json";
                        if (!string.IsNullOrEmpty(authToken))
                        {
                            streamUrl += $"?auth={authToken}";
                        }

                        using var client = new System.Net.Http.HttpClient();
                        client.Timeout = TimeSpan.FromMinutes(30);
                        using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, streamUrl);
                        req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

                        using var resp = await client.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, token);
                        if (!resp.IsSuccessStatusCode)
                        {
                            await Task.Delay(5000, token);
                            continue;
                        }

                        using var stream = await resp.Content.ReadAsStreamAsync(token);
                        using var reader = new System.IO.StreamReader(stream);

                        string? currentEvent = null;
                        while (!token.IsCancellationRequested && !reader.EndOfStream)
                        {
                            var line = await reader.ReadLineAsync(token);
                            if (string.IsNullOrWhiteSpace(line))
                            {
                                currentEvent = null;
                                continue;
                            }

                            if (line.StartsWith("event: "))
                            {
                                currentEvent = line.Substring(7).Trim();
                            }
                            else if (line.StartsWith("data: "))
                            {
                                var dataJson = line.Substring(6).Trim();
                                if (currentEvent == "put" || currentEvent == "patch")
                                {
                                    try
                                    {
                                        OnCloudStreamDataReceived?.Invoke(currentEvent, dataJson);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CloudSync SSE Listener] Error: {ex.Message}");
                        await Task.Delay(5000, token);
                    }
                }
            }, token);
        }

        public void StopRealtimeStreamListener()
        {
            if (_streamCts != null)
            {
                try { _streamCts.Cancel(); } catch { }
                _streamCts.Dispose();
                _streamCts = null;
            }
        }
    }
}
