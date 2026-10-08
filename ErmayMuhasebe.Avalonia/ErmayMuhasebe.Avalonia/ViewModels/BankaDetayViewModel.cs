using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using ErmayMuhasebe.Avalonia.Messages;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System.Threading.Tasks;
using System;
using Avalonia.Threading;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class BankaDetayViewModel : ErmayMuhasebe.Shared.ViewModels.BankaDetayViewModel
{
    [ObservableProperty] private bool _hasHareketler;
    [ObservableProperty] private bool _noHareketler = true;

    public override async Task LoadHareketlerAsync()
    {
        await base.LoadHareketlerAsync();
        HasHareketler = Hareketler.Count > 0;
        NoHareketler = !HasHareketler;
    }

    public event Action? GoBackRequest;
    public Func<Task<string?>>? RequestFilePick { get; set; }

    public BankaDetayViewModel(IUnitOfWork uow, BankaKart banka, IPdfService pdfService) 
        : base(uow, pdfService)
    {
        _ = InitializeAsync(banka);
        WeakReferenceMessenger.Default.Register<FinancialDataChangedMessage>(this, (r, m) => 
        {
            if (!IsTransactionDialogVisible && !IsCariSecimVisible) _ = LoadHareketlerAsync();
        });
    }

    [ObservableProperty] private bool _isCariSecimVisible;
    [ObservableProperty] private ObservableCollection<CariKart> _cariListesi = new();
    [ObservableProperty] private CariKart? _selectedCariForSelection;
    [ObservableProperty] private string _cariSearchText = "";
    
    private bool _isSelectingForTransfer = false; 

    public override async Task SelectCariForTransactionAsync()
    {
         _isSelectingForTransfer = false;
         await LoadCarilerAsync();
         IsCariSecimVisible = true;
    }
    
    public override async Task TransferTransactionAsync()
    {
         if (!IsTransferPossible) return;
         _isSelectingForTransfer = true;
         await LoadCarilerAsync();
         IsCariSecimVisible = true;
    }

    public override async Task UploadDekontForTransactionAsync()
    {
        if (RequestFilePick != null)
        {
             var path = await RequestFilePick.Invoke();
             if (!string.IsNullOrEmpty(path))
             {
                 TransactionDekontPath = path;
             }
        }
    }

    [RelayCommand]
    public async Task ConfirmCariSelection()
    {
        if (SelectedCariForSelection == null) return;
        
        IsCariSecimVisible = false;
        
        if (_isSelectingForTransfer)
        {
             var targetCari = SelectedCariForSelection;
             var source = SelectedHareket;
             
             if (source != null)
             {
                 // Create Outgoing
                 var outgoing = new BankaHareket
                 {
                     BankaId = source.BankaId,
                     BankaAdi = source.BankaAdi,
                     IBAN = source.IBAN,
                     Tarih = DateTime.Now,
                     IslemTuru = "Giden Havale (Aktarım)",
                     Aciklama = $"Aktarım: {source.Aciklama} -> {targetCari.Unvan}",
                     Cikan = source.Giren, 
                     Giren = 0,
                     CariUnvan = targetCari.Unvan,
                     DekontPath = source.DekontPath // Inherit Receipt? Optional.
                 };
                 await _uow.Bankalar.SaveHareketAsync(outgoing);
                 
                 // Update Source
                 source.Durum = "Yönlendirildi";
                 source.YonlendirilenCariId = targetCari.Id;
                 source.YonlendirilenCariUnvan = targetCari.Unvan;
                 source.YonlendirmeTarihi = DateTime.Now;
                 await _uow.Bankalar.SaveHareketAsync(source);
                 
                 await LoadHareketlerAsync();
                 NotifyFinancialDataChanged();
                 SuccessMessage = $"Tutar {targetCari.Unvan} hesabına aktarıldı.";
             }
        }
        else
        {
             TransactionCariUnvan = SelectedCariForSelection.Unvan ?? "";
             TransactionCariId = SelectedCariForSelection.Id;
        }
    }
    
    [RelayCommand]
    public void CancelCariSelection()
    {
        IsCariSecimVisible = false;
    }

    private async Task LoadCarilerAsync()
    {
        var list = await _uow.Cariler.GetAllAsync();
        if (!string.IsNullOrEmpty(CariSearchText))
             list = list.Where(c => (c.Unvan ?? "").Contains(CariSearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        CariListesi = new ObservableCollection<CariKart>(list);
    }
    
    [RelayCommand]
    public void SearchCari() => _ = LoadCarilerAsync();


    protected override void NotifyFinancialDataChanged()
    {
        WeakReferenceMessenger.Default.Send(new FinancialDataChangedMessage());
    }

    protected override async Task InvokeOnUIThreadAsync(Action action)
    {
        await Dispatcher.UIThread.InvokeAsync(action);
    }

    protected override async Task HandleFileOpenAsync(byte[] content, string fileName)
    {
        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ErmayFiles");
        if (!System.IO.Directory.Exists(tempDir)) System.IO.Directory.CreateDirectory(tempDir);
        string path = System.IO.Path.Combine(tempDir, fileName);
        await System.IO.File.WriteAllBytesAsync(path, content);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    public override void GoBack()
    {
        GoBackRequest?.Invoke();
    }
}
