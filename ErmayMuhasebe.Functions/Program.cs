using Microsoft.AspNetCore.Mvc;
using ErmayMuhasebe.Models;
using ErmayMuhasebe.Services;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Set default culture to tr-TR for currency and date formats
var culture = new CultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Add services
builder.Services.AddScoped<PdfService>();
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.MaxDepth = 256;
    options.SerializerOptions.Converters.Add(new ByteArrayBase64Converter());
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => {
    options.SerializerOptions.MaxDepth = 256;
    options.SerializerOptions.Converters.Add(new ByteArrayBase64Converter());
});

// Explicitly set max request body size for Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
});

// 1. CORS Service Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 2. ULTRA-PERMISSIVE CORS MIDDLEWARE (Must be FIRST)
app.Use(async (context, next) =>
{
    // Clear existing to avoid duplicates if any
    context.Response.Headers["Access-Control-Allow-Origin"] = "*";
    context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
    context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Authorization, Accept, X-Requested-With, X-API-Key";
    context.Response.Headers["Access-Control-Max-Age"] = "86400"; // Cache preflight for 24h

    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        await context.Response.CompleteAsync();
        return;
    }

    // Optional API Key check if PDF_API_KEY environment variable is configured
    var requiredApiKey = Environment.GetEnvironmentVariable("PDF_API_KEY");
    if (!string.IsNullOrEmpty(requiredApiKey) && context.Request.Path.StartsWithSegments("/generate"))
    {
        var providedKey = context.Request.Headers["X-API-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(providedKey) && context.Request.Headers.ContainsKey("Authorization"))
        {
            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                providedKey = authHeader.Substring(7).Trim();
        }

        if (string.IsNullOrEmpty(providedKey) || !string.Equals(providedKey, requiredApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Unauthorized: Invalid or missing API Key" });
            return;
        }
    }

    Console.WriteLine($"[API-LOG] Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

// 3. Standard Routing & CORS
app.UseRouting();
app.UseCors("AllowAll");

// Set QuestPDF License
try {
    QuestPDF.Settings.License = LicenseType.Community;
    Console.WriteLine("[SYSTEM] QuestPDF Lisansı aktif edildi.");
} catch (Exception ex) {
     Console.WriteLine("[SYSTEM] Lisans Hatası: " + ex.Message);
}

bool anyFontLoaded = false;
string baseDir = AppDomain.CurrentDomain.BaseDirectory;

try {
    Console.WriteLine($"[FONT] Base Directory: {baseDir}");
    string[] fontPaths = new[] {
        Path.Combine(baseDir, "wwwroot", "css", "fonts"),
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "css", "fonts"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "ErmayMuhasebe.Shared", "wwwroot", "css", "fonts"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "ErmayMuhasebe.Cloud", "wwwroot", "css", "fonts")
    };

    foreach(var fontDir in fontPaths)
    {
        if (!Directory.Exists(fontDir)) continue;
        string reg = Path.Combine(fontDir, "Roboto-Regular.ttf");
        string bold = Path.Combine(fontDir, "Roboto-Bold.ttf");
        if (File.Exists(reg))
        {
            try {
                using var stream = File.OpenRead(reg);
                QuestPDF.Drawing.FontManager.RegisterFont(stream);
                Console.WriteLine($"[FONT] Roboto-Regular yüklendi: {reg}");
                anyFontLoaded = true;
            } catch (Exception ex) {
                Console.WriteLine($"[FONT] HATA (Regular): {ex.Message}");
            }
        }
        if (File.Exists(bold))
        {
            try {
                using var stream = File.OpenRead(bold);
                QuestPDF.Drawing.FontManager.RegisterFont(stream);
                Console.WriteLine($"[FONT] Roboto-Bold yüklendi: {bold}");
            } catch (Exception ex) {
                Console.WriteLine($"[FONT] HATA (Bold): {ex.Message}");
            }
        }
        if (anyFontLoaded) break;
    }
} catch (Exception fontGlobalEx) {
    Console.WriteLine("[FONT] Kritik Font Hatası: " + fontGlobalEx.Message);
}

if (anyFontLoaded)
{
    var pdfService = app.Services.GetRequiredService<PdfService>();
    pdfService.DefaultFontFamily = "Roboto";
    Console.WriteLine("[FONT] Varsayılan font: Roboto");
}

app.MapGet("/", () => "VK Ön Muhasebe PDF API V1.2 - Status: OK");
app.MapGet("/health", () => Results.Ok(new { 
    Status = "Healthy", 
    Version = "1.2", 
    Port = 5244,
    FontsLoaded = anyFontLoaded,
    BaseDir = baseDir
}));

// Print Startup info
Console.WriteLine("\n" + new string('=', 40));
Console.WriteLine("VK ON MUHASEBE PDF API V1.2 CALISIYOR");
Console.WriteLine("----------------------------------------");
Console.WriteLine("Tarayici Erisimi: http://localhost:5244");
Console.WriteLine("Ag Erisimi: http://0.0.0.0:5244");
Console.WriteLine(new string('=', 40) + "\n");

app.MapPost("/generate/fatura", (PdfService pdfService, [FromBody] FaturaRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoFatura = request.ShowLogo;
        if (!string.IsNullOrEmpty(request.Size)) pdfService.FaturaSize = request.Size;
        if (!string.IsNullOrEmpty(request.Orientation)) pdfService.FaturaOrientation = request.Orientation;
        pdfService.AktifFaturaTasarimi = request.Tasarim;
        var pdfBytes = pdfService.GenerateFaturaPdf(request.Fatura, request.Detaylar);
        return Results.File(pdfBytes, "application/pdf", $"Fatura_{request.Fatura?.FaturaNo}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA: {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/teklif", (PdfService pdfService, [FromBody] TeklifRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoTeklif = request.ShowLogo;
        if (!string.IsNullOrEmpty(request.Size)) pdfService.TeklifSize = request.Size;
        if (!string.IsNullOrEmpty(request.Orientation)) pdfService.TeklifOrientation = request.Orientation;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Teklif PDF isteği alındı: {request.Teklif.TeklifNo}");
        var pdfBytes = pdfService.GenerateTeklifPdf(request.Teklif, request.Detaylar, request.Cari);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Teklif PDF başarıyla üretildi: {pdfBytes.Length} byte");
        return Results.File(pdfBytes, "application/pdf", $"Teklif_{request.Teklif.TeklifNo}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Teklif PDF Hatası: {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/siparis", (PdfService pdfService, [FromBody] SiparisRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoSiparis = request.ShowLogo;
        if (!string.IsNullOrEmpty(request.Size)) pdfService.SiparisSize = request.Size;
        if (!string.IsNullOrEmpty(request.Orientation)) pdfService.SiparisOrientation = request.Orientation;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sipariş PDF isteği alındı: {request.Siparis.SiparisNo}");
        var pdfBytes = pdfService.GenerateSiparisPdf(request.Siparis, request.Detaylar, request.Cari);
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sipariş PDF başarıyla üretildi: {pdfBytes.Length} byte");
        return Results.File(pdfBytes, "application/pdf", $"Siparis_{request.Siparis.SiparisNo}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Sipariş PDF Hatası: {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/generic", (PdfService pdfService, [FromBody] GenericTableRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        var pdfBytes = pdfService.GenerateGenericTablePdf(request.Title, request.Headers, request.Rows, request.Subtitle);
        return Results.File(pdfBytes, "application/pdf", $"{request.Title.Replace(" ", "_")}.pdf");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/consolidated", (PdfService pdfService, [FromBody] ConsolidatedReportRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"Konsolide PDF Üretiliyor: {request.Title} ({request.Sections?.Count ?? 0} bölüm)");
        var sections = request.Sections ?? new List<ReportSection>();
        var pdfBytes = pdfService.GenerateConsolidatedReportPdf(request.Title, sections);
        Console.WriteLine("Konsolide PDF Başarıyla Üretildi.");
        return Results.File(pdfBytes, "application/pdf", $"{request.Title.Replace(" ", "_")}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Konsolide PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/budget", (PdfService pdfService, [FromBody] BudgetReportRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        var annual = request.Annual.Select(x => new ChartDataItem { Label = x.Label, Target = x.Target, Actual = x.Actual }).ToList();
        var monthly = request.Monthly.Select(x => new ChartDataItem { Label = x.Label, Target = x.Target, Actual = x.Actual }).ToList();
        var weekly = request.Weekly.Select(x => new ChartDataItem { Label = x.Label, Target = x.Target, Actual = x.Actual }).ToList();

        var pdfBytes = pdfService.GenerateBudgetReportPdf(request.Title, annual, monthly, weekly);
        return Results.File(pdfBytes, "application/pdf", "Butce_Raporu.pdf");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/makbuz", (PdfService pdfService, [FromBody] MakbuzRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoTahsilat = request.ShowLogo;
        pdfService.ShowLogoOdeme = request.ShowLogo;
        pdfService.ShowLogoAcilisBakiye = request.ShowLogo;
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        var pdfBytes = pdfService.GenerateMakbuzPdf(request.MakbuzTipi, request.CariUnvan, request.Tarih, request.Tutar, request.Aciklama);
        return Results.File(pdfBytes, "application/pdf", "Makbuz.pdf");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/makbuz-from-kasa", (PdfService pdfService, [FromBody] KasaMakbuzRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoTahsilat = request.ShowLogo;
        pdfService.ShowLogoOdeme = request.ShowLogo;
        pdfService.ShowLogoAcilisBakiye = request.ShowLogo;
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        var pdfBytes = pdfService.GenerateMakbuzFromKasaPdf(request.Hareket);
        return Results.File(pdfBytes, "application/pdf", "KasaMakbuz.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Kasa Makbuz PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/eft", (PdfService pdfService, [FromBody] EftRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoTahsilat = request.ShowLogo;
        var pdfBytes = pdfService.GenerateEftSlipPdf(request.Islem);
        return Results.File(pdfBytes, "application/pdf", "EftSlip.pdf");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/kk", (PdfService pdfService, [FromBody] KkRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoTahsilat = request.ShowLogo;
        var pdfBytes = pdfService.GenerateKrediKartiSlipPdf(request.Islem);
        return Results.File(pdfBytes, "application/pdf", "KkSlip.pdf");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/ekstre", (PdfService pdfService, [FromBody] EkstreRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoEkstre = request.ShowLogo;
        var pdfBytes = pdfService.GenerateCariEkstrePdf(request.Cari, request.Hareketler);
        return Results.File(pdfBytes, "application/pdf", $"Ekstre_{request.Cari.Unvan}_{DateTime.Now:ddMMyyyy}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Ekstre): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/ekstre-detayli", (PdfService pdfService, [FromBody] DetayliEkstreRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoEkstre = request.ShowLogo;
        var pdfBytes = pdfService.GenerateDetayliCariEkstrePdf(request.Cari, request.Hareketler, request.DetayDictionary);
        return Results.File(pdfBytes, "application/pdf", $"DetayliEkstre_{request.Cari.Unvan}_{DateTime.Now:ddMMyyyy}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Detaylı Ekstre): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/fatura-batch", (PdfService pdfService, [FromBody] List<FaturaRequest> requests) =>
{
    try
    {
        if (requests == null || !requests.Any()) return Results.BadRequest("Hata: Fatura listesi boş.");
        
        var first = requests.First();
        pdfService.LogoBytes = (first.LogoBytes != null && first.LogoBytes.Length > 0) ? first.LogoBytes : new byte[0];
        pdfService.ShowLogoFatura = first.ShowLogo;
        
        var pdfBytes = pdfService.GenerateFaturaBatchPdf(requests.Select(r => (r.Fatura, r.Detaylar)).ToList());
        return Results.File(pdfBytes, "application/pdf", $"Toplu_Faturalar_{DateTime.Now:yyyyMMdd}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Toplu Fatura): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/stok-list", (PdfService pdfService, [FromBody] StokListReportRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Stok Listesi PDF Üretiliyor ({request.Stoklar?.Count ?? 0} stok)");
        var pdfBytes = pdfService.GenerateStokListPdf(request.Stoklar ?? new List<StokKart>());
        return Results.File(pdfBytes, "application/pdf", $"TopluStokRaporu_{DateTime.Now:ddMMyyyy}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Stok Listesi PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/stok-hareket", (PdfService pdfService, [FromBody] StokHareketReportRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Stok Hareket PDF Üretiliyor: {request.Stok?.StokKodu}");
        var pdfBytes = pdfService.GenerateStokHareketleriPdf(request.Stok ?? new StokKart(), request.Hareketler ?? new List<StokHareket>());
        return Results.File(pdfBytes, "application/pdf", $"StokHareket_{request.Stok?.StokKodu ?? "rapor"}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Stok Hareket PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/kasa-ekstre", (PdfService pdfService, [FromBody] KasaEkstreRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Kasa Ekstre PDF Üretiliyor: {request.Kasa?.BankaAdi}");
        var pdfBytes = pdfService.GenerateKasaEkstrePdf(request.Kasa ?? new BankaKart(), request.Hareketler ?? new List<KasaHareket>());
        return Results.File(pdfBytes, "application/pdf", $"KasaEkstresi_{request.Kasa?.BankaAdi ?? "rapor"}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Kasa Ekstresi PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/cek", (PdfService pdfService, [FromBody] CekRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Çek PDF Üretiliyor: {request.Cek?.PortfoyNo}");
        var pdfBytes = pdfService.GenerateCekPdf(request.Cek ?? new Cek());
        return Results.File(pdfBytes, "application/pdf", $"Cek_{request.Cek?.PortfoyNo ?? "form"}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Çek PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/cek-list", (PdfService pdfService, [FromBody] CekListRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Çek Listesi PDF Üretiliyor");
        var pdfBytes = pdfService.GenerateCekListPdf(request.Cekler ?? new List<Cek>());
        return Results.File(pdfBytes, "application/pdf", $"CekListesi_{DateTime.Now:ddMMyyyy}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Çek Listesi PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/kk-list", (PdfService pdfService, [FromBody] KrediKartiListRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] KK Listesi PDF Üretiliyor");
        var pdfBytes = pdfService.GenerateKrediKartiListPdf(request.Islemler ?? new List<KrediKartiIslem>());
        return Results.File(pdfBytes, "application/pdf", $"KrediKartiIslemleri_{DateTime.Now:ddMMyyyy}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (KK Listesi PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/eft-list", (PdfService pdfService, [FromBody] EftListRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] EFT Listesi PDF Üretiliyor");
        var pdfBytes = pdfService.GenerateEftListPdf(request.Islemler ?? new List<EftIslem>());
        return Results.File(pdfBytes, "application/pdf", $"EftIslemleri_{DateTime.Now:ddMMyyyy}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (EFT Listesi PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/kasa-list", (PdfService pdfService, [FromBody] KasaListRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Kasa Listesi PDF Üretiliyor");
        var pdfBytes = pdfService.GenerateKasaListesiPdf(request.Kasalar ?? new List<BankaKart>());
        return Results.File(pdfBytes, "application/pdf", $"KasaListesi_{DateTime.Now:ddMMyyyy}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Kasa Listesi PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/fatura-list", (PdfService pdfService, [FromBody] FaturaListRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Fatura Listesi PDF Üretiliyor ({request.Faturalar?.Count ?? 0} fatura)");
        var pdfBytes = pdfService.GenerateFaturaListPdf(request.Faturalar ?? new List<Fatura>(), request.StartDate, request.EndDate);
        return Results.File(pdfBytes, "application/pdf", $"FaturaListesi_{DateTime.Now:ddMMyyyy}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Fatura Listesi PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/vade-rapor", (PdfService pdfService, [FromBody] VadeRaporuRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Vade Raporu PDF Üretiliyor ({request.Items?.Count ?? 0} kalem)");
        var pdfBytes = pdfService.GenerateVadeRaporuPdf(request.Items ?? new List<VadeReportItem>());
        return Results.File(pdfBytes, "application/pdf", $"VadeRaporu_{DateTime.Now:ddMMyyyy}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Vade Raporu PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/adres-etiketi", (PdfService pdfService, [FromBody] AdresEtiketiRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        var pdfBytes = pdfService.GenerateAdresEtiketiPdf(request.Unvan, request.AdSoyad, request.Adres, request.IlIlce, request.PostaKodu, request.Telefon);
        return Results.File(pdfBytes, "application/pdf", "AdresEtiketi.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Adres Etiketi PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

app.MapPost("/generate/finans-rapor", (PdfService pdfService, [FromBody] FinansRaporRequest request) =>
{
    try
    {
        pdfService.LogoBytes = (request.LogoBytes != null && request.LogoBytes.Length > 0) ? request.LogoBytes : new byte[0];
        pdfService.ShowLogoRaporlar = request.ShowLogo;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Finans Raporu PDF Üretiliyor: {request.Title}");
        var pdfBytes = pdfService.GenerateFinansRaporPdf(request.Title, request.Data);
        return Results.File(pdfBytes, "application/pdf", $"{request.Title.Replace(" ", "_")}_{DateTime.Now:ddMMyyyy}.pdf");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"HATA (Finans Raporu PDF): {ex.Message}");
        return Results.Problem(ex.Message);
    }
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "5244";
app.Run($"http://0.0.0.0:{port}");

// Custom converter to handle both clean Base64, data-prefixed Base64 (data:image/...;base64,), and byte arrays
public class ByteArrayBase64Converter : System.Text.Json.Serialization.JsonConverter<byte[]>
{
    public override byte[]? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Null)
            return null;

        if (reader.TokenType == System.Text.Json.JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.IsNullOrWhiteSpace(str))
                return Array.Empty<byte>();

            var clean = str.Trim();
            if (clean.Contains("base64,"))
            {
                clean = clean.Substring(clean.IndexOf("base64,", StringComparison.OrdinalIgnoreCase) + 7);
            }
            clean = clean.Replace(" ", "").Replace("\r", "").Replace("\n", "");

            try
            {
                int mod4 = clean.Length % 4;
                if (mod4 > 0) clean += new string('=', 4 - mod4);
                return Convert.FromBase64String(clean);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Base64] Logo conversion failed: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        if (reader.TokenType == System.Text.Json.JsonTokenType.StartArray)
        {
            var byteList = new List<byte>();
            while (reader.Read())
            {
                if (reader.TokenType == System.Text.Json.JsonTokenType.EndArray)
                    break;
                byteList.Add(reader.GetByte());
            }
            return byteList.ToArray();
        }

        return Array.Empty<byte>();
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, byte[] value, System.Text.Json.JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(Convert.ToBase64String(value));
    }
}
