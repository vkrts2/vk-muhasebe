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
    public class BusinessFlowTests : IDisposable
    {
        private readonly DatabaseService _dbService;
        private readonly string _testDbPath;
        private readonly PdfService _pdfService;
        private readonly ExcelService _excelService;

        public BusinessFlowTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"FlowTestDb_{Guid.NewGuid()}.db3");
            ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;
            // Test ortamında SQLite kütüphanesini başlat
            if (!OperatingSystem.IsBrowser())
            {
                SQLitePCL.Batteries_V2.Init();
            }
            _dbService = new DatabaseService();
            _dbService.UseEncryption = false;
            _dbService.DisableCloudSync = true;
            _dbService.IsTestMode = true;
            _pdfService = new PdfService();
            _excelService = new ExcelService();
        }

        [Fact]
        public async Task FullBusinessFlow_UserScenario_ShouldSucceed()
        {
            // 1. ADIM: Veritabanı Hazırlığı
            await _dbService.InitializeAsync();

            // 2. ADIM: Cari Hesap Oluştur (1 Tedarikçi, 1 Müşteri)
            var tedarikci = new CariKart { Unvan = "ERMAY TEDARİK A.Ş.", Tur = "Satici", CariKod = "T-001" };
            var musteri = new CariKart { Unvan = "ERMAY MÜŞTERİ LTD. ŞTİ.", Tur = "Alici", CariKod = "M-001" };
            
            await _dbService.SaveCariKartAsync(tedarikci);
            await _dbService.SaveCariKartAsync(musteri);

            Assert.True(tedarikci.Id > 0);
            Assert.True(musteri.Id > 0);

            // 3. ADIM: Stok Kartı Oluştur
            var stok = new StokKart { StokAdi = "TEST ÜRÜN 101", Birim = "Adet", KDV = 20, AlisFiyati = 100, SatisFiyati = 200 };
            await _dbService.SaveStokKartAsync(stok);
            Assert.True(stok.Id > 0);

            // 4. ADIM: Tedarikçiden Alış Faturası Kaydet
            var alisFatura = new Fatura 
            { 
                CariId = tedarikci.Id, 
                CariUnvan = tedarikci.Unvan, 
                Tur = "Alış", 
                FaturaNo = "AF-2024-001",
                Tarih = DateTime.Now,
                GenelToplam = 1200 // 10 adet * 100 TL + %20 KDV
            };
            var alisDetaylar = new List<FaturaDetay> 
            { 
                new FaturaDetay { StokId = stok.Id, StokAdi = stok.StokAdi, Miktar = 10, BirimFiyat = 100, KDVOrani = 20, ToplamTutar = 1200 } 
            };
            await _dbService.SaveFaturaWithTransactionAsync(alisFatura, alisDetaylar, tedarikci);

            // Kontrol: Stok miktarı 10 olmalı, tedarikçi 1200 TL alacaklı olmalı
            var checkStokAfterAlis = await _dbService.GetStokKartAsync(stok.Id);
            var checkTedarikci = await _dbService.GetCariKartAsync(tedarikci.Id);
            Assert.Equal(10, checkStokAfterAlis.Miktar);
            Assert.Equal(1200, checkTedarikci.Alacak);

            // 5. ADIM: Müşteriye Satış Faturası Kaydet
            var satisFatura = new Fatura 
            { 
                CariId = musteri.Id, 
                CariUnvan = musteri.Unvan, 
                Tur = "Satış", 
                FaturaNo = "SF-2024-001",
                Tarih = DateTime.Now,
                GenelToplam = 480 // 2 adet * 200 TL + %20 KDV
            };
            var satisDetaylar = new List<FaturaDetay> 
            { 
                new FaturaDetay { StokId = stok.Id, StokAdi = stok.StokAdi, Miktar = 2, BirimFiyat = 200, KDVOrani = 20, ToplamTutar = 480 } 
            };
            await _dbService.SaveFaturaWithTransactionAsync(satisFatura, satisDetaylar, musteri);

            // Kontrol: Stok miktarı 8 olmalı (10-2), müşteri 480 TL borçlu olmalı
            var checkStokAfterSatis = await _dbService.GetStokKartAsync(stok.Id);
            var checkMusteri = await _dbService.GetCariKartAsync(musteri.Id);
            Assert.Equal(8, checkStokAfterSatis.Miktar);
            Assert.Equal(480, checkMusteri.Borc);

            // 6. ADIM: PDF ve Excel Oluşturma Testi (Dosya açma simülasyonu)
            var pdfBytes = await _pdfService.GenerateFaturaPdfBytesAsync(satisFatura, satisDetaylar);
            Assert.NotEmpty(pdfBytes);

            var excelBytes = await _excelService.ExportStyledListToMemoryAsync(satisDetaylar, "Satış Faturası Detayları");
            Assert.NotEmpty(excelBytes);

            // 7. ADIM: Teklif -> Sipariş -> Fatura Döngüsü
            // Tedarikçiye (veya müşteriye) teklif oluştur
            var teklif = new Teklif { CariId = musteri.Id, CariUnvan = musteri.Unvan, TeklifNo = "TKL-001", Tarih = DateTime.Now, GenelToplam = 1000 };
            var teklifDetaylar = new List<TeklifDetay> { new TeklifDetay { StokId = stok.Id, StokAdi = stok.StokAdi, Miktar = 5, BirimFiyat = 200, KdvOrani = 20, Tutar = 1000 } };
            await _dbService.SaveTeklifWithDetailsAsync(teklif, teklifDetaylar);
            
            // Teklifi Siparişe Dönüştür
            int siparisId = await _dbService.ConvertTeklifToSiparisAsync(teklif.Id);
            Assert.True(siparisId > 0);
            
            var checkSiparis = await _dbService.GetSiparisAsync(siparisId);
            Assert.Equal("Bekliyor", checkSiparis.Durum);
            Assert.Contains("Tekliften Dönüştürüldü", checkSiparis.Aciklama);

            // Siparişi Faturaya Dönüştür
            int finalFaturaId = await _dbService.ConvertSiparisToFaturaAsync(siparisId);
            Assert.True(finalFaturaId > 0);

            var checkFinalFatura = await _dbService.GetFaturaAsync(finalFaturaId);
            Assert.Equal("Satış", checkFinalFatura.Tur);
            Assert.Equal(musteri.Id, checkFinalFatura.CariId);

            // Stok ve Cari son durum kontrolü
            var checkStokFinal = await _dbService.GetStokKartAsync(stok.Id);
            var checkMusteriFinal = await _dbService.GetCariKartAsync(musteri.Id);
            
            // Başlangıç 10, Satış 2, Dönüşüm Satış 5 => Kalan 3
            Assert.Equal(3, checkStokFinal.Miktar);
            // İlk satış 480 + İkinci satış (tekliften gelen) 1000 + KDV? 
            // Faturada GenelToplam 1000 olarak set edilmişti (KDV dahil varsayılıyor veya Teklif toplamı neyse o)
            Assert.Equal(1480, checkMusteriFinal.Borc);
        }

        public void Dispose()
        {
            try { if (File.Exists(_testDbPath)) File.Delete(_testDbPath); } catch { }
        }
    }
}
