import React, { useState, useEffect, useRef } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Platform,
  Keyboard,
  Animated,
  Dimensions,
} from 'react-native';
import { ChevronDown, Keyboard as KeyboardIcon } from 'lucide-react-native';

export const KEYBOARD_ACCESSORY_ID = 'appNumericDoneBar';

// Global flag to track whether a root dismiss bar is active
let rootBarMounted = false;

export const setRootBarMounted = (val: boolean) => {
  rootBarMounted = val;
};

export const isRootBarMounted = () => rootBarMounted;

export interface KeyboardDismissBarProps {
  isRoot?: boolean;
  inModal?: boolean;
  onDone?: () => void;
  nativeID?: string;
  buttonText?: string;
}

export const KeyboardDismissBar: React.FC<KeyboardDismissBarProps> = ({
  isRoot = false,
  inModal = false,
  onDone,
  buttonText = 'Klavyeyi Kapat',
}) => {
  const [visible, setVisible] = useState(false);
  const animBottom = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (isRoot) {
      rootBarMounted = true;
      return () => {
        rootBarMounted = false;
      };
    }
  }, [isRoot]);

  useEffect(() => {
    // If this is a non-root, non-modal instance and root is mounted, do not bind duplicate listeners
    if (!isRoot && !inModal && rootBarMounted) {
      return;
    }

    const onShow = (e: any) => {
      const kHeight = e?.endCoordinates?.height || 280;
      const duration = e?.duration || (Platform.OS === 'ios' ? 250 : 150);

      let targetBottom = kHeight;
      if (Platform.OS === 'android') {
        const winH = Dimensions.get('window').height;
        const scrH = Dimensions.get('screen').height;
        // On Android with adjustResize, window height shrinks by keyboard height.
        // If window shrunk significantly, bottom is 0 (already above keyboard).
        const isResized = (scrH - winH) >= (kHeight * 0.5);
        targetBottom = isResized ? 0 : kHeight;
      }

      setVisible(true);
      Animated.timing(animBottom, {
        toValue: targetBottom,
        duration,
        useNativeDriver: false,
      }).start();
    };

    const onHide = (e: any) => {
      const duration = e?.duration || (Platform.OS === 'ios' ? 250 : 150);
      Animated.timing(animBottom, {
        toValue: 0,
        duration,
        useNativeDriver: false,
      }).start(() => {
        setVisible(false);
      });
    };

    const showEvent = Platform.OS === 'ios' ? 'keyboardWillShow' : 'keyboardDidShow';
    const hideEvent = Platform.OS === 'ios' ? 'keyboardWillHide' : 'keyboardDidHide';

    const showSub = Keyboard.addListener(showEvent, onShow);
    const hideSub = Keyboard.addListener(hideEvent, onHide);

    return () => {
      showSub.remove();
      hideSub.remove();
    };
  }, [isRoot, inModal]);

  // If this is a duplicate non-modal instance, do not render
  if (!isRoot && !inModal && rootBarMounted) {
    return null;
  }

  if (!visible) {
    return null;
  }

  const handleDismiss = () => {
    Keyboard.dismiss();
    if (onDone) onDone();
  };

  return (
    <Animated.View
      style={[
        styles.barContainer,
        {
          bottom: animBottom,
        },
      ]}
      pointerEvents="box-none"
    >
      <View style={styles.barInner}>
        <View style={styles.leftBadge}>
          <KeyboardIcon size={15} color="#38BDF8" />
          <Text style={styles.badgeText}>Klavye</Text>
        </View>

        <TouchableOpacity
          style={styles.closeButton}
          onPress={handleDismiss}
          activeOpacity={0.75}
          hitSlop={{ top: 10, bottom: 10, left: 16, right: 16 }}
        >
          <ChevronDown size={16} color="#FFFFFF" strokeWidth={2.5} />
          <Text style={styles.closeButtonText}>{buttonText}</Text>
        </TouchableOpacity>
      </View>
    </Animated.View>
  );
};

const styles = StyleSheet.create({
  barContainer: {
    position: 'absolute',
    left: 0,
    right: 0,
    zIndex: 999999,
    elevation: 999999,
  },
  barInner: {
    height: 42,
    backgroundColor: '#161922',
    borderTopWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.14)',
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(0, 0, 0, 0.4)',
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 14,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: -3 },
    shadowOpacity: 0.28,
    shadowRadius: 4,
  },
  leftBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingVertical: 3,
    paddingHorizontal: 8,
    borderRadius: 6,
    backgroundColor: 'rgba(56, 189, 248, 0.12)',
  },
  badgeText: {
    color: '#38BDF8',
    fontSize: 12,
    fontWeight: '600',
  },
  closeButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 5,
    paddingVertical: 6,
    paddingHorizontal: 13,
    borderRadius: 8,
    backgroundColor: '#0061FF',
    shadowColor: '#0061FF',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.35,
    shadowRadius: 3,
    elevation: 3,
  },
  closeButtonText: {
    color: '#FFFFFF',
    fontSize: 13,
    fontWeight: '700',
  },
});

export default KeyboardDismissBar;
