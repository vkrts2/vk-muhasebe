using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using ErmayMuhasebe.Models;

namespace ErmayMuhasebe.Services
{
    public class CloudConfig
    {
        public string BaseUrl { get; set; } = "https://fqgbdymffknglqeqoogt.supabase.co";
        public string AuthSecret { get; set; } = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZxZ2JkeW1mZmtuZ2xxZXFvb2d0Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODk1ODUzMDEsImV4cCI6MjEwNTE2MTMwMX0.pBeE2ivWpbkAd8KSN1y2pXNZPIr_1mGMLXXHYPzjTDg";
        public string GoogleApiKey { get; set; } = "";
        public string GoogleClientId { get; set; } = "";
        public string GoogleClientSecret { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public bool IsAutoSyncEnabled { get; set; } = true;
    }

    public class CloudSyncService
    {
        private CloudConfig _config = new();
        private readonly string _configPath;
        private readonly IYearContext _yearContext;
        private readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        private static readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public object? Client => this;
        public CloudConfig Config => _config;
        public bool IsConnected => !string.IsNullOrEmpty(_config.BaseUrl) && !string.IsNullOrEmpty(_config.AuthSecret) && _config.IsActive;
        public string BaseUrl => _config.BaseUrl;
        public string AuthSecret => _config.AuthSecret;
        public bool IsAutoSyncEnabled => _config.IsAutoSyncEnabled;

        public void Disconnect()
        {
            _config.IsActive = false;
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
            return resourceName;
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var loaded = JsonSerializer.Deserialize<CloudConfig>(json);
                    if (loaded != null && !string.IsNullOrEmpty(loaded.BaseUrl) && !string.IsNullOrEmpty(loaded.AuthSecret))
                    {
                        _config = loaded;
                    }
                }
                else
                {
                    _config = new CloudConfig();
                    SaveConfig(_config.BaseUrl, _config.AuthSecret);
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
            _config.IsActive = !string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(secret);
            
            try
            {
                var json = JsonSerializer.Serialize(_config);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cloud Config Save Error: {ex.Message}");
            }
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, string pathAndQuery)
        {
            string cleanBase = _config.BaseUrl.TrimEnd('/');
            string url = $"{cleanBase}/rest/v1/{pathAndQuery.TrimStart('/')}";
            var req = new HttpRequestMessage(method, url);
            req.Headers.Add("apikey", _config.AuthSecret);
            req.Headers.Add("Authorization", $"Bearer {_config.AuthSecret}");
            return req;
        }

        private async Task SafeRun(Func<Task> action)
        {
            if (!IsConnected) return;
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase SafeRun Error] {ex.Message}");
            }
        }

