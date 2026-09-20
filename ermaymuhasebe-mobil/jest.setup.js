jest.mock('expo-secure-store', () => ({
  getItemAsync: jest.fn(async () => null),
  setItemAsync: jest.fn(async () => {}),
  deleteItemAsync: jest.fn(async () => {})
}));

jest.mock('expo-crypto', () => ({
  digestStringAsync: jest.fn(async (algorithm, str) => 'mocked-hash-' + str),
  CryptoDigestAlgorithm: { SHA256: 'SHA-256' }
}));

jest.mock('expo-file-system/legacy', () => ({
  cacheDirectory: '/cache/',
  writeAsStringAsync: jest.fn(),
  EncodingType: { Base64: 'base64' }
}), { virtual: true });

jest.mock('firebase/app', () => ({
  initializeApp: jest.fn(() => ({})),
  getApps: jest.fn(() => []),
  getApp: jest.fn(() => ({}))
}), { virtual: true });

jest.mock('firebase/database', () => ({
  getDatabase: jest.fn(() => ({})),
  goOnline: jest.fn(),
  goOffline: jest.fn()
}), { virtual: true });

jest.mock('react-native', () => ({
  Platform: { OS: 'ios', select: jest.fn(dict => dict.ios || dict.default) },
  Alert: { alert: jest.fn() },
  Dimensions: { get: jest.fn(() => ({ width: 375, height: 812 })) }
}));
