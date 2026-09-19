$ErrorActionPreference = "Stop"

Write-Host "==================================================="
Write-Host "  VK ON MUHASEBE - TEMIZ SIFIR VERITABANI ISLEMI"
Write-Host "==================================================="

# 1. Calisan VK sureclerini kapat
$procs = Get-Process -Name "VK" -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host "Calisan VK uygulamasi kapatiliyor..."
    $procs | Stop-Process -Force
    Start-Sleep -Seconds 1
}

# 2. LocalAppData yolu
$appDataDir = "$env:LOCALAPPDATA\ErmayMuhasebe"
if (-not (Test-Path $appDataDir)) {
    New-Item -ItemType Directory -Path $appDataDir -Force | Out-Null
}

$backupsDir = Join-Path $appDataDir "Backups"
if (-not (Test-Path $backupsDir)) {
    New-Item -ItemType Directory -Path $backupsDir -Force | Out-Null
}

# 3. Mevcut veritabanini guvenlik icin yedekle
$dbFile = Join-Path $appDataDir "ErmayV4_Stable.db3"
if (Test-Path $dbFile) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupPath = Join-Path $backupsDir "yedek_sifirlama_oncesi_$timestamp.db3"
    Copy-Item $dbFile $backupPath -Force
    Write-Host "Mevcut veri guvenlik icin yedeklendi: $backupPath"
}

# 4. Supabase Bulut verilerini sifirla (Eski test verilerinin geri yuklenmesini onlemek icin)
$cloudConfigPath = "$env:LOCALAPPDATA\ermay_cloud_config.json"
if (Test-Path $cloudConfigPath) {
    try {
        $cfg = Get-Content $cloudConfigPath | ConvertFrom-Json
        if ($cfg.BaseUrl -and $cfg.AuthSecret -and $cfg.BaseUrl -match 'supabase\.co') {
            Write-Host "Supabase bulut veritabani temizleniyor ($($cfg.BaseUrl))..."
            $headers = @{
                apikey = $cfg.AuthSecret
                Authorization = "Bearer $($cfg.AuthSecret)"
            }
            $tables = @(
                'cari_hareketler','cariler','stok_hareketler','stoklar',
                'fatura_detaylar','faturalar','siparis_detaylar','siparisler',
                'teklif_detaylar','teklifler','banka_hareketler','bankalar',
                'kasa_hareketler','kasalar','cekler','senetler',
                'kredi_karti_islemler','eft_islemler','doviz_kurlari','belge_arsiv',
                'notlar','gorevler','personeller','firma_profili',
                'satis_hedefleri','haftalik_satis_hedefleri','yillik_satis_hedefleri',
                'stok_sayim_fisileri','stok_sayim_detaylari','portfoy_kartlar',
                'musteri_takip_klasorler','musteri_takip_detaylar'
            )
            foreach ($t in $tables) {
                try {
                    Invoke-RestMethod -Uri "$($cfg.BaseUrl)/rest/v1/$t`?id=gte.0" -Method Delete -Headers $headers -ErrorAction SilentlyContinue
                } catch {}
            }
            Write-Host "Supabase bulut veritabani tamamen temizlendi."
        }
    } catch {
        Write-Warning "Supabase temizleme uyarisi: $_"
    }
}

# 5. Yerel veritabani ve gecici dosyalari sil
$filesToDelete = @(
    "ErmayV4_Stable.db3",
    "ErmayV4_Stable.db3-wal",
    "ErmayV4_Stable.db3-shm",
    "notifications_*.json",
    "dismissed_alerts_*.json"
)

foreach ($f in $filesToDelete) {
    Get-ChildItem -Path $appDataDir -Filter $f -File -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
        Write-Host "Silindi: $($_.Name)"
    }
}

# 6. Temiz kurulum kullanici konfigurasyonu olustur (admin / 123)
$initUserConfig = @{
    Users = @(
        @{
            Username = "admin"
            Password = "123"
            Email = ""
        }
    )
    FactoryResetPassword = "123"
    GoogleClientId = ""
    GoogleClientSecret = ""
    SmtpEmail = ""
    SmtpPass = ""
    TelegramBotToken = ""
    TelegramChatId = ""
} | ConvertTo-Json -Depth 5

Set-Content -Path (Join-Path $appDataDir "setup_initial_user.json") -Value $initUserConfig -Encoding UTF8
Write-Host "Temiz yonetici (admin / 123) yapilandirmasi hazirlandi."

Write-Host "==================================================="
Write-Host "Veritabani basariyla tertemiz ve sifirlandi!"
Write-Host "Programi calistirdiginizda tertemiz, bos bir veritabani acilacaktir."
Write-Host "Giris Bilgileri: Kullanici: admin | Sifre: 123"
Write-Host "==================================================="
