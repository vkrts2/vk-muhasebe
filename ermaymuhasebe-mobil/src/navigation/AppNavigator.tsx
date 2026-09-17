import React, { useRef, useEffect } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Platform,
  Animated,
  PanResponder,
  Dimensions,
} from 'react-native';
import { createBottomTabNavigator, BottomTabBarProps } from '@react-navigation/bottom-tabs';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { NavigationContainer, DarkTheme } from '@react-navigation/native';
import { Home, Users, FileText, Settings, Package } from 'lucide-react-native';

// Safe dynamic imports for Expo Blur & Linear Gradient
let BlurView: any = null;
let LinearGradient: any = null;
try {
  BlurView = require('expo-blur').BlurView;
} catch {
  BlurView = null;
}
try {
  LinearGradient = require('expo-linear-gradient').LinearGradient;
} catch {
  LinearGradient = null;
}

import { triggerSelectionHaptic } from '../services/hapticsService';

// Screens
import DashboardScreen from '../screens/DashboardScreen';
import CarilerScreen from '../screens/CarilerScreen';
import FaturalarScreen from '../screens/FaturalarScreen';
import StoklarScreen from '../screens/StoklarScreen';
import DahaFazlaScreen from '../screens/DahaFazlaScreen';
import FinansScreen from '../screens/FinansScreen';
import SiparislerScreen from '../screens/SiparislerScreen';
import TekliflerScreen from '../screens/TekliflerScreen';
import VadeTakipScreen from '../screens/VadeTakipScreen';
import RaporlarScreen from '../screens/RaporlarScreen';
import RaporDetayScreen from '../screens/RaporDetayScreen';
import AyarlarScreen from '../screens/AyarlarScreen';
import KanbanScreen from '../screens/KanbanScreen';
import MaliyetHesaplamaScreen from '../screens/MaliyetHesaplamaScreen';
import AraclarScreen from '../screens/AraclarScreen';
import StokGrupScreen from '../screens/StokGrupScreen';
import FaturaFormScreen from '../screens/FaturaFormScreen';
import SiparisFormScreen from '../screens/SiparisFormScreen';
import TeklifFormScreen from '../screens/TeklifFormScreen';
import MusteriTakipScreen from '../screens/MusteriTakipScreen';
import MusteriTakipDetayScreen from '../screens/MusteriTakipDetayScreen';

const { width: SCREEN_WIDTH } = Dimensions.get('window');
const RootStack = createNativeStackNavigator();
const Tab = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

// iOS Liquid Glass Bar Dimensions
const NUM_TABS = 5;
const BAR_MARGIN = 14;
const BAR_WIDTH = SCREEN_WIDTH - (BAR_MARGIN * 2);
const PADDING_H = 4;
const TAB_WIDTH = (BAR_WIDTH - (PADDING_H * 2)) / NUM_TABS;
const PILL_WIDTH = TAB_WIDTH - 4;
const PILL_HEIGHT = 52;

const getPillX = (idx: number) => {
  const safeIdx = Math.max(0, Math.min(NUM_TABS - 1, idx));
  return PADDING_H + safeIdx * TAB_WIDTH + (TAB_WIDTH - PILL_WIDTH) / 2;
};

