import { Platform } from 'react-native';

let Haptics: any = null;
try {
  Haptics = require('expo-haptics');
} catch {
  Haptics = null;
}

/**
 * Apple Taptic Engine Feedback Service (Safe Execution)
 */
export function triggerLightHaptic() {
  try {
    if (Haptics && Haptics.impactAsync && Haptics.ImpactFeedbackStyle) {
      Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Light).catch(() => {});
    }
  } catch (e) {}
}

export function triggerMediumHaptic() {
  try {
    if (Haptics && Haptics.impactAsync && Haptics.ImpactFeedbackStyle) {
      Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium).catch(() => {});
    }
  } catch (e) {}
}

export function triggerSelectionHaptic() {
  try {
    if (Haptics && Haptics.selectionAsync) {
      Haptics.selectionAsync().catch(() => {});
    }
  } catch (e) {}
}

export function triggerSuccessHaptic() {
  try {
    if (Haptics && Haptics.notificationAsync && Haptics.NotificationFeedbackType) {
      Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success).catch(() => {});
    }
  } catch (e) {}
}

export function triggerErrorHaptic() {
  try {
    if (Haptics && Haptics.notificationAsync && Haptics.NotificationFeedbackType) {
      Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error).catch(() => {});
    }
  } catch (e) {}
}
