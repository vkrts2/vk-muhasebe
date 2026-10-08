using CommunityToolkit.Mvvm.Input;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;
using System.Threading.Tasks;
using System;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Messaging;
using ErmayMuhasebe.Avalonia.Messages;
using Avalonia.Threading;
using System.Linq;

namespace ErmayMuhasebe.Avalonia.ViewModels;

public partial class StokListViewModel : ErmayMuhasebe.Shared.ViewModels.StokListViewModel, IHandleBack
{
    public StokListViewModel(IUnitOfWork uow, IExcelService excelService, IPdfService pdfService, IFileService fileService) 
        : base(uow, excelService, pdfService, fileService)
    {
        WeakReferenceMessenger.Default.Register<FinancialDataChangedMessage>(this, (r, m) => 
        {
            if (m.Sender == this) return;
            if (IsTransactionWindowVisible || IsAddGroupVisible || IsEditingTransaction) return;
            _ = LoadStoklarAsync(isSilent: true);
        });
    }

    protected override async Task InvokeOnUIThreadAsync(Action action)
    {
        await Dispatcher.UIThread.InvokeAsync(action);
    }

    protected override async Task HandleFileOpenAsync(byte[] content, string fileName)
    {
        if (content == null || content.Length == 0 || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        try 
        {
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ErmayFiles");
            if (!System.IO.Directory.Exists(tempDir)) System.IO.Directory.CreateDirectory(tempDir);
            
            // Add timestamp to prevent locking if same name exists
            string safeName = System.IO.Path.GetFileNameWithoutExtension(fileName) + "_" + DateTime.Now.ToString("HHmmss") + System.IO.Path.GetExtension(fileName);
            string path = System.IO.Path.Combine(tempDir, safeName);
            
            await System.IO.File.WriteAllBytesAsync(path, content);

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Fallback: Open in Windows Explorer if no application is registered for the extension
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"File Open Error: {ex.Message}");
            ErrorMessage = $"Dosya açma hatası: {ex.Message}";
        }
    }



    public bool HandleBack()
    {
        if (IsAddGroupVisible) { IsAddGroupVisible = false; return true; }
        if (IsTransactionWindowVisible) { IsTransactionWindowVisible = false; return true; }
        if (SelectedStok != null) { SelectedStok = null; return true; }
        return false;
    }
}
