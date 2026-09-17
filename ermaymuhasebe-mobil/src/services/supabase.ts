import { createClient, SupabaseClient } from '@supabase/supabase-js';
import AsyncStorage from './storage';

export interface SupabaseConfig {
  url: string;
  anonKey: string;
  tenantId?: string;
}

export interface FirebaseConfig {
  url: string;
  secret?: string;
  tenantId?: string;
}

export interface ExtendedFirebaseConfig extends FirebaseConfig {
  googleApiKey?: string;
  googleClientId?: string;
  googleClientSecret?: string;
  telegramBotToken?: string;
  telegramChatId?: string;
  smtpEmail?: string;
  smtpPass?: string;
}

export const DEFAULT_SUPABASE_URL = 'https://fqgbdymffknglqeqoogt.supabase.co';
export const DEFAULT_SUPABASE_KEY = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6ImZxZ2JkeW1mZmtuZ2xxZXFvb2d0Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODk1ODUzMDEsImV4cCI6MjEwNTE2MTMwMX0.pBeE2ivWpbkAd8KSN1y2pXNZPIr_1mGMLXXHYPzjTDg';

let cachedConfig: SupabaseConfig = {
  url: DEFAULT_SUPABASE_URL,
  anonKey: DEFAULT_SUPABASE_KEY,
  tenantId: 'default'
};

let cachedExtendedConfig: ExtendedFirebaseConfig = {
  url: DEFAULT_SUPABASE_URL,
  secret: DEFAULT_SUPABASE_KEY,
  tenantId: 'default'
};

let cachedYear: string = new Date().getFullYear().toString();
let supabase: SupabaseClient = createClient(cachedConfig.url, cachedConfig.anonKey);

let configListeners: (() => void)[] = [];

export const getSupabaseClient = () => supabase;

// --- Helper Case Conversion ---
export const toSnakeCase = (obj: any): any => {
  if (!obj || typeof obj !== 'object') return obj;
  if (Array.isArray(obj)) return obj.map(toSnakeCase);
  const newObj: any = {};
  for (const key of Object.keys(obj)) {
    const snakeKey = key.replace(/[A-Z]/g, letter => `_${letter.toLowerCase()}`);
    newObj[snakeKey] = toSnakeCase(obj[key]);
  }
  return newObj;
};

export const toCamelCase = (obj: any): any => {
  if (!obj || typeof obj !== 'object') return obj;
  if (Array.isArray(obj)) return obj.map(toCamelCase);
  const newObj: any = {};
  for (const key of Object.keys(obj)) {
    const camelKey = key.replace(/_([a-z0-9])/g, (_, g) => g.toUpperCase());
    newObj[camelKey] = toCamelCase(obj[key]);
  }
  return newObj;
};

// --- Table Mapping ---
const TABLE_MAPPINGS: Record<string, string> = {
  'cariler': 'cariler',
  'carihareketler': 'cari_hareketler',
  'stoklar': 'stoklar',
  'stokhareketler': 'stok_hareketler',
  'faturalar': 'faturalar',
  'faturadetaylar': 'fatura_detaylar',
  'siparisler': 'siparisler',
  'siparisdetaylar': 'siparis_detaylar',
  'teklifler': 'teklifler',
  'teklifdetaylar': 'teklif_detaylar',
  'kasalar': 'kasalar',
  'kasahareketler': 'kasa_hareketler',
  'bankalar': 'bankalar',
  'bankahareketler': 'banka_hareketler',
  'firmaprofili': 'firma_profili',
  'notes': 'notlar',
  'notlar': 'notlar'
};

