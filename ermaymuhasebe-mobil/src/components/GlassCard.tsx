import React from 'react';
import type { TouchableOpacityProps, StyleProp, ViewStyle, GestureResponderEvent } from 'react-native';
import { TouchableOpacity, StyleSheet, View } from 'react-native';
import { BlurView } from 'expo-blur';
import { LinearGradient } from 'expo-linear-gradient';
import * as Haptics from 'expo-haptics';

interface GlassCardProps extends TouchableOpacityProps {
  children: React.ReactNode;
  style?: StyleProp<ViewStyle>;
  hapticFeedback?: boolean;
  hapticStyle?: Haptics.ImpactFeedbackStyle;
  tint?: 'dark' | 'light' | 'extraDark';
  intensity?: number;
}

export const GlassCard: React.FC<GlassCardProps> = ({
  children,
  style,
  onPress,
  hapticFeedback = true,
  hapticStyle = Haptics.ImpactFeedbackStyle.Light,
  activeOpacity = 0.75,
  intensity = 35,
  ...rest
}) => {
  const handlePress = (e: GestureResponderEvent) => {
    if (hapticFeedback) {
      Haptics.impactAsync(hapticStyle).catch(() => {});
    }
    if (onPress) {
      onPress(e);
    }
  };

  return (
    <TouchableOpacity
      activeOpacity={activeOpacity}
      onPress={handlePress}
      style={[styles.outerContainer, style]}
      {...rest}
    >
      <LinearGradient
        colors={['rgba(255, 255, 255, 0.08)', 'rgba(255, 255, 255, 0.02)']}
        start={{ x: 0, y: 0 }}
        end={{ x: 1, y: 1 }}
        style={styles.gradientBorder}
      >
        <BlurView intensity={intensity} tint="dark" style={styles.blurContent}>
          {children}
        </BlurView>
      </LinearGradient>
    </TouchableOpacity>
  );
};

const styles = StyleSheet.create({
  outerContainer: {
    borderRadius: 20,
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.45,
    shadowRadius: 14,
    elevation: 8,
    backgroundColor: '#101014',
  },
  gradientBorder: {
    borderRadius: 20,
    padding: 1,
  },
  blurContent: {
    backgroundColor: 'rgba(20, 20, 26, 0.72)',
    borderRadius: 19,
    padding: 16,
  },
});