        public async Task<bool> UpsertAsync<T>(string table, T item)
        {
            if (!IsConnected || item == null) return false;
            try
            {
                using var req = CreateRequest(HttpMethod.Post, table);
                req.Headers.Add("Prefer", "resolution=merge-duplicates");
                string json = JsonSerializer.Serialize(item, _jsonOpts);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase Upsert Error] {table}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpsertBatchAsync<T>(string table, IEnumerable<T> items)
        {
            if (!IsConnected || items == null || !items.Any()) return false;
            try
            {
                using var req = CreateRequest(HttpMethod.Post, table);
                req.Headers.Add("Prefer", "resolution=merge-duplicates");
                string json = JsonSerializer.Serialize(items, _jsonOpts);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase UpsertBatch Error] {table}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAsync(string table, int id)
        {
            if (!IsConnected) return false;
            try
            {
                using var req = CreateRequest(HttpMethod.Delete, $"{table}?id=eq.{id}");
                using var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase Delete Error] {table}/{id}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteFilteredAsync(string table, string filter)
        {
            if (!IsConnected) return false;
            try
            {
                using var req = CreateRequest(HttpMethod.Delete, $"{table}?{filter}");
                using var res = await _http.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase DeleteFiltered Error] {table}?{filter}: {ex.Message}");
                return false;
            }
        }

        public async Task<List<T>?> GetAllAsync<T>(string table, string? filter = null)
        {
            if (!IsConnected) return null;
            try
            {
                string query = table + "?select=*";
                if (!string.IsNullOrEmpty(filter)) query += "&" + filter;
                using var req = CreateRequest(HttpMethod.Get, query);
                using var res = await _http.SendAsync(req);
                if (!res.IsSuccessStatusCode) return null;
                string json = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<T>>(json, _jsonOpts);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Supabase GetAll Error] {table}: {ex.Message}");
                return null;
            }
        }

        // ==========================================
        // CARİ KARTLAR & CARİ HAREKETLER
        // ==========================================
        public async Task SyncCariAsync(CariKart cari) => await UpsertAsync("cariler", cari);
        public async Task DeleteCariAsync(int id) => await DeleteAsync("cariler", id);
        public async Task<List<CariKart>?> PullCarilerAsync() => await GetAllAsync<CariKart>("cariler");

        public async Task SyncCariHareketAsync(CariHareket hareket) => await UpsertAsync("cari_hareketler", hareket);
        public async Task DeleteCariHareketAsync(int id) => await DeleteAsync("cari_hareketler", id);
        public async Task<List<CariHareket>?> PullCariHareketlerAsync() => await GetAllAsync<CariHareket>("cari_hareketler");

        public async Task DeleteCariHareketByFaturaIdAsync(int faturaId, string? evrakNo = null)
        {
            if (faturaId > 0)
                await DeleteFilteredAsync("cari_hareketler", $"fatura_id=eq.{faturaId}");
            if (!string.IsNullOrEmpty(evrakNo))
            {
                await DeleteFilteredAsync("cari_hareketler", $"evrak_no=eq.{Uri.EscapeDataString(evrakNo)}");
                await DeleteFilteredAsync("cari_hareketler", $"evrak_no=eq.{Uri.EscapeDataString("KPL-" + evrakNo)}");
            }
        }

        public async Task SyncCariToAllYearsAsync(CariKart cari) => await SyncCariAsync(cari);

        public async Task UpdateFutureBalancesAsync(string entityType, int entityId, decimal borcDelta, decimal alacakDelta)
        {
            await SafeRun(async () =>
            {
                string table = entityType.ToLower() switch
                {
                    "cariler" or "cari" => "cariler",
                    _ => entityType.ToLower()
                };

                var existing = await GetAllAsync<CariKart>(table, $"id=eq.{entityId}");
                var item = existing?.FirstOrDefault();
                if (item != null)
                {
                    item.Borc += borcDelta;
                    item.Alacak += alacakDelta;
                    await UpsertAsync(table, item);
                }
            });
        }

        // ==========================================
        // STOKLAR & STOK HAREKETLER
        // ==========================================
        public async Task SyncStokAsync(StokKart stok) => await UpsertAsync("stoklar", stok);
        public async Task DeleteStokAsync(int id) => await DeleteAsync("stoklar", id);
        public async Task<List<StokKart>?> PullStoklarAsync() => await GetAllAsync<StokKart>("stoklar");

        public async Task SyncStokHareketAsync(StokHareket hareket) => await UpsertAsync("stok_hareketler", hareket);
        public async Task DeleteStokHareketAsync(int id) => await DeleteAsync("stok_hareketler", id);
        public async Task<List<StokHareket>?> PullStokHareketlerAsync() => await GetAllAsync<StokHareket>("stok_hareketler");

        public async Task DeleteStokHareketByFaturaIdAsync(int faturaId, string? evrakNo = null)
        {
            if (faturaId > 0)
                await DeleteFilteredAsync("stok_hareketler", $"fatura_id=eq.{faturaId}");
            if (!string.IsNullOrEmpty(evrakNo))
                await DeleteFilteredAsync("stok_hareketler", $"evrak_no=eq.{Uri.EscapeDataString(evrakNo)}");
        }

        public async Task SyncStokGrupAsync(StokGrupDef grup) => await Task.CompletedTask;
        public async Task DeleteStokGrupAsync(int id) => await Task.CompletedTask;

        // ==========================================
        // FATURALAR & FATURA DETAYLAR
        // ==========================================
        public async Task SyncFaturaAsync(Fatura fatura) => await UpsertAsync("faturalar", fatura);
        public async Task DeleteFaturaAsync(int id)
        {
            await DeleteFilteredAsync("fatura_detaylar", $"fatura_id=eq.{id}");
            await DeleteAsync("faturalar", id);
        }
        public async Task<List<Fatura>?> PullFaturalarAsync() => await GetAllAsync<Fatura>("faturalar");

        public async Task SyncFaturaDetaylarAsync(int faturaId, List<FaturaDetay> detaylar)
        {
            await SafeRun(async () =>
            {
                await DeleteFilteredAsync("fatura_detaylar", $"fatura_id=eq.{faturaId}");
                if (detaylar != null && detaylar.Count > 0)
                {
                    foreach (var d in detaylar) d.FaturaId = faturaId;
                    await UpsertBatchAsync("fatura_detaylar", detaylar);
                }
            });
        }

        public async Task DeleteFaturaDetaylarAsync(int faturaId)
        {
            await DeleteFilteredAsync("fatura_detaylar", $"fatura_id=eq.{faturaId}");
        }

        public async Task<List<FaturaDetay>> PullFaturaDetaylarAsync(int faturaId)
        {
            var list = await GetAllAsync<FaturaDetay>("fatura_detaylar", $"fatura_id=eq.{faturaId}");
            return list ?? new List<FaturaDetay>();
        }

        // ==========================================
        // SİPARİŞLER & TEKLİFLER
        // ==========================================
        public async Task SyncSiparisAsync(Siparis siparis) => await UpsertAsync("siparisler", siparis);
        public async Task DeleteSiparisAsync(int id)
        {
            await DeleteFilteredAsync("siparis_detaylar", $"siparis_id=eq.{id}");
            await DeleteAsync("siparisler", id);
        }
        public async Task<List<Siparis>?> PullSiparislerAsync() => await GetAllAsync<Siparis>("siparisler");

        public async Task SyncSiparisDetaylarAsync(int siparisId, List<SiparisDetay> detaylar)
        {
            await SafeRun(async () =>
            {
                await DeleteFilteredAsync("siparis_detaylar", $"siparis_id=eq.{siparisId}");
                if (detaylar != null && detaylar.Count > 0)
                {
                    foreach (var d in detaylar) d.SiparisId = siparisId;
                    await UpsertBatchAsync("siparis_detaylar", detaylar);
                }
            });
        }

        public async Task DeleteSiparisDetaylarAsync(int siparisId)
        {
            await DeleteFilteredAsync("siparis_detaylar", $"siparis_id=eq.{siparisId}");
        }

        public async Task<List<SiparisDetay>> PullSiparisDetaylarAsync(int siparisId)
        {
            var list = await GetAllAsync<SiparisDetay>("siparis_detaylar", $"siparis_id=eq.{siparisId}");
            return list ?? new List<SiparisDetay>();
        }

        public async Task SyncTeklifAsync(Teklif teklif) => await UpsertAsync("teklifler", teklif);
        public async Task DeleteTeklifAsync(int id)
        {
            await DeleteFilteredAsync("teklif_detaylar", $"teklif_id=eq.{id}");
            await DeleteAsync("teklifler", id);
        }
        public async Task<List<Teklif>?> PullTekliflerAsync() => await GetAllAsync<Teklif>("teklifler");

        public async Task SyncTeklifDetaylarAsync(int teklifId, List<TeklifDetay> detaylar)
        {
            await SafeRun(async () =>
            {
                await DeleteFilteredAsync("teklif_detaylar", $"teklif_id=eq.{teklifId}");
                if (detaylar != null && detaylar.Count > 0)
                {
                    foreach (var d in detaylar) d.TeklifId = teklifId;
                    await UpsertBatchAsync("teklif_detaylar", detaylar);
                }
            });
        }

        public async Task DeleteTeklifDetaylarAsync(int teklifId)
        {
            await DeleteFilteredAsync("teklif_detaylar", $"teklif_id=eq.{teklifId}");
        }

        public async Task<List<TeklifDetay>> PullTeklifDetaylarAsync(int teklifId)
        {
            var list = await GetAllAsync<TeklifDetay>("teklif_detaylar", $"teklif_id=eq.{teklifId}");
            return list ?? new List<TeklifDetay>();
        }

        // ==========================================
        // KASALAR & BANKALAR
        // ==========================================
        public async Task SyncKasaHareketAsync(KasaHareket hareket) => await UpsertAsync("kasa_hareketler", hareket);
        public async Task DeleteKasaHareketAsync(int id) => await DeleteAsync("kasa_hareketler", id);
        public async Task<List<KasaHareket>?> PullKasaHareketlerAsync() => await GetAllAsync<KasaHareket>("kasa_hareketler");

        public async Task SyncBankaAsync(BankaKart banka) => await UpsertAsync("bankalar", banka);
        public async Task DeleteBankaAsync(int id) => await DeleteAsync("bankalar", id);
        public async Task<List<BankaKart>?> PullBankalarAsync() => await GetAllAsync<BankaKart>("bankalar");

        public async Task SyncBankaHareketAsync(BankaHareket hareket) => await UpsertAsync("banka_hareketler", hareket);
        public async Task DeleteBankaHareketAsync(int id) => await DeleteAsync("banka_hareketler", id);
        public async Task<List<BankaHareket>?> PullBankaHareketlerAsync() => await GetAllAsync<BankaHareket>("banka_hareketler");

        // ==========================================
        // FİRMA PROFİLİ & NOTLAR
        // ==========================================
        public async Task SyncFirmaProfiliAsync(FirmaProfili profil)
        {
            if (profil.Id == 0) profil.Id = 1;
            await UpsertAsync("firma_profili", profil);
        }

        public async Task<FirmaProfili?> PullFirmaProfiliAsync()
        {
            var list = await GetAllAsync<FirmaProfili>("firma_profili", "id=eq.1");
            return list?.FirstOrDefault();
        }

        public async Task SyncFaturaTasarimiAsync(FaturaTasarimi tasarim) => await Task.CompletedTask;

        public async Task SyncNoteAsync(Note note) => await UpsertAsync("notlar", note);
        public async Task DeleteNoteAsync(int id) => await DeleteAsync("notlar", id);
        public async Task<List<Note>> PullNotesAsync()
        {
            var list = await GetAllAsync<Note>("notlar");
            return list ?? new List<Note>();
        }

        // ==========================================
        // GENERIC STUBS FOR OPTIONAL MODULES
        // ==========================================
        public async Task SyncGenericAsync<T>(string resourceName, T data, int id) => await Task.CompletedTask;
        public async Task DeleteGenericAsync(string resourceName, int id) => await Task.CompletedTask;
        public async Task<List<T>> GlobalGetAllAsync<T>(string resourceName) => new();

        public async Task<List<KrediKartiIslem>> PullKrediKartlariAsync() => new();
        public async Task<List<EftIslem>> PullEftIslemleriAsync() => new();
        public async Task<List<Cek>> PullCeklerAsync() => new();
        public async Task<List<Senet>> PullSenetlerAsync() => new();
        public async Task<List<MusteriTakipKlasor>> PullMusteriTakipKlasorlerAsync() => new();
        public async Task<List<MusteriTakipDetay>> PullMusteriTakipDetaylarAsync() => new();
        public async Task<List<User>> PullUsersAsync() => new();
        public async Task SyncUserAsync(User user) => await Task.CompletedTask;
        public async Task DeleteUserFromCloudAsync(int userId, string? username = null) => await Task.CompletedTask;

        public async Task SyncCekAsync(Cek cek) => await Task.CompletedTask;
        public async Task SyncSenetAsync(Senet senet) => await Task.CompletedTask;
        public async Task SyncKrediKartiIslemAsync(KrediKartiIslem islem) => await Task.CompletedTask;
        public async Task SyncEftIslemAsync(EftIslem islem) => await Task.CompletedTask;
        public async Task SyncStokSayimFisiAsync(StokSayimFisi fis) => await Task.CompletedTask;
        public async Task SyncStokSayimDetaylarAsync(int fisId, List<StokSayimDetay> detaylar) => await Task.CompletedTask;
        public async Task DeleteCekAsync(int id) => await Task.CompletedTask;
        public async Task DeleteSenetAsync(int id) => await Task.CompletedTask;
        public async Task DeleteKrediKartiIslemAsync(int id) => await Task.CompletedTask;
        public async Task DeleteEftIslemAsync(int id) => await Task.CompletedTask;
        public async Task DeleteStokSayimFisiAsync(int id) => await Task.CompletedTask;

        public async Task SyncGorevAsync(Gorev gorev) => await Task.CompletedTask;
        public async Task DeleteGorevAsync(int id) => await Task.CompletedTask;
        public async Task<List<Gorev>> PullGorevlerAsync() => new();

        public async Task SyncPersonelAsync(Personel personel) => await Task.CompletedTask;
        public async Task DeletePersonelAsync(int id) => await Task.CompletedTask;
        public async Task<List<Personel>> PullPersonellerAsync() => new();

        public async Task SyncSatisHedefiAsync(SatisHedefi hedef) => await Task.CompletedTask;
        public async Task<List<SatisHedefi>> PullSatisHedefleriAsync() => new();

        public async Task SyncHaftalikSatisHedefiAsync(HaftalikSatisHedefi hedef) => await Task.CompletedTask;
        public async Task<List<HaftalikSatisHedefi>> PullHaftalikSatisHedefleriAsync() => new();

        public async Task SyncYillikSatisHedefiAsync(YillikSatisHedefi hedef) => await Task.CompletedTask;
        public async Task<List<YillikSatisHedefi>> PullYillikSatisHedefleriAsync() => new();

        public async Task SyncPortfoyAsync(PortfoyKart portfoy) => await Task.CompletedTask;
        public async Task DeletePortfoyAsync(int id) => await Task.CompletedTask;
        public async Task<List<PortfoyKart>> PullPortfoyAsync() => new();

        public async Task<List<StokSayimFisi>> PullStokSayimlarAsync() => new();
        public async Task<List<StokSayimDetay>> PullStokSayimDetaylarAsync(int fisId) => new();
        public async Task<List<DovizKur>?> PullDovizKurlariAsync() => new();
        public async Task<List<BelgeArsiv>?> PullBelgeArsivAsync() => new();

        public async Task ClearCloudTablesAsync(string tenantId = "default") => await Task.CompletedTask;

        // ==========================================
        // BULK INITIAL PUSH (LOCAL SQLITE -> SUPABASE)
        // ==========================================
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
                if (cariler?.Any() == true) await UpsertBatchAsync("cariler", cariler);
                if (cariHareketler?.Any() == true) await UpsertBatchAsync("cari_hareketler", cariHareketler);
                if (stoklar?.Any() == true) await UpsertBatchAsync("stoklar", stoklar);
                if (stokHareketler?.Any() == true) await UpsertBatchAsync("stok_hareketler", stokHareketler);
                if (faturalar?.Any() == true) await UpsertBatchAsync("faturalar", faturalar);
                if (faturaDetaylar?.Any() == true) await UpsertBatchAsync("fatura_detaylar", faturaDetaylar);
                if (siparisler?.Any() == true) await UpsertBatchAsync("siparisler", siparisler);
                if (siparisDetaylar?.Any() == true) await UpsertBatchAsync("siparis_detaylar", siparisDetaylar);
                if (teklifler?.Any() == true) await UpsertBatchAsync("teklifler", teklifler);
                if (teklifDetaylar?.Any() == true) await UpsertBatchAsync("teklif_detaylar", teklifDetaylar);
                if (bankalar?.Any() == true) await UpsertBatchAsync("bankalar", bankalar);
                if (kasaHareketler?.Any() == true) await UpsertBatchAsync("kasa_hareketler", kasaHareketler);
                if (bankaHareketler?.Any() == true) await UpsertBatchAsync("banka_hareketler", bankaHareketler);
                if (notes?.Any() == true) await UpsertBatchAsync("notlar", notes);
            });
        }
    }
}
