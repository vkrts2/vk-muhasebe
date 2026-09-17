import 'react-native-gesture-handler';
import { enableScreens } from 'react-native-screens';

// Disable native screen controllers on iOS Fabric to prevent EXC_BAD_ACCESS / hitTest crashes
try {
  enableScreens(false);
} catch (e) {
  console.warn('[index] enableScreens error:', e);
}

// Global Exception Handler to prevent any unhandled JS crash from terminating the app
if ((global as any).ErrorUtils) {
  try {
    const defaultHandler = (global as any).ErrorUtils.getGlobalHandler();
    (global as any).ErrorUtils.setGlobalHandler((error: any, isFatal?: boolean) => {
      console.warn('[GlobalErrorHandler] Caught error:', error?.message, error?.stack);
      // Suppress fatal crash in production
    });
  } catch (e) {}
}

import { registerRootComponent } from 'expo';
import App from './App';

registerRootComponent(App);
