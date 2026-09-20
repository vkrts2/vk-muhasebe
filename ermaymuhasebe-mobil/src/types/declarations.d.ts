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

declare module 'expo' {
  export const registerRootComponent: any;
  const _default: any;
  export default _default;
}

declare module '@react-navigation/native' {
  export const NavigationContainer: any;
  export function useNavigation<T = any>(): T;
  export function useRoute<T = any>(): T;
  export function useIsFocused(): boolean;
  export const DefaultTheme: any;
  export const DarkTheme: any;
  const _default: any;
  export default _default;
}

declare module '@react-navigation/native-stack' {
  export const createNativeStackNavigator: any;
  const _default: any;
  export default _default;
}

declare module '@react-navigation/bottom-tabs' {
  export const createBottomTabNavigator: any;
  const _default: any;
  export default _default;
}
