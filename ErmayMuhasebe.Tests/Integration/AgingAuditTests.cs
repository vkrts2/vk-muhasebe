using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ErmayMuhasebe.Tests.Integration
{
    [Collection("DatabaseTests")]
    public class AgingAuditTests : IDisposable
    {
        private readonly DatabaseService _dbService;
        private readonly string _testDbPath;

        public AgingAuditTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"AgingAudit_{Guid.NewGuid()}.db3");
            ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;
            if (!OperatingSystem.IsBrowser()) SQLitePCL.Batteries_V2.Init();
            _dbService = new DatabaseService();
            _dbService.UseEncryption = false;
            _dbService.DisableCloudSync = true;
            _dbService.IsTestMode = true;
            _dbService.InitializeAsync().Wait();
        }

        [Fact]
        public async Task AgingReport_ShouldCorrectlyIdentifyDebtAges()
        {
            // 1. Hazırlık: Müşteri oluştur
            var customer = new CariKart { Unvan = "VADE TEST MÜŞTERİSİ", Tur = "Alici" };
            await _dbService.SaveCariKartAsync(customer);

            // Banka kartı oluştur (BankaId = 1 eşleşmesi ve hareketin kaydedilebilmesi için)
            var banka = new BankaKart { BankaAdi = "Test Bankası", KartTuru = "Vadesiz" };
            await _dbService.SaveBankaKartAsync(banka);

            // 2. İşlem - Adım 1: 30 gün önce 10.000 TL satış
            var date30DaysAgo = DateTime.Now.AddDays(-30);
            var inv1 = new Fatura { CariId = customer.Id, Tur = "Satış", GenelToplam = 10000, Tarih = date30DaysAgo, FaturaNo = "F-001" };
            await _dbService.SaveFaturaWithTransactionAsync(inv1, new List<FaturaDetay>(), customer);

            // 3. İşlem - Adım 2: 25 gün önce 5.000 TL ödeme
            var date25DaysAgo = DateTime.Now.AddDays(-25);
            var pay1 = new BankaHareket { BankaId = 1, CariId = customer.Id, Giren = 5000, IslemTuru = "Tahsilat", Tarih = date25DaysAgo, EvrakNo = "P-001" };
            await _dbService.SaveBankaHareketAsync(pay1);

            // 4. İşlem - Adım 3: 23 gün önce 7.500 TL yeni satış
            var date23DaysAgo = DateTime.Now.AddDays(-23);
            var inv2 = new Fatura { CariId = customer.Id, Tur = "Satış", GenelToplam = 7500, Tarih = date23DaysAgo, FaturaNo = "F-002" };
            await _dbService.SaveFaturaWithTransactionAsync(inv2, new List<FaturaDetay>(), customer);

            // 5. DOĞRULAMA: Vade Takip ve Bakiyeleri Kontrol Et
            await _dbService.RecalculateSystemBalancesAsync();
            var dbCust = await _dbService.GetCariKartAsync(customer.Id);
            
            // Toplam Borç: 10.000 + 7.500 = 17.500
            // Toplam Alacak: 5.000
            // Net Bakiye: 12.500
            Assert.Equal(12500, dbCust.Borc - dbCust.Alacak);

            // RİSK ANALİZİ: En eski vadenin 30 gün öncesi olduğunu göstermeli
            var risky = await _dbService.GetRiskyCarisAsync(10);
            var myRisk = risky.FirstOrDefault(r => r.CariId == customer.Id);
            
            Assert.NotNull(myRisk);
            // SonIslemTarihi en eski ödenmemiş faturayı işaret etmeli (30 gün önce)
            Assert.Equal(date30DaysAgo.Date, myRisk.SonIslemTarihi.Value.Date);
            
            System.Diagnostics.Debug.WriteLine($"[AGING OK] Customer Debt: 12.500. Oldest Unpaid Date: {myRisk.SonIslemTarihi.Value.ToShortDateString()}");
        }

        public void Dispose()
        {
            try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        }
    }
}
