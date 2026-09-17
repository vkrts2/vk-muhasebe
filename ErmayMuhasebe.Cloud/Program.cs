using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ErmayMuhasebe.Cloud;
using MudBlazor.Services;
using ErmayMuhasebe.Cloud.Services;
using Blazored.LocalStorage;
using ErmayMuhasebe.Services;
using ErmayMuhasebe.Repositories;

Console.WriteLine("Ermay Cloud WASM Booting (Minimal Mode)...");
var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<BrowserStorageService>();
builder.Services.AddScoped<ClientAuthService>();
builder.Services.AddScoped<IYearContext, CloudYearContext>();
builder.Services.AddScoped<FirebaseService>();
builder.Services.AddScoped<IFirebaseService>(sp => sp.GetRequiredService<FirebaseService>());
builder.Services.AddScoped<ErmayMuhasebe.Repositories.DataProviders.IDataProvider, ErmayMuhasebe.Repositories.DataProviders.FirebaseDataProvider>();
builder.Services.AddScoped<IUnitOfWork>(sp => (IUnitOfWork)sp.GetRequiredService<ErmayMuhasebe.Repositories.DataProviders.IDataProvider>());
builder.Services.AddScoped<ClientDatabaseService>();
builder.Services.AddScoped<DatabaseService>(sp => sp.GetRequiredService<ClientDatabaseService>());
builder.Services.AddScoped<DovizService>();
builder.Services.AddScoped<CloudPdfService>();
builder.Services.AddScoped<PdfService>(); // Satisfy legacy concrete injections
builder.Services.AddScoped<IPdfService>(sp => sp.GetRequiredService<CloudPdfService>());
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<ErmayMuhasebe.Cloud.Services.IFileService, ErmayMuhasebe.Cloud.Services.ClientFileService>();
builder.Services.AddScoped<UiService>();
builder.Services.AddScoped<ExternalApiService>();
builder.Services.AddScoped<I18nService>();
builder.Services.AddScoped<IFinansService, FinansService>();

// --- VIEWMODELS (Cloud + Shared Mappings) ---
void RegisterViewModel<TShared, TCloud>(IServiceCollection services) 
    where TShared : class 
    where TCloud : class, TShared
{
    services.AddScoped<TCloud>();
    services.AddScoped<TShared>(sp => sp.GetRequiredService<TCloud>());
}

RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.FaturaListViewModel, ErmayMuhasebe.Cloud.ViewModels.FaturaListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.FaturaDetayViewModel, ErmayMuhasebe.Cloud.ViewModels.FaturaDetayViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.CariListViewModel, ErmayMuhasebe.Cloud.ViewModels.CariListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.StokListViewModel, ErmayMuhasebe.Cloud.ViewModels.StokListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.BankaListViewModel, ErmayMuhasebe.Cloud.ViewModels.BankaListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.BankaDetayViewModel, ErmayMuhasebe.Cloud.ViewModels.BankaDetayViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.KasaListViewModel, ErmayMuhasebe.Cloud.ViewModels.KasaListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.KasaDetayViewModel, ErmayMuhasebe.Cloud.ViewModels.KasaDetayViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.KrediKartiListViewModel, ErmayMuhasebe.Cloud.ViewModels.KrediKartiListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.CekSenetListViewModel, ErmayMuhasebe.Cloud.ViewModels.CekSenetListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.EFTListViewModel, ErmayMuhasebe.Cloud.ViewModels.EFTListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.RaporListViewModel, ErmayMuhasebe.Cloud.ViewModels.RaporListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.VadeTakipViewModel, ErmayMuhasebe.Cloud.ViewModels.VadeTakipViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.SettingsViewModel, ErmayMuhasebe.Cloud.ViewModels.SettingsViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.TopluFiyatViewModel, ErmayMuhasebe.Cloud.ViewModels.TopluFiyatViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.SiparisListViewModel, ErmayMuhasebe.Cloud.ViewModels.SiparisListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.TeklifListViewModel, ErmayMuhasebe.Cloud.ViewModels.TeklifListViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.SiparisDetayViewModel, ErmayMuhasebe.Cloud.ViewModels.SiparisDetayViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.TeklifDetayViewModel, ErmayMuhasebe.Cloud.ViewModels.TeklifDetayViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.BarkodTasarimViewModel, ErmayMuhasebe.Cloud.ViewModels.BarkodTasarimViewModel>(builder.Services);
// RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.FaturaTasarimViewModel, ErmayMuhasebe.Cloud.ViewModels.FaturaTasarimViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.CariBirlestirmeViewModel, ErmayMuhasebe.Cloud.ViewModels.CariBirlestirmeViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.StokGrupDuzenleViewModel, ErmayMuhasebe.Cloud.ViewModels.StokGrupDuzenleViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.BorcHatirlaticiViewModel, ErmayMuhasebe.Cloud.ViewModels.BorcHatirlaticiViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.OptimalFiyatViewModel, ErmayMuhasebe.Cloud.ViewModels.OptimalFiyatViewModel>(builder.Services);
RegisterViewModel<ErmayMuhasebe.Shared.ViewModels.EvrakNoDuzenleViewModel, ErmayMuhasebe.Cloud.ViewModels.EvrakNoDuzenleViewModel>(builder.Services);
builder.Services.AddScoped<ErmayMuhasebe.Cloud.ViewModels.KanbanViewModel>();
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.YearSelectionViewModel>();
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.VeriTemizlikViewModel>();
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.SistemSaglikViewModel>();
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.HaftalikHedefTakipViewModel>();
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.DashboardViewModel>();

// --- ADDITIONAL DESKTOP PARITY VIEWMODELS ---
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.FinansViewModel>(); 
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.CekRiskAnalizViewModel>();
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.DovizOtomasyonViewModel>();
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.PortfoyListViewModel>();
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.MusteriLimitViewModel>();
builder.Services.AddScoped<ErmayMuhasebe.Shared.ViewModels.ButcePlanlamaViewModel>(); 

var host = builder.Build();
var i18nService = host.Services.GetRequiredService<I18nService>();
await i18nService.InitializeAsync();

Console.WriteLine("Core App Ready.");
await host.RunAsync();
