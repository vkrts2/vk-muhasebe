declare module 'expo-file-system/legacy' {
  export const cacheDirectory: string | null;
  export const documentDirectory: string | null;
  export const writeAsStringAsync: (fileUri: string, contents: string, options?: any) => Promise<void>;
  export const readAsStringAsync: (fileUri: string, options?: any) => Promise<string>;
  export const deleteAsync: (fileUri: string, options?: any) => Promise<void>;
  export const getInfoAsync: (fileUri: string, options?: any) => Promise<any>;
  export const EncodingType: {
    UTF8: 'utf8';
    Base64: 'base64';
  };
  const _default: any;
  export default _default;
}

