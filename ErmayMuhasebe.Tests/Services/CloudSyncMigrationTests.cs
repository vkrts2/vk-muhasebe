using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using Xunit;
using Xunit.Abstractions;

namespace ErmayMuhasebe.Tests.Services;

public class CloudSyncMigrationTests
{
    private readonly ITestOutputHelper _output;

    public CloudSyncMigrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task MigrateActualDesktopDbToSupabase()
    {
        string actualDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "ErmayV4_Stable.db3");
        _output.WriteLine($"Database exists: {File.Exists(actualDb)} at {actualDb}");
        Assert.True(File.Exists(actualDb), "Database file must exist");

        ErmayMuhasebe.Data.Constants.DatabasePath = actualDb;
        var dbService = new DatabaseService();
        await dbService.InitializeAsync();

        var testCari = await dbService.GetCariKartAsync(9999);
        if (testCari != null) await dbService.DeleteCariKartAsync(testCari);

        var cariler = await dbService.GetCarilerAsync();
        _output.WriteLine($"Loaded {cariler.Count} cariler from SQLite.");

        var stoklar = await dbService.GetStoklarAsync();
        _output.WriteLine($"Loaded {stoklar.Count} stoklar from SQLite.");

        var faturalar = await dbService.GetFaturalarAsync();
        _output.WriteLine($"Loaded {faturalar.Count} faturalar from SQLite.");

        _output.WriteLine($"IsCloudConnected: {dbService.IsCloudConnected}");
        
        await dbService.SyncToCloudAsync();
        _output.WriteLine("SyncToCloudAsync finished successfully!");
    }

    [Fact]
    public async Task TestBidirectionalSync_PullsMobilRecordToDesktop()
    {
        string actualDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ErmayMuhasebe", "ErmayV4_Stable.db3");
        ErmayMuhasebe.Data.Constants.DatabasePath = actualDb;
        var dbService = new DatabaseService();
        await dbService.InitializeAsync();

        var syncService = new CloudSyncService(new YearContext());
        
        // 0. Ensure clean state
        var old = await dbService.GetCariKartAsync(9999);
        if (old != null) await dbService.DeleteCariKartAsync(old);
        await syncService.DeleteAsync("cariler", 9999);
        await syncService.DeleteAsync("cari_hareketler", 99991);

        // 1. Ensure Cari and CariHareket exist in cloud
        await syncService.SyncCariAsync(new CariKart
        {
            Id = 9999,
            CariKod = "MOB-001",
            Unvan = "Mobil Test Müşteri",
            Il = "İstanbul"
        });

        await syncService.SyncCariHareketAsync(new CariHareket
        {
            Id = 99991,
            CariId = 9999,
            IslemTuru = "Nakit Tahsilat",
            EvrakNo = "EVR-9999",
            Borc = 1500m,
            Alacak = 500m,
            Tarih = DateTime.UtcNow
        });

        // 2. Pull from cloud to local SQLite
        await dbService.SyncFromCloudAsync();

        var pulled = await dbService.GetCariKartAsync(9999);
        Assert.NotNull(pulled);
        Assert.Equal("Mobil Test Müşteri", pulled.Unvan);
        Assert.Equal("MOB-001", pulled.CariKod);
        Assert.Equal("İstanbul", pulled.Il);

        var pulledHareketler = await dbService.GetCariHareketlerAsync(9999);
        Assert.NotEmpty(pulledHareketler);
        _output.WriteLine($"Pulled {pulledHareketler.Count} hareketler for cari 9999.");

        // 3. Clean up test records
        await dbService.DeleteCariKartAsync(pulled);
        await syncService.DeleteAsync("cariler", 9999);
        await syncService.DeleteAsync("cari_hareketler", 99991);
        _output.WriteLine("Cleaned up test records successfully.");
    }
}

