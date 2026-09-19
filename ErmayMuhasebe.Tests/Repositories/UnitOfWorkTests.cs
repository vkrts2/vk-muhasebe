using ErmayMuhasebe.Models;
using ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Services;
using System.IO;
using Xunit;

namespace ErmayMuhasebe.Tests.Repositories;

/// <summary>
/// UnitOfWork için unit testler
/// </summary>
[Collection("DatabaseTests")] // Paralel çalışmayı engelle
public class UnitOfWorkTests : IDisposable
{
    private readonly DatabaseService _dbService;
    private readonly UnitOfWork _uow;
    private readonly string _testDbPath;

    public UnitOfWorkTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"TestDb_{Guid.NewGuid()}.db3");
        ErmayMuhasebe.Data.Constants.DatabasePath = _testDbPath;
        
        _dbService = new DatabaseService();
        _dbService.IsTestMode = true;
        _dbService.DisableCloudSync = true;
        _uow = new UnitOfWork(_dbService);
    }

    [Fact]
    public async Task UnitOfWork_ShouldProvideAllRepositories()
    {
        // Arrange
        await _dbService.InitializeAsync();
        
        // Act & Assert
        Assert.NotNull(_uow.Cariler);
        Assert.NotNull(_uow.Stoklar);
        Assert.NotNull(_uow.Faturalar);
        Assert.NotNull(_uow.Bankalar);
        Assert.NotNull(_uow.Kasalar);
    }

    [Fact]
    public async Task UnitOfWork_Cariler_ShouldWork()
    {
        // Arrange
        await _dbService.InitializeAsync();
        var cari = new CariKart
        {
            Unvan = "Test Cari",
            Tur = "Alici"
        };
        
        // Act
        int id = await _uow.Cariler.SaveAsync(cari);
        var result = await _uow.Cariler.GetByIdAsync(id);
        
        // Assert
        Assert.True(id > 0);
        Assert.NotNull(result);
        Assert.Equal("Test Cari", result.Unvan);
    }

    [Fact]
    public async Task UnitOfWork_Stoklar_ShouldWork()
    {
        // Arrange
        await _dbService.InitializeAsync();
        var stok = new StokKart
        {
            StokKodu = "STK001",
            StokAdi = "Test Ürün"
        };
        
        // Act
        int id = await _uow.Stoklar.SaveAsync(stok);
        var result = await _uow.Stoklar.GetByIdAsync(id);
        
        // Assert
        Assert.True(id > 0);
        Assert.NotNull(result);
        Assert.Equal("STK001", result.StokKodu);
    }

    [Fact]
    public async Task UnitOfWork_Faturalar_ShouldWork()
    {
        // Arrange
        await _dbService.InitializeAsync();
        var fatura = new Fatura
        {
            FaturaNo = "FAT001",
            Tur = "Satış"
        };
        
        // Act
        int id = await _uow.Faturalar.SaveAsync(fatura);
        var result = await _uow.Faturalar.GetByIdAsync(id);
        
        // Assert
        Assert.True(id > 0);
        Assert.NotNull(result);
        Assert.Equal("FAT001", result.FaturaNo);
    }

    [Fact]
    public async Task UnitOfWork_MultipleRepositories_ShouldWorkTogether()
    {
        // Arrange
        await _dbService.InitializeAsync();
        
        // Act - Birden fazla repository kullan
        var cari = new CariKart { Unvan = "Test Cari", Tur = "Alici" };
        var stok = new StokKart { StokKodu = "STK001", StokAdi = "Test Ürün" };
        var fatura = new Fatura { FaturaNo = "FAT001", Tur = "Satış", CariId = 1 };
        
        int cariId = await _uow.Cariler.SaveAsync(cari);
        int stokId = await _uow.Stoklar.SaveAsync(stok);
        int faturaId = await _uow.Faturalar.SaveAsync(fatura);
        
        // Assert
        Assert.True(cariId > 0);
        Assert.True(stokId > 0);
        Assert.True(faturaId > 0);
        
        var savedCari = await _uow.Cariler.GetByIdAsync(cariId);
        var savedStok = await _uow.Stoklar.GetByIdAsync(stokId);
        var savedFatura = await _uow.Faturalar.GetByIdAsync(faturaId);
        
        Assert.NotNull(savedCari);
        Assert.NotNull(savedStok);
        Assert.NotNull(savedFatura);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch { }
    }
}

