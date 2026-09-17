-- ==============================================================================
-- VK ÖN MUHASEBE - SUPABASE POSTGRESQL VERİTABANI KURULUM KODLARI (TAM ŞEMA)
-- ==============================================================================
-- Bu SQL kodunu yeni bir Supabase projesinde:
-- SQL Editor -> New Query (Yeni Sorgu) alanına yapıştırıp RUN (Çalıştır) demeniz yeterlidir.
-- Tüm tablolar ve okuma/yazma güvenlik izinleri (RLS) otomatik açılacaktır.
-- ==============================================================================

-- 1. CARİLER TABLOSU
CREATE TABLE IF NOT EXISTS public.cariler (
    id TEXT PRIMARY KEY,
    kod TEXT,
    unvan TEXT,
    bakiye NUMERIC DEFAULT 0,
    vergi_dairesi TEXT,
    vergi_no TEXT,
    telefon TEXT,
    eposta TEXT,
    adres TEXT,
    il TEXT,
    ilce TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
ALTER TABLE public.cariler ENABLE ROW LEVEL SECURITY;
CREATE POLICY "cariler_all_policy" ON public.cariler FOR ALL USING (true) WITH CHECK (true);

-- 2. CARİ HAREKETLER TABLOSU
CREATE TABLE IF NOT EXISTS public.cari_hareketler (
    id TEXT PRIMARY KEY,
    cari_id TEXT,
    evrak_no TEXT,
    islem_turu TEXT,
    borc NUMERIC DEFAULT 0,
    alacak NUMERIC DEFAULT 0,
    bakiye NUMERIC DEFAULT 0,
    aciklama TEXT,
    fatura_id TEXT,
    tarih TIMESTAMPTZ DEFAULT NOW(),
    is_deleted BOOLEAN DEFAULT FALSE
);
ALTER TABLE public.cari_hareketler ENABLE ROW LEVEL SECURITY;
CREATE POLICY "cari_hareketler_all_policy" ON public.cari_hareketler FOR ALL USING (true) WITH CHECK (true);

-- 3. STOKLAR TABLOSU
CREATE TABLE IF NOT EXISTS public.stoklar (
    id TEXT PRIMARY KEY,
    stok_kodu TEXT,
    stok_adi TEXT,
    birim TEXT DEFAULT 'Adet',
    kdv_orani NUMERIC DEFAULT 20,
    alis_fiyati NUMERIC DEFAULT 0,
    satis_fiyati NUMERIC DEFAULT 0,
    mevcut_miktar NUMERIC DEFAULT 0,
    kritik_stok NUMERIC DEFAULT 0,
    grup TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
ALTER TABLE public.stoklar ENABLE ROW LEVEL SECURITY;
CREATE POLICY "stoklar_all_policy" ON public.stoklar FOR ALL USING (true) WITH CHECK (true);

-- 4. STOK HAREKETLER TABLOSU
CREATE TABLE IF NOT EXISTS public.stok_hareketler (
    id TEXT PRIMARY KEY,
    stok_id TEXT,
    evrak_no TEXT,
    hareket_tipi TEXT,
    miktar NUMERIC DEFAULT 0,
    birim_fiyat NUMERIC DEFAULT 0,
    kdv_orani NUMERIC DEFAULT 0,
    kdv_tutari NUMERIC DEFAULT 0,
    toplam_tutar NUMERIC DEFAULT 0,
    aciklama TEXT,
    fatura_id TEXT,
    tarih TIMESTAMPTZ DEFAULT NOW(),
    is_deleted BOOLEAN DEFAULT FALSE
);
ALTER TABLE public.stok_hareketler ENABLE ROW LEVEL SECURITY;
CREATE POLICY "stok_hareketler_all_policy" ON public.stok_hareketler FOR ALL USING (true) WITH CHECK (true);

-- 5. FATURALAR TABLOSU
CREATE TABLE IF NOT EXISTS public.faturalar (
    id TEXT PRIMARY KEY,
    fatura_no TEXT,
    fatura_turu TEXT,
    cari_id TEXT,
    cari_unvan TEXT,
    tarih TIMESTAMPTZ DEFAULT NOW(),
    vade_tarihi TIMESTAMPTZ,
    ara_toplam NUMERIC DEFAULT 0,
    kdv_toplam NUMERIC DEFAULT 0,
    iskonto_toplam NUMERIC DEFAULT 0,
    genel_toplam NUMERIC DEFAULT 0,
    kalan_tutar NUMERIC DEFAULT 0,
    durum TEXT DEFAULT 'Ödenmedi',
    aciklama TEXT,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW()
);
ALTER TABLE public.faturalar ENABLE ROW LEVEL SECURITY;
CREATE POLICY "faturalar_all_policy" ON public.faturalar FOR ALL USING (true) WITH CHECK (true);

-- 6. FATURA DETAYLAR (KALEMLER) TABLOSU
CREATE TABLE IF NOT EXISTS public.fatura_detaylar (
    id TEXT PRIMARY KEY,
    fatura_id TEXT,
    stok_id TEXT,
    stok_kodu TEXT,
    stok_adi TEXT,
    birim TEXT,
    miktar NUMERIC DEFAULT 0,
    birim_fiyat NUMERIC DEFAULT 0,
    kdv_orani NUMERIC DEFAULT 0,
    kdv_tutari NUMERIC DEFAULT 0,
    toplam_tutar NUMERIC DEFAULT 0,
    iskonto_orani NUMERIC DEFAULT 0,
    iskonto_tutari NUMERIC DEFAULT 0
);
ALTER TABLE public.fatura_detaylar ENABLE ROW LEVEL SECURITY;
CREATE POLICY "fatura_detaylar_all_policy" ON public.fatura_detaylar FOR ALL USING (true) WITH CHECK (true);

-- 7. KASALAR TABLOSU
CREATE TABLE IF NOT EXISTS public.kasalar (
    id TEXT PRIMARY KEY,
    kasa_kodu TEXT,
    kasa_adi TEXT,
    bakiye NUMERIC DEFAULT 0,
    para_birimi TEXT DEFAULT 'TRY',
    aciklama TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    is_deleted BOOLEAN DEFAULT FALSE
);
ALTER TABLE public.kasalar ENABLE ROW LEVEL SECURITY;
CREATE POLICY "kasalar_all_policy" ON public.kasalar FOR ALL USING (true) WITH CHECK (true);

-- 8. KASA HAREKETLER TABLOSU
CREATE TABLE IF NOT EXISTS public.kasa_hareketler (
    id TEXT PRIMARY KEY,
    kasa_id TEXT,
    evrak_no TEXT,
    islem_turu TEXT,
    tutar NUMERIC DEFAULT 0,
    aciklama TEXT,
    cari_id TEXT,
    tarih TIMESTAMPTZ DEFAULT NOW(),
    is_deleted BOOLEAN DEFAULT FALSE
);
ALTER TABLE public.kasa_hareketler ENABLE ROW LEVEL SECURITY;
CREATE POLICY "kasa_hareketler_all_policy" ON public.kasa_hareketler FOR ALL USING (true) WITH CHECK (true);

-- 9. BANKALAR TABLOSU
CREATE TABLE IF NOT EXISTS public.bankalar (
    id TEXT PRIMARY KEY,
    banka_adi TEXT,
    hesap_no TEXT,
    iban TEXT,
    bakiye NUMERIC DEFAULT 0,
    para_birimi TEXT DEFAULT 'TRY',
    sube_adi TEXT,
    is_active BOOLEAN DEFAULT TRUE,
    is_deleted BOOLEAN DEFAULT FALSE
);
ALTER TABLE public.bankalar ENABLE ROW LEVEL SECURITY;
CREATE POLICY "bankalar_all_policy" ON public.bankalar FOR ALL USING (true) WITH CHECK (true);

-- 10. BANKA HAREKETLER TABLOSU
CREATE TABLE IF NOT EXISTS public.banka_hareketler (
    id TEXT PRIMARY KEY,
    banka_id TEXT,
    evrak_no TEXT,
    islem_turu TEXT,
    tutar NUMERIC DEFAULT 0,
    aciklama TEXT,
    cari_id TEXT,
    tarih TIMESTAMPTZ DEFAULT NOW(),
    is_deleted BOOLEAN DEFAULT FALSE
);
ALTER TABLE public.banka_hareketler ENABLE ROW LEVEL SECURITY;
CREATE POLICY "banka_hareketler_all_policy" ON public.banka_hareketler FOR ALL USING (true) WITH CHECK (true);

-- 11. SİPARİŞLER TABLOSU
CREATE TABLE IF NOT EXISTS public.siparisler (
    id TEXT PRIMARY KEY,
    siparis_no TEXT,
    siparis_turu TEXT,
    cari_id TEXT,
    cari_unvan TEXT,
    tarih TIMESTAMPTZ DEFAULT NOW(),
    teslim_tarihi TIMESTAMPTZ,
    ara_toplam NUMERIC DEFAULT 0,
    kdv_toplam NUMERIC DEFAULT 0,
    genel_toplam NUMERIC DEFAULT 0,
    durum TEXT DEFAULT 'Bekliyor',
    aciklama TEXT,
    is_deleted BOOLEAN DEFAULT FALSE
);
ALTER TABLE public.siparisler ENABLE ROW LEVEL SECURITY;
CREATE POLICY "siparisler_all_policy" ON public.siparisler FOR ALL USING (true) WITH CHECK (true);

-- 12. SİPARİŞ DETAYLAR (KALEMLER) TABLOSU
CREATE TABLE IF NOT EXISTS public.siparis_detaylar (
    id TEXT PRIMARY KEY,
    siparis_id TEXT,
    stok_id TEXT,
    stok_kodu TEXT,
    stok_adi TEXT,
    birim TEXT,
    miktar NUMERIC DEFAULT 0,
    birim_fiyat NUMERIC DEFAULT 0,
    kdv_orani NUMERIC DEFAULT 0,
    kdv_tutari NUMERIC DEFAULT 0,
    toplam_tutar NUMERIC DEFAULT 0
);
ALTER TABLE public.siparis_detaylar ENABLE ROW LEVEL SECURITY;
CREATE POLICY "siparis_detaylar_all_policy" ON public.siparis_detaylar FOR ALL USING (true) WITH CHECK (true);

-- 13. TEKLİFLER TABLOSU
CREATE TABLE IF NOT EXISTS public.teklifler (
    id TEXT PRIMARY KEY,
    teklif_no TEXT,
    teklif_turu TEXT,
    cari_id TEXT,
    cari_unvan TEXT,
    tarih TIMESTAMPTZ DEFAULT NOW(),
    gecerlilik_tarihi TIMESTAMPTZ,
    ara_toplam NUMERIC DEFAULT 0,
    kdv_toplam NUMERIC DEFAULT 0,
    genel_toplam NUMERIC DEFAULT 0,
    durum TEXT DEFAULT 'Bekliyor',
    aciklama TEXT,
    is_deleted BOOLEAN DEFAULT FALSE
);
ALTER TABLE public.teklifler ENABLE ROW LEVEL SECURITY;
CREATE POLICY "teklifler_all_policy" ON public.teklifler FOR ALL USING (true) WITH CHECK (true);

-- 14. TEKLİF DETAYLAR (KALEMLER) TABLOSU
CREATE TABLE IF NOT EXISTS public.teklif_detaylar (
    id TEXT PRIMARY KEY,
    teklif_id TEXT,
    stok_id TEXT,
    stok_kodu TEXT,
    stok_adi TEXT,
    birim TEXT,
    miktar NUMERIC DEFAULT 0,
    birim_fiyat NUMERIC DEFAULT 0,
    kdv_orani NUMERIC DEFAULT 0,
    kdv_tutari NUMERIC DEFAULT 0,
    toplam_tutar NUMERIC DEFAULT 0
);
ALTER TABLE public.teklif_detaylar ENABLE ROW LEVEL SECURITY;
CREATE POLICY "teklif_detaylar_all_policy" ON public.teklif_detaylar FOR ALL USING (true) WITH CHECK (true);

-- 15. FİRMA PROFİLİ VE SİSTEM AYARLARI TABLOSU
CREATE TABLE IF NOT EXISTS public.firma_profili (
    id TEXT PRIMARY KEY,
    firma_adi TEXT,
    unvan TEXT,
    vergi_dairesi TEXT,
    vergi_no TEXT,
    telefon TEXT,
    eposta TEXT,
    web TEXT,
    adres TEXT,
    logo_base64 TEXT,
    cloud_pdf_api_url TEXT DEFAULT 'https://ermay-pdf-api-390930978984.europe-west1.run.app',
    cloud_pdf_api_key TEXT,
    smtp_host TEXT,
    smtp_port INTEGER DEFAULT 587,
    smtp_user TEXT,
    smtp_pass TEXT,
    smtp_ssl BOOLEAN DEFAULT TRUE,
    factory_reset_password TEXT DEFAULT 'VK2026'
);
ALTER TABLE public.firma_profili ENABLE ROW LEVEL SECURITY;
CREATE POLICY "firma_profili_all_policy" ON public.firma_profili FOR ALL USING (true) WITH CHECK (true);

-- 16. NOTLAR TABLOSU
CREATE TABLE IF NOT EXISTS public.notlar (
    id TEXT PRIMARY KEY,
    title TEXT,
    content TEXT,
    color TEXT,
    is_pinned BOOLEAN DEFAULT FALSE,
    is_deleted BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);
ALTER TABLE public.notlar ENABLE ROW LEVEL SECURITY;
CREATE POLICY "notlar_all_policy" ON public.notlar FOR ALL USING (true) WITH CHECK (true);
