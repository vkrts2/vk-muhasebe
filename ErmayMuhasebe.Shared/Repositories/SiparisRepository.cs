using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using SQLite;

namespace ErmayMuhasebe.Repositories;

public class SiparisRepository : BaseRepository<Siparis>, ISiparisRepository
{
    public SiparisRepository(DatabaseService dbService) : base(dbService)
    {
    }

    public override async Task<List<Siparis>> GetAllAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Siparis>()
            .Where(s => !s.IsDeleted)
            .OrderByDescending(s => s.Tarih)
            .ToListAsync();
    }

    public override async Task<Siparis?> GetByIdAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<Siparis>()
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted);
    }

    public override async Task<int> SaveAsync(Siparis entity)
    {
        var db = await GetConnectionAsync();

        if (entity.Id != 0)
        {
            await db.UpdateAsync(entity);
        }
        else
        {
            await db.InsertAsync(entity);
        }

        await _syncService.SyncSiparisAsync(entity);
        return entity.Id;
    }

    public override async Task<int> DeleteAsync(Siparis entity)
    {
        entity.IsDeleted = true;
        int res = await SaveAsync(entity);
        await _syncService.DeleteSiparisAsync(entity.Id);
        await _syncService.DeleteSiparisDetaylarAsync(entity.Id);
        return res;
    }

    public override async Task<int> DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity == null) return 0;
        return await DeleteAsync(entity);
    }

    public override async Task<List<Siparis>> GetDeletedAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<Siparis>()
            .Where(s => s.IsDeleted)
            .ToListAsync();
    }

    public override async Task RestoreAsync(Siparis entity)
    {
        entity.IsDeleted = false;
        await SaveAsync(entity);
    }

    public async Task<List<SiparisDetay>> GetDetaylarAsync(int siparisId)
    {
        var db = await GetConnectionAsync();
        var details = await db.Table<SiparisDetay>()
            .Where(d => d.SiparisId == siparisId)
            .ToListAsync();

        if ((details == null || details.Count == 0) && siparisId > 0)
        {
            try
            {
                var siparis = await db.Table<Siparis>().FirstOrDefaultAsync(s => s.Id == siparisId);
                if (siparis != null && !string.IsNullOrWhiteSpace(siparis.SiparisNo))
                {
                    var otherSiparisler = await db.Table<Siparis>()
                        .Where(s => s.SiparisNo == siparis.SiparisNo && s.Id != siparisId)
                        .ToListAsync();

                    foreach (var other in otherSiparisler)
                    {
                        var otherDetails = await db.Table<SiparisDetay>()
                            .Where(d => d.SiparisId == other.Id)
                            .ToListAsync();

                        if (otherDetails != null && otherDetails.Count > 0)
                        {
                            foreach (var od in otherDetails)
                            {
                                od.SiparisId = siparisId;
                                await db.UpdateAsync(od);
                            }
                            return otherDetails;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SiparisRepository] GetDetaylarAsync self-healing error: {ex.Message}");
            }
        }

        return details ?? new List<SiparisDetay>();
    }

    public async Task<List<SiparisDetay>> GetAllDetaylarAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<SiparisDetay>().ToListAsync();
    }

    public async Task<int> SaveWithDetailsAsync(Siparis siparis, List<SiparisDetay> detaylar)
    {
        var db = await GetConnectionAsync();
        
        await db.RunInTransactionAsync(tran =>
        {
            if (siparis.Id != 0)
            {
                tran.Update(siparis);
            }
            else
            {
                tran.Insert(siparis);
            }

            // Clear old details
            tran.Execute("DELETE FROM SiparisDetay WHERE SiparisId = ?", siparis.Id);

            foreach (var d in detaylar)
            {
                d.SiparisId = siparis.Id;
                tran.Insert(d);
            }
        });
        
        await _syncService.SyncSiparisAsync(siparis);
        await _syncService.SyncSiparisDetaylarAsync(siparis.Id, detaylar);
        return siparis.Id;
    }
}