export const parsePath = (path: string): { table: string; id?: string; isDetail?: boolean; foreignKey?: string } => {
  let clean = path.replace(/^companies\/[^\/]+\/years\/[^\/]+\//, '');
  clean = clean.replace(/^\/+|\/+$/g, '');
  const parts = clean.split('/');

  const rawTable = (parts[0] || '').toLowerCase();
  const table = TABLE_MAPPINGS[rawTable] || rawTable;
  const id = parts[1];

  if (table === 'fatura_detaylar' && id) {
    return { table, id, isDetail: true, foreignKey: 'fatura_id' };
  }
  if (table === 'siparis_detaylar' && id) {
    return { table, id, isDetail: true, foreignKey: 'siparis_id' };
  }
  if (table === 'teklif_detaylar' && id) {
    return { table, id, isDetail: true, foreignKey: 'teklif_id' };
  }

  return { table, id };
};

// --- Storage & Config ---
export const loadConfigFromStorage = async () => {
  try {
    const url = await AsyncStorage.getItem('ermay_supabase_url');
    const key = await AsyncStorage.getItem('ermay_supabase_key');
    const yr = await AsyncStorage.getItem('ermay_active_year');
    if (url && key) {
      cachedConfig = { url, anonKey: key, tenantId: 'default' };
      supabase = createClient(url, key);
    }
    if (yr) cachedYear = yr;
  } catch (e) {
    console.log('[Supabase] loadConfigFromStorage error:', e);
  }
};

export const saveSupabaseConfig = async (url: string, anonKey: string) => {
  cachedConfig = { url, anonKey, tenantId: 'default' };
  supabase = createClient(url, anonKey);
  await AsyncStorage.setItem('ermay_supabase_url', url);
  await AsyncStorage.setItem('ermay_supabase_key', anonKey);
  notifyConfigListeners();
};

export const saveActiveYear = async (year: string) => {
  cachedYear = year;
  await AsyncStorage.setItem('ermay_active_year', year);
  notifyConfigListeners();
};

export const addConfigListener = (listener: () => void): (() => void) => {
  configListeners.push(listener);
  return () => {
    configListeners = configListeners.filter(l => l !== listener);
  };
};

export const notifyConfigListeners = () => {
  configListeners.forEach(l => {
    try { l(); } catch (e) {}
  });
};

// --- Compatibility Aliases for screens ---
export const getFirebaseConfig = (): FirebaseConfig | null => ({
  url: cachedConfig.url,
  secret: cachedConfig.anonKey,
  tenantId: cachedConfig.tenantId
});

export const getExtendedConfig = (): ExtendedFirebaseConfig | null => cachedExtendedConfig;

export const saveFirebaseConfig = async (
  url: string,
  secret?: string,
  tenantId: string = 'default',
  extra?: Partial<ExtendedFirebaseConfig>
) => {
  if (url && secret) {
    await saveSupabaseConfig(url, secret);
  }
  cachedExtendedConfig = {
    url,
    secret,
    tenantId,
    ...extra
  };
};

export const fetchAvailableYears = async (): Promise<string[]> => {
  const current = new Date().getFullYear();
  return [ (current - 1).toString(), current.toString(), (current + 1).toString() ];
};

export const getIdToken = () => null;
export const saveIdToken = async () => {};
export const mapAppToDatabase = (path: string, val: any): any => val;
export const mapDatabaseToApp = (path: string, val: any): any => val;
export const mapPathToDatabase = (path: string): string => path;
export const getCacheKey = (path: string): string => path;
export const fetchWithTimeout = async (url: string, opts?: any, timeoutMs?: number): Promise<Response> => fetch(url, opts);
export const getAuthParam = (config?: any): string => '';

// --- CRUD Operations ---
export const readData = async (path: string, timeoutMs = 7000): Promise<any> => {
  try {
    const { table, id, isDetail, foreignKey } = parsePath(path);

    if (isDetail && foreignKey && id) {
      const { data, error } = await supabase
        .from(table)
        .select('*')
        .eq(foreignKey, id);

      if (error) {
        console.warn(`[Supabase] readData error on ${table}:`, error.message);
        return [];
      }
      return toCamelCase(data || []);
    }

    if (id) {
      const { data, error } = await supabase
        .from(table)
        .select('*')
        .eq('id', id)
        .maybeSingle();

      if (error) {
        console.warn(`[Supabase] readData single error on ${table}/${id}:`, error.message);
        return null;
      }
      return toCamelCase(data);
    }

    // List all
    const { data, error } = await supabase
      .from(table)
      .select('*');

    if (error) {
      console.warn(`[Supabase] readData list error on ${table}:`, error.message);
      return {};
    }

    const camelList = toCamelCase(data || []);
    const recordMap: Record<string, any> = {};
    for (const item of camelList) {
      if (item && item.id !== undefined) {
        recordMap[item.id.toString()] = item;
      }
    }
    return recordMap;
  } catch (err: any) {
    console.error(`[Supabase] readData exception on ${path}:`, err.message);
    return null;
  }
};

export const writeData = async (path: string, data: any): Promise<boolean> => {
  try {
    const { table, id, isDetail, foreignKey } = parsePath(path);

    if (isDetail && foreignKey && id) {
      if (Array.isArray(data)) {
        await supabase.from(table).delete().eq(foreignKey, id);
        if (data.length > 0) {
          const snakeItems = toSnakeCase(data).map((item: any) => ({
            ...item,
            [foreignKey]: id
          }));
          const { error } = await supabase.from(table).insert(snakeItems);
          if (error) throw error;
        }
        return true;
      }
    }

    if (data === null || data === undefined) {
      return await deleteData(path);
    }

    let payload = { ...data };
    if (id && payload.id === undefined) {
      payload.id = Number(id) || id;
    }

    const snakeData = toSnakeCase(payload);
    const { error } = await supabase
      .from(table)
      .upsert(snakeData, { onConflict: 'id' });

    if (error) {
      console.error(`[Supabase] writeData error on ${table}:`, error.message);
      return false;
    }
    return true;
  } catch (err: any) {
    console.error(`[Supabase] writeData exception on ${path}:`, err.message);
    return false;
  }
};

export const deleteData = async (path: string): Promise<boolean> => {
  try {
    const { table, id, isDetail, foreignKey } = parsePath(path);
    if (isDetail && foreignKey && id) {
      const { error } = await supabase.from(table).delete().eq(foreignKey, id);
      return !error;
    }
    if (id) {
      const { error } = await supabase.from(table).delete().eq('id', id);
      return !error;
    }
    return false;
  } catch (err: any) {
    console.error(`[Supabase] deleteData error on ${path}:`, err.message);
    return false;
  }
};

export const updateDataBatch = async (updates: Record<string, any>): Promise<boolean> => {
  try {
    let success = true;
    for (const [path, val] of Object.entries(updates)) {
      const ok = await writeData(path, val);
      if (!ok) success = false;
    }
    return success;
  } catch (e: any) {
    console.error('[Supabase] updateDataBatch error:', e.message);
    return false;
  }
};

export const subscribeToPath = (path: string, callback: (data: any) => void): (() => void) => {
  const { table } = parsePath(path);
  
  readData(path).then(data => {
    if (data !== null) callback(data);
  });

  const channel = supabase
    .channel(`public:${table}`)
    .on(
      'postgres_changes',
      { event: '*', schema: 'public', table },
      () => {
        readData(path).then(data => {
          if (data !== null) callback(data);
        });
      }
    )
    .subscribe();

  return () => {
    supabase.removeChannel(channel);
  };
};

export const updateFutureBalances = async (
  entityType: string,
  entityId: string | number,
  borcDelta: number,
  alacakDelta: number
) => {
  try {
    const { table } = parsePath(entityType);
    const { data: current } = await supabase
      .from(table)
      .select('borc_tutari, alacak_tutari, bakiye')
      .eq('id', entityId)
      .maybeSingle();

    if (current) {
      const newBorc = (Number(current.borc_tutari) || 0) + borcDelta;
      const newAlacak = (Number(current.alacak_tutari) || 0) + alacakDelta;
      const newBakiye = newBorc - newAlacak;

      await supabase
        .from(table)
        .update({
          borc_tutari: newBorc,
          alacak_tutari: newAlacak,
          bakiye: newBakiye
        })
        .eq('id', entityId);
    }
  } catch (err: any) {
    console.error('[Supabase] updateFutureBalances error:', err.message);
  }
};

export const updateCariBaseInfoInAllYears = async (cariId: number | string, baseInfo: any) => {
  try {
    const snake = toSnakeCase(baseInfo);
    await supabase.from('cariler').update(snake).eq('id', cariId);
  } catch (err: any) {
    console.error('[Supabase] updateCariBaseInfoInAllYears error:', err.message);
  }
};

export const pushData = async (path: string, data: any): Promise<string | null> => {
  const newId = Date.now();
  const ok = await writeData(`${path}/${newId}`, { ...data, id: newId });
  return ok ? newId.toString() : null;
};

// --- Account / Kasa Helper Functions ---
export const KASA_KART_TURU = 'Kasa';

export const isKasaRecord = (record: any): boolean => {
  if (!record) return false;
  return record.kartTuru === 'Kasa' || record.turu === 'Kasa';
};

export const splitAccounts = (bankalar: any[]): { kasalar: any[]; bankalar: any[] } => {
  const kList: any[] = [];
  const bList: any[] = [];
  (bankalar || []).forEach(b => {
    if (isKasaRecord(b)) kList.push(b);
    else bList.push(b);
  });
  return { kasalar: kList, bankalar: bList };
};

export const mergeKasalar = (bankKasa: any[], legacyKasa: any[]): any[] => {
  const map = new Map<string, any>();
  (bankKasa || []).forEach(k => { if (k?.id) map.set(k.id.toString(), k); });
  (legacyKasa || []).forEach(k => { if (k?.id && !map.has(k.id.toString())) map.set(k.id.toString(), k); });
  return Array.from(map.values());
};

export const getKasaWritePath = (id: number | string): string => `Kasalar/${id}`;

export const toKasaRecord = (kasa: any): any => ({
  id: kasa.id,
  kasaKodu: kasa.kasaKodu || kasa.kod || '',
  kasaAdi: kasa.kasaAdi || kasa.ad || '',
  bakiye: Number(kasa.bakiye) || 0,
  paraBirimi: kasa.paraBirimi || 'TRY',
  aciklama: kasa.aciklama || '',
  isActive: kasa.isActive !== false,
  isDeleted: kasa.isDeleted === true
});

// --- Auth Compatibility ---
export const registerInitialUser = async (username: string, password: string, email?: string): Promise<boolean> => {
  try {
    const userEmail = email && email.includes('@') ? email : `${username.toLowerCase().trim()}@ermay.local`;
    try {
      await supabase.auth.signUp({
        email: userEmail,
        password,
        options: {
          data: {
            username: username.toLowerCase().trim(),
            role: 'Admin',
            fullName: username.trim()
          }
        }
      });
    } catch (authErr) {
      console.warn('[registerInitialUser] Supabase auth signup warning:', authErr);
    }

    // Yerel belleğe de güvenli şekilde kaydet
    await AsyncStorage.setItem('ermay_saved_username', username.trim());
    await AsyncStorage.setItem('ermay_saved_password', password.trim());
    return true;
  } catch (e) {
    console.error('[registerInitialUser error]:', e);
    return false;
  }
};

export const loginUser = async (usernameOrEmail: string, password: string): Promise<{ success: boolean; error?: string; user?: any }> => {
  try {
    const cleanUser = usernameOrEmail.trim();
    const targetEmail = cleanUser.includes('@') ? cleanUser : `${cleanUser.toLowerCase()}@ermay.local`;

    // 1. Supabase Auth ile dene
    try {
      const { data, error } = await supabase.auth.signInWithPassword({
        email: targetEmail,
        password
      });

      if (data?.user) {
        return { success: true, user: data.user };
      }
    } catch {}

    // 2. Yerelde kurulumda belirlenen kullanıcı adı ve şifreyi kontrol et
    const localUsername = await AsyncStorage.getItem('ermay_saved_username');
    const localPassword = await AsyncStorage.getItem('ermay_saved_password');
    if (localUsername && localPassword) {
      if (
        (cleanUser.toLowerCase() === localUsername.toLowerCase() || targetEmail.toLowerCase() === `${localUsername.toLowerCase()}@ermay.local`) &&
        password === localPassword
      ) {
        return {
          success: true,
          user: { id: localUsername, email: `${localUsername}@ermay.local`, user_metadata: { role: 'Admin', fullName: localUsername } }
        };
      }
    }

    return { success: false, error: 'Geçersiz kullanıcı adı veya şifre' };
  } catch (err: any) {
    return { success: false, error: err.message };
  }
};

export const logoutUser = async () => {
  try {
    await supabase.auth.signOut();
  } catch (e) {}
};

export const getLoggedUser = async (): Promise<any | null> => {
  try {
    const { data } = await supabase.auth.getUser();
    return data?.user || null;
  } catch (e) {
    return null;
  }
};

export const initFirebase = () => {};
export const startAutoFlush = () => {};
export const stopAutoFlush = () => {};
export const flushPendingWrites = async () => 0;
export const getPendingWriteCount = async () => 0;
export const goOfflineMode = async () => {};
export const goOnlineMode = () => {};

export default supabase;