function CustomTabBar({ state, descriptors, navigation }: BottomTabBarProps) {
  const activeIndex = state.index;
  const pillAnimX = useRef(new Animated.Value(getPillX(activeIndex))).current;

  // Sync pill animation whenever activeIndex changes
  useEffect(() => {
    Animated.spring(pillAnimX, {
      toValue: getPillX(activeIndex),
      useNativeDriver: true,
      tension: 150,
      friction: 18,
    }).start();
  }, [activeIndex]);

  const renderBarContent = () => (
    <View style={styles.barBackground}>
      {/* Animated Sliding Liquid Pill Highlight */}
      <Animated.View
        style={[
          styles.slidingPill,
          {
            width: PILL_WIDTH,
            height: PILL_HEIGHT,
            transform: [
              { translateX: pillAnimX },
                          ],
          },
        ]}
      >
        {LinearGradient ? (
          <LinearGradient
            colors={['rgba(255, 255, 255, 0.24)', 'rgba(255, 255, 255, 0.08)']}
            style={styles.pillGradient}
            start={{ x: 0, y: 0 }}
            end={{ x: 0, y: 1 }}
          />
        ) : (
          <View style={styles.pillFallback} />
        )}
      </Animated.View>

      {/* Tab Icons & Text Labels */}
      {state.routes.map((route, index) => {
        const isFocused = activeIndex === index;

        const onPress = () => {
          triggerSelectionHaptic();
          navigation.navigate(route.name);
        };

        let IconComponent = Home;
        let label = 'Ana Sayfa';

        if (route.name === 'Dashboard') {
          IconComponent = Home;
          label = 'Ana Sayfa';
        } else if (route.name === 'Cariler') {
          IconComponent = Users;
          label = 'Cariler';
        } else if (route.name === 'Faturalar') {
          IconComponent = FileText;
          label = 'Faturalar';
        } else if (route.name === 'Stoklar') {
          IconComponent = Package;
          label = 'Stoklar';
        } else if (route.name === 'DahaFazla') {
          IconComponent = Settings;
          label = 'Daha Fazla';
        }

        return (
          <TouchableOpacity
            key={route.key}
            onPress={onPress}
            activeOpacity={0.7}
            style={styles.tabItem}
          >
            <IconComponent
              color={isFocused ? '#FFFFFF' : '#8E8E93'}
              size={19}
              strokeWidth={isFocused ? 2.4 : 1.8}
            />
            <Text
              style={[
                styles.tabLabel,
                {
                  color: isFocused ? '#FFFFFF' : '#8E8E93',
                  fontWeight: isFocused ? '700' : '500',
                },
              ]}
              numberOfLines={1}
            >
              {label}
            </Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );

  return (
    <View style={styles.floatingContainer} pointerEvents="box-none">
      <View style={styles.glassOuterWrapper}>
        {BlurView ? (
          <BlurView intensity={Platform.OS === 'ios' ? 70 : 100} tint="dark" style={styles.barBlurView}>
            {LinearGradient ? (
              <LinearGradient
                colors={['rgba(255, 255, 255, 0.16)', 'rgba(255, 255, 255, 0.03)']}
                style={styles.gradientBorder}
                start={{ x: 0, y: 0 }}
                end={{ x: 1, y: 1 }}
              >
                {renderBarContent()}
              </LinearGradient>
            ) : (
              renderBarContent()
            )}
          </BlurView>
        ) : (
          renderBarContent()
        )}
      </View>
    </View>
  );
}

function MoreStack() {
  return (
    <Stack.Navigator
      screenOptions={{
        headerShown: false,
        animation: 'slide_from_right',
        contentStyle: { backgroundColor: '#0A0A0A' },
      }}
    >
      <Stack.Screen 
        name="DahaFazlaMain" 
        component={DahaFazlaScreen} 
      />
      <Stack.Screen name="MusteriTakip" component={MusteriTakipScreen} />
      <Stack.Screen name="MusteriTakipDetay" component={MusteriTakipDetayScreen} />
      <Stack.Screen name="Kanban" component={KanbanScreen} />
      <Stack.Screen name="MaliyetHesaplama" component={MaliyetHesaplamaScreen} />
      <Stack.Screen name="Araclar" component={AraclarScreen} />
      <Stack.Screen name="Finans" component={FinansScreen} />
      <Stack.Screen name="Siparisler" component={SiparislerScreen} />
      <Stack.Screen name="Teklifler" component={TekliflerScreen} />
      <Stack.Screen name="VadeTakip" component={VadeTakipScreen} />
      <Stack.Screen name="Raporlar" component={RaporlarScreen} />
      <Stack.Screen name="RaporDetay" component={RaporDetayScreen} />
      <Stack.Screen name="Ayarlar" component={AyarlarScreen} />
      <Stack.Screen name="StokGrup" component={StokGrupScreen} />
    </Stack.Navigator>
  );
}

function MainTabs() {
  return (
    <Tab.Navigator
      tabBar={(props) => <CustomTabBar {...props} />}
      screenOptions={{
        headerShown: false,
        sceneStyle: { backgroundColor: '#0A0A0A' },
      }}
    >
      <Tab.Screen name="Dashboard" component={DashboardScreen} />
      <Tab.Screen name="Cariler" component={CarilerScreen} />
      <Tab.Screen name="Faturalar" component={FaturalarScreen} />
      <Tab.Screen name="Stoklar" component={StoklarScreen} />
      <Tab.Screen name="DahaFazla" component={MoreStack} />
    </Tab.Navigator>
  );
}

const customDarkTheme = {
  ...DarkTheme,
  colors: {
    ...DarkTheme.colors,
    background: '#0A0A0A',
    card: '#0A0A0A',
  },
};

export default function AppNavigator() {
  return (
    <NavigationContainer theme={customDarkTheme}>
      <RootStack.Navigator screenOptions={{ headerShown: false, contentStyle: { backgroundColor: '#0A0A0A' } }}>
        <RootStack.Screen name="MainTabs" component={MainTabs} />
        <RootStack.Screen 
          name="FaturaForm" 
          component={FaturaFormScreen} 
          options={{ presentation: 'card', animation: 'slide_from_right', gestureEnabled: true }}
        />
        <RootStack.Screen 
          name="SiparisForm" 
          component={SiparisFormScreen} 
          options={{ presentation: 'card', animation: 'slide_from_right', gestureEnabled: true }}
        />
        <RootStack.Screen 
          name="TeklifForm" 
          component={TeklifFormScreen} 
          options={{ presentation: 'card', animation: 'slide_from_right', gestureEnabled: true }}
        />
      </RootStack.Navigator>
    </NavigationContainer>
  );
}

const styles = StyleSheet.create({
  floatingContainer: {
    position: 'absolute',
    bottom: Platform.OS === 'ios' ? 22 : 14,
    left: BAR_MARGIN,
    right: BAR_MARGIN,
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 1000,
  },
  glassOuterWrapper: {
    borderRadius: 30,
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.5,
    shadowRadius: 16,
    elevation: 12,
  },
  barBlurView: {
    borderRadius: 32,
    backgroundColor: 'rgba(18, 18, 22, 0.92)',
  },
  gradientBorder: {
    borderRadius: 32,
    padding: 1,
  },
  barBackground: {
    flexDirection: 'row',
    backgroundColor: 'rgba(22, 22, 26, 0.88)',
    borderRadius: 31,
    paddingHorizontal: PADDING_H,
    width: BAR_WIDTH,
    height: 66,
    alignItems: 'center',
    justifyContent: 'space-around',
    overflow: 'hidden',
  },
  slidingPill: {
    position: 'absolute',
    left: 0,
    top: (66 - PILL_HEIGHT) / 2,
    borderRadius: 20,
    overflow: 'hidden',
  },
  pillGradient: {
    width: '100%',
    height: '100%',
    borderRadius: 18,
    backgroundColor: 'rgba(255, 255, 255, 0.14)',
  },
  pillFallback: {
    width: '100%',
    height: '100%',
    borderRadius: 18,
    backgroundColor: '#2A2A2D',
  },
  tabItem: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    height: '100%',
    zIndex: 2,
  },
  tabLabel: {
    fontSize: 9,
    marginTop: 2,
    textAlign: 'center',
  },
});

