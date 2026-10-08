using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Repositories;
using ErmayMuhasebe.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ErmayMuhasebe.Helpers;

namespace ErmayMuhasebe.Shared.ViewModels;

public abstract partial class BankaListViewModel : ViewModelBase
{
    protected readonly IUnitOfWork _uow;
    protected readonly IPdfService _pdfService;

    [ObservableProperty] private bool _isEditDialogVisible;
    [ObservableProperty] private string _dialogTitle = "Yeni Banka Hesabı";
    
    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _editBankaAdi = "";
    [ObservableProperty] private string _editSubeAdi = "";
    [ObservableProperty] private string _editHesapNo = "";
    [ObservableProperty] private string _editIban = "";
    [ObservableProperty] private string _editDovizTuru = "TL";
    [ObservableProperty] private string _editYetkili = "";
    [ObservableProperty] private decimal _editAcilisBakiyesi;

    [ObservableProperty]
    private ObservableCollection<BankaKart> _bankalar = new();

    [ObservableProperty]
    private BankaKart? _selectedBanka;

    public BankaListViewModel(IUnitOfWork uow, IPdfService pdfService)
    {
        _uow = uow;
        _pdfService = pdfService;
        
        _ = LoadBankalarAsync();
    }

    [RelayCommand]
    public async Task LoadBankalarAsync()
    {
        try 
        {
            IsLoading = true;
            
            // Bakiyeleri doğrula (mismatch'i gidermek için)
            await _uow.RecalculateSystemBalancesAsync();

            var list = await _uow.Bankalar.GetAllAsync();
            var filtered = list.Where(x => x.KartTuru != "Kasa").ToList();
            
            foreach (var b in filtered)
            {
                b.DovizTuru = GetCleanDoviz(b.DovizTuru);
            }

            await InvokeOnUIThreadAsync(() => 
            {
                var selectedId = SelectedBanka?.Id;
                Bankalar.SyncWith(filtered, b => b.Id, (target, src) =>
                {
                    if (target.BankaAdi != src.BankaAdi) target.BankaAdi = src.BankaAdi;
                    if (target.SubeKodu != src.SubeKodu) target.SubeKodu = src.SubeKodu;
                    if (target.HesapNo != src.HesapNo) target.HesapNo = src.HesapNo;
                    if (target.IBAN != src.IBAN) target.IBAN = src.IBAN;
                    if (target.Yetkili != src.Yetkili) target.Yetkili = src.Yetkili;
                    if (target.DovizTuru != src.DovizTuru) target.DovizTuru = src.DovizTuru;
                    if (target.GuncelBakiye != src.GuncelBakiye) target.GuncelBakiye = src.GuncelBakiye;
                    if (target.Bakiye != src.Bakiye) target.Bakiye = src.Bakiye;
                    if (target.AcilisBakiyesi != src.AcilisBakiyesi) target.AcilisBakiyesi = src.AcilisBakiyesi;
                });

                if (selectedId.HasValue)
                {
                    var reselected = Bankalar.FirstOrDefault(b => b.Id == selectedId.Value);
                    if (reselected != null && SelectedBanka != reselected)
                    {
                        SelectedBanka = reselected;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Banka listesi yükleme hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected string GetCleanDoviz(string? val)
    {
        if (string.IsNullOrEmpty(val)) return "TL";
        string s = val.ToString();
        if (s.Contains(":")) s = s.Split(':').Last();
        if (s.Contains(".")) s = s.Split('.').Last();
        return s.Trim();
    }

    public override void OnNavigatedTo() => _ = LoadBankalarAsync();

    [RelayCommand]
    public void OpenAddBankaDialog()
    {
        EditId = 0;
        EditBankaAdi = "";
        EditSubeAdi = "";
        EditHesapNo = "";
        EditIban = "";
        EditDovizTuru = "TL";
        EditYetkili = "";
        EditAcilisBakiyesi = 0;
        DialogTitle = "Yeni Banka Hesabı";
        IsEditDialogVisible = true;
    }

    [RelayCommand]
    public void OpenEditBankaDialog(BankaKart? banka)
    {
        if (banka == null) banka = SelectedBanka;
        if (banka == null) return;
        
        EditId = banka.Id;
        EditBankaAdi = banka.BankaAdi ?? "";
        EditSubeAdi = banka.SubeAdi ?? "";
        EditHesapNo = banka.HesapNo ?? "";
        EditIban = banka.IBAN ?? "";
        EditDovizTuru = banka.DovizTuru ?? "TL";
        EditYetkili = banka.Yetkili ?? "";
        EditAcilisBakiyesi = banka.AcilisBakiyesi;
        DialogTitle = "Banka Hesabı Düzenle";
        IsEditDialogVisible = true;
    }

    [RelayCommand]
    public async Task SaveBankaAsync()
    {
        if (string.IsNullOrWhiteSpace(EditBankaAdi)) return;

        IsLoading = true;
        try
        {
            var banka = new BankaKart
            {
                Id = EditId,
                BankaAdi = EditBankaAdi,
                SubeAdi = EditSubeAdi,
                HesapNo = EditHesapNo,
                IBAN = EditIban,
                DovizTuru = GetCleanDoviz(EditDovizTuru),
                Yetkili = EditYetkili,
                AcilisBakiyesi = EditAcilisBakiyesi,
                GuncelBakiye = EditAcilisBakiyesi, // Simplified. Real bakiye is sum of movements.
                KartTuru = "Vadesiz"
            };

            await _uow.Bankalar.SaveAsync(banka);
            IsEditDialogVisible = false;
            await LoadBankalarAsync();
            NotifyFinancialDataChanged();
            SuccessMessage = "Banka hesabı kaydedildi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kaydetme hatası: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected abstract void NotifyFinancialDataChanged();

    [RelayCommand]
    public void CloseDialog() => IsEditDialogVisible = false;

    [RelayCommand]
    public virtual void NavigateToDetay(BankaKart? banka) { }

    [RelayCommand]
    public void DeleteBankaConfirm(BankaKart? banka)
    {
        if (banka == null) banka = SelectedBanka;
        if (banka == null) return;
        ShowConfirm("Banka Hesabını Sil", $"{banka.BankaAdi} adlı hesabı silmek istediğinize emin misiniz?", () => DeleteBankaAsync(banka));
    }

    public async Task DeleteBankaAsync(BankaKart? banka)
    {
        try 
        {
            await _uow.Bankalar.DeleteAsync(banka!.Id);
            await LoadBankalarAsync();
            NotifyFinancialDataChanged();
            SuccessMessage = "Banka hesabı silindi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Silme hatası: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task GenerateReportAsync(BankaKart? banka)
    {
        if (banka == null) banka = SelectedBanka;
        if (banka == null) return;

        try
        {
            var hareketler = await _uow.Bankalar.GetHareketlerAsync(banka.Id);
            var pdfBytes = await _pdfService.GenerateKasaEkstrePdfBytesAsync(banka, hareketler.Select(h => new KasaHareket {
                Tarih = h.Tarih,
                IslemTuru = h.IslemTuru,
                Aciklama = h.Aciklama,
                Giren = h.Giren,
                Cikan = h.Cikan
            }).ToList());

            await HandleFileOpenAsync(pdfBytes, $"BankaEkstre_{banka.BankaAdi}_{DateTime.Now:ddMMyyyy}.pdf");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Rapor hatası: {ex.Message}";
        }
    }

    protected abstract Task HandleFileOpenAsync(byte[] content, string fileName);

    protected override Task InvokeOnUIThreadAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
