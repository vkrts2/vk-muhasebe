import React, { useState, useEffect } from 'react';
import { StyleSheet, Text, View, SafeAreaView, FlatList, TextInput, ActivityIndicator, TouchableOpacity, Modal, ScrollView, Alert, Keyboard } from 'react-native';
import { Search, Plus, X, Save, Trash2, Calendar, User, Clock, AlertCircle, LayoutGrid, CheckCircle2 } from 'lucide-react-native';
import { subscribeToPath, writeData, deleteData } from '../services/firebase';
import { generateInt32Id } from '../utils/IdGenerator';
import { KeyboardDismissBar } from '../components/KeyboardDismissBar';

export interface Gorev {
  id: number;
  baslik: string;
  aciklama?: string;
  atananPersonelId: number;
  atananPersonelAd?: string;
  durum: string; // 'Yapılacak' | 'Yapılıyor' | 'Bitti'
  oncelik: string; // 'Düşük' | 'Normal' | 'Acil'
  sonTarih: string;
  bitisTarihi?: string;
  olusturmaTarihi: string;
}

export default function KanbanScreen() {
  const [gorevler, setGorevler] = useState<Gorev[]>([]);
  const [personeller, setPersoneller] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');
  const [activeTab, setActiveTab] = useState<'Yapılacak' | 'Yapılıyor' | 'Bitti'>('Yapılacak');

  // Modals state
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [isDetailOpen, setIsDetailOpen] = useState(false);
  const [selectedGorev, setSelectedGorev] = useState<Gorev | null>(null);

  // Form fields state
  const [baslik, setBaslik] = useState('');
  const [aciklama, setAciklama] = useState('');
  const [oncelik, setOncelik] = useState('Normal');
  const [sonTarih, setSonTarih] = useState(new Date().toISOString().split('T')[0]);
  const [bitisTarihi, setBitisTarihi] = useState('');
  const [personelAd, setPersonelAd] = useState('Yönetici');
  const [personelId, setPersonelId] = useState(1);

  useEffect(() => {
    const unsub = subscribeToPath('Gorevler', (data) => {
      if (!data) {
        setGorevler([]);
      } else {
        const list = Array.isArray(data) 
          ? data.filter(Boolean) 
          : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key }));
        setGorevler(list);
      }
      setLoading(false);
    });

    const unsubPersonel = subscribeToPath('Personeller', (data) => {
      if (!data) {
        setPersoneller([]);
      } else {
        setPersoneller(Array.isArray(data) ? data.filter(Boolean) : Object.keys(data).map(key => ({ ...data[key], firebaseKey: key })));
      }
    });

    return () => {
      unsub();
      unsubPersonel();
    };
  }, []);

  const filteredGorevler = gorevler.filter(g => 
    (g.durum || 'Yapılacak') === activeTab &&
    ((g.baslik || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR')) ||
    (g.aciklama || '').toLocaleLowerCase('tr-TR').includes(searchQuery.toLocaleLowerCase('tr-TR')))
  );

  const resetForm = () => {
    setBaslik('');
    setAciklama('');
    setOncelik('Normal');
    setSonTarih(new Date().toISOString().split('T')[0]);
    setBitisTarihi('');
    setPersonelAd('Yönetici');
    setPersonelId(1);
  };

  const handleOpenAdd = () => {
    resetForm();
    setIsFormOpen(true);
  };

  const handleSave = async () => {
    if (!baslik.trim()) {
      Alert.alert('Hata', 'Lütfen görev başlığını giriniz.');
      return;
    }

    const currentId = generateInt32Id();
    const newGorev: Gorev = {
      id: currentId,
      baslik: baslik.trim(),
      aciklama: aciklama.trim(),
      atananPersonelId: personelId,
      atananPersonelAd: personelAd.trim(),
      durum: 'Yapılacak',
      oncelik,
      sonTarih,
      bitisTarihi: bitisTarihi || '',
      olusturmaTarihi: new Date().toISOString().split('T')[0]
    };

    const success = await writeData(`Gorevler/${currentId}`, newGorev);
    if (success) {
      setIsFormOpen(false);
      resetForm();
      Alert.alert('Başarılı', 'Görev panoya eklendi.');
    } else {
      Alert.alert('Hata', 'Görev kaydedilirken bir hata oluştu.');
    }
  };

  const updateTaskStatus = async (task: Gorev, newStatus: string) => {
    const today = new Date().toISOString().split('T')[0];
    const updatedTask: Gorev = {
      ...task,
      durum: newStatus,
      bitisTarihi: task.bitisTarihi || (newStatus === 'Bitti' ? today : task.bitisTarihi),
    };
    const success = await writeData(`Gorevler/${task.id}`, updatedTask);
    if (success) {
      if (selectedGorev?.id === task.id) {
        setSelectedGorev(updatedTask);
      }
      Alert.alert('Başarılı', `Görev "${newStatus}" sütununa taşındı.`);
    } else {
      Alert.alert('Hata', 'Görev durumu güncellenemedi.');
    }
  };

  const handleDelete = async (task: Gorev) => {
    Alert.alert(
      'Görevi Sil',
      'Bu görevi silmek istediğinize emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        { 
          text: 'Evet, Sil', 
          style: 'destructive',
          onPress: async () => {
            const success = await deleteData(`Gorevler/${task.id}`);
            if (success) {
              setIsDetailOpen(false);
              Alert.alert('Başarılı', 'Görev silindi.');
            } else {
              Alert.alert('Hata', 'Görev silinemedi.');
            }
          }
        }
      ]
    );
  };

  const renderItem = ({ item }: { item: Gorev }) => {
    let priorityColor = '#00FF87';
    if (item.oncelik === 'Normal') priorityColor = '#F59E0B';
    if (item.oncelik === 'Acil') priorityColor = '#EF4444';

    return (
      <TouchableOpacity 
        style={styles.card}
        onPress={() => {
          setSelectedGorev(item);
          setIsDetailOpen(true);
        }}
      >
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <Text style={styles.cardTitle}>{item.baslik}</Text>
          <View style={[styles.priorityBadge, { backgroundColor: `${priorityColor}15`, borderColor: `${priorityColor}30` }]}>
            <Text style={[styles.priorityText, { color: priorityColor }]}>{item.oncelik}</Text>
          </View>
        </View>
        
        {item.aciklama ? (
          <Text style={styles.cardDesc} numberOfLines={2}>{item.aciklama}</Text>
        ) : null}

        <View style={styles.cardFooter}>
          <View style={styles.footerItem}>
            <User color="#64748B" size={14} />
            <Text style={styles.footerText}>{item.atananPersonelAd || 'Atanmamış'}</Text>
          </View>
          <View style={styles.footerItem}>
            <Calendar color="#64748B" size={14} />
            <Text style={styles.footerText}>{item.sonTarih}</Text>
          </View>
        </View>
      </TouchableOpacity>
    );
  };

  return (
    <SafeAreaView style={styles.container}>
      <View style={styles.header}>
        <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <View style={{ flexDirection: 'row', alignItems: 'center' }}>
            <LayoutGrid color="#00FF87" size={24} style={{ marginRight: 8 }} />
            <Text style={styles.headerTitle}>İş Takip Panosu</Text>
          </View>
          <TouchableOpacity style={styles.addButton} onPress={handleOpenAdd}>
            <Plus color="#FFF" size={20} />
            <Text style={styles.addButtonText}>Yeni</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.segmentContainer}>
          {['Yapılacak', 'Yapılıyor', 'Bitti'].map((col) => (
            <TouchableOpacity 
              key={col}
              style={[styles.segmentBtn, activeTab === col && styles.segmentBtnActive]}
              onPress={() => setActiveTab(col as any)}
            >
              <Text style={[styles.segmentText, activeTab === col && styles.segmentTextActive]}>
                {col}
              </Text>
            </TouchableOpacity>
          ))}
        </View>

        <View style={styles.searchBox}>
          <Search color="#64748B" size={20} />
          <TextInput 
            style={styles.searchInput} 
            placeholder="Görev veya açıklama ara..." 
            placeholderTextColor="#64748B"
            value={searchQuery}
            onChangeText={setSearchQuery}
            returnKeyType="search"
            onSubmitEditing={() => Keyboard.dismiss()}
          />
          {searchQuery ? (
            <TouchableOpacity onPress={() => { setSearchQuery(''); Keyboard.dismiss(); }}>
              <X color="#94A3B8" size={18} />
            </TouchableOpacity>
          ) : null}
        </View>
      </View>

      {loading ? (
        <View style={styles.center}>
          <ActivityIndicator size="large" color="#0061FF" />
        </View>
      ) : (
        <FlatList initialNumToRender={20} maxToRenderPerBatch={20} windowSize={5} 
          data={filteredGorevler}
          keyExtractor={(item) => item.id.toString()}
          renderItem={renderItem}
          contentContainerStyle={styles.listContent}
          ListEmptyComponent={<Text style={styles.emptyText}>Bu sütunda görev bulunmuyor.</Text>}
        />
      )}

      {/* Görev Ekleme Modalı */}
      <Modal visible={isFormOpen} animationType="slide" transparent={true}>
        <SafeAreaView style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Yeni Görev Oluştur</Text>
              <TouchableOpacity onPress={() => setIsFormOpen(false)}>
                <X color="#FFF" size={24} />
              </TouchableOpacity>
            </View>

            <ScrollView contentContainerStyle={styles.formScroll}>
              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Görev Başlığı *</Text>
                <TextInput 
                  style={styles.input} 
                  placeholder="Görev adı..." 
                  placeholderTextColor="#64748B"
                  value={baslik}
                  onChangeText={setBaslik}
                />
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Görev Açıklaması</Text>
                <TextInput 
                  style={[styles.input, { height: 80 }]} 
                  placeholder="Detaylar..." 
                  placeholderTextColor="#64748B"
                  multiline={true}
                  value={aciklama}
                  onChangeText={setAciklama}
                />
              </View>

              <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Öncelik</Text>
                  <View style={styles.toggleRow}>
                    {['Düşük', 'Normal', 'Acil'].map((p) => (
                      <TouchableOpacity 
                        key={p}
                        style={[styles.toggleBtn, oncelik === p && styles.toggleBtnActive, oncelik === p && p === 'Acil' && { backgroundColor: '#EF4444' }]}
                        onPress={() => setOncelik(p)}
                      >
                        <Text style={[styles.toggleBtnText, oncelik === p && styles.toggleBtnTextActive]}>{p}</Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                </View>
                <View style={[styles.inputGroup, { width: '48%' }]}>
                  <Text style={styles.inputLabel}>Son Tarih</Text>
                  <TextInput 
                    style={styles.input} 
                    placeholder="YYYY-MM-DD" 
                    placeholderTextColor="#64748B"
                    value={sonTarih}
                    onChangeText={setSonTarih}
                  />
                </View>
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Sorumlu Personel</Text>
                <TextInput 
                  style={styles.input} 
                  placeholder="İsim soyisim..." 
                  placeholderTextColor="#64748B"
                  value={personelAd}
                  onChangeText={setPersonelAd}
                />
                {personeller.length > 0 && (
                  <View style={styles.chipRow}>
                    {personeller.map((p) => {
                      const ad = p.adSoyad || `${p.ad || ''} ${p.soyad || ''}`.trim();
                      const pId = Number(p.id);
                      return (
                        <TouchableOpacity
                          key={pId}
                          style={[styles.chip, personelId === pId && styles.chipActive]}
                          onPress={() => {
                            setPersonelId(pId);
                            setPersonelAd(ad);
                          }}
                        >
                          <Text style={[styles.chipText, personelId === pId && styles.chipTextActive]}>{ad}</Text>
                        </TouchableOpacity>
                      );
                    })}
                  </View>
                )}
              </View>

              <View style={styles.inputGroup}>
                <Text style={styles.inputLabel}>Bitiş Tarihi</Text>
                <TextInput 
                  style={styles.input} 
                  placeholder="YYYY-MM-DD (bitince otomatik dolar)" 
                  placeholderTextColor="#64748B"
                  value={bitisTarihi}
                  onChangeText={setBitisTarihi}
                />
              </View>

              <TouchableOpacity style={styles.saveButton} onPress={handleSave}>
                <Save color="#FFF" size={20} />
                <Text style={styles.saveButtonText}>Görevi Kaydet</Text>
              </TouchableOpacity>
            </ScrollView>
          </View>
          <KeyboardDismissBar inModal={true} />
        </SafeAreaView>
      </Modal>

      {/* Görev Detay Modalı */}
      <Modal visible={isDetailOpen} transparent={true} animationType="fade">
        <SafeAreaView style={styles.modalOverlay}>
          <View style={[styles.modalContent, { justifyContent: 'center', padding: 25 }]}>
            {selectedGorev && (
              <View style={styles.detailBox}>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle} numberOfLines={1}>{selectedGorev.baslik}</Text>
                  <TouchableOpacity onPress={() => setIsDetailOpen(false)}>
                    <X color="#FFF" size={24} />
                  </TouchableOpacity>
                </View>

                <ScrollView style={{ maxHeight: 200, marginBottom: 16 }}>
                  <Text style={styles.detailDesc}>{selectedGorev.aciklama || 'Açıklama belirtilmemiş.'}</Text>
                </ScrollView>

                <View style={styles.detailMetaGrid}>
                  <View style={styles.metaRow}>
                    <Text style={styles.metaLabel}>Personel:</Text>
                    <Text style={styles.metaVal}>{selectedGorev.atananPersonelAd || '-'}</Text>
                  </View>
                  <View style={styles.metaRow}>
                    <Text style={styles.metaLabel}>Tarih:</Text>
                    <Text style={styles.metaVal}>{selectedGorev.sonTarih}</Text>
                  </View>
                  {selectedGorev.bitisTarihi ? (
                    <View style={styles.metaRow}>
                      <Text style={styles.metaLabel}>Bitiş:</Text>
                      <Text style={[styles.metaVal, { color: '#00FF87' }]}>{selectedGorev.bitisTarihi}</Text>
                    </View>
                  ) : null}
                  <View style={styles.metaRow}>
                    <Text style={styles.metaLabel}>Öncelik:</Text>
                    <Text style={[styles.metaVal, { fontWeight: 'bold' }]}>{selectedGorev.oncelik}</Text>
                  </View>
                </View>

                {/* Sütunlar Arası Taşıma Butonları */}
                <Text style={[styles.inputLabel, { marginTop: 16, marginBottom: 8 }]}>Durumu Güncelle</Text>
                <View style={{ flexDirection: 'row', justifyContent: 'space-between', marginBottom: 20 }}>
                  {['Yapılacak', 'Yapılıyor', 'Bitti'].map((st) => (
                    <TouchableOpacity
                      key={st}
                      style={[styles.statusBtn, selectedGorev.durum === st && styles.statusBtnActive]}
                      onPress={() => updateTaskStatus(selectedGorev, st)}
                    >
                      <Text style={[styles.statusBtnText, selectedGorev.durum === st && styles.statusBtnTextActive]}>{st}</Text>
                    </TouchableOpacity>
                  ))}
                </View>

                <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
                  <TouchableOpacity style={[styles.actionBtn, { backgroundColor: '#EF4444', flex: 1, marginRight: 8 }]} onPress={() => handleDelete(selectedGorev)}>
                    <Trash2 color="#FFF" size={18} />
                    <Text style={styles.actionBtnText}>Görevi Sil</Text>
                  </TouchableOpacity>
                </View>
              </View>
            )}
          </View>
        </SafeAreaView>
      </Modal>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0A0A0A',
  },
  center: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  header: {
    padding: 20,
    paddingTop: 40,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255, 255, 255, 0.08)',
  },
  headerTitle: {
    fontSize: 22,
    fontWeight: '900',
    color: '#FFFFFF',
  },
  addButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#0061FF',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 12,
  },
  addButtonText: {
    color: '#FFF',
    fontWeight: 'bold',
    marginLeft: 6,
    fontSize: 14,
  },
  segmentContainer: {
    flexDirection: 'row',
    backgroundColor: '#161616',
    borderRadius: 12,
    padding: 4,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
    marginBottom: 16,
  },
  segmentBtn: {
    flex: 1,
    paddingVertical: 10,
    alignItems: 'center',
    borderRadius: 8,
  },
  segmentBtnActive: {
    backgroundColor: 'rgba(0, 97, 255, 0.15)',
    borderWidth: 1,
    borderColor: 'rgba(0, 97, 255, 0.3)',
  },
  segmentText: {
    color: '#64748B',
    fontWeight: 'bold',
    fontSize: 12,
  },
  segmentTextActive: {
    color: '#60A5FA',
  },
  searchBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#2A2A2A',
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#444',
    paddingHorizontal: 12,
  },
  searchInput: {
    flex: 1,
    paddingVertical: 12,
    paddingHorizontal: 8,
    color: '#FFFFFF',
    fontSize: 15,
  },
  listContent: {
    padding: 20,
  },
  emptyText: {
    color: '#64748B',
    textAlign: 'center',
    marginTop: 40,
  },
  card: {
    backgroundColor: '#0F0F0F',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    padding: 16,
    marginBottom: 12,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#FFFFFF',
    flex: 1,
    marginRight: 8,
  },
  priorityBadge: {
    borderWidth: 1,
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 6,
  },
  priorityText: {
    fontSize: 10,
    fontWeight: 'bold',
  },
  cardDesc: {
    color: '#94A3B8',
    fontSize: 13,
    marginTop: 8,
    lineHeight: 18,
  },
  cardFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    borderTopWidth: 1,
    borderTopColor: 'rgba(255, 255, 255, 0.05)',
    paddingTop: 12,
    marginTop: 12,
  },
  footerItem: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  footerText: {
    color: '#64748B',
    fontSize: 11,
    marginLeft: 6,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.85)',
  },
  modalContent: {
    flex: 1,
    backgroundColor: '#0A0A0A',
    padding: 20,
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 20,
    borderBottomWidth: 1,
    borderBottomColor: 'rgba(255,255,255,0.08)',
    paddingBottom: 16,
  },
  modalTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#FFF',
  },
  formScroll: {
    paddingBottom: 40,
  },
  inputGroup: {
    marginBottom: 16,
  },
  inputLabel: {
    color: '#94A3B8',
    fontSize: 13,
    fontWeight: '600',
    marginBottom: 8,
  },
  input: {
    backgroundColor: '#2A2A2A',
    borderRadius: 12,
    padding: 14,
    color: '#FFF',
    fontSize: 15,
    borderWidth: 1,
    borderColor: '#444',
  },
  toggleRow: {
    flexDirection: 'row',
    backgroundColor: '#2A2A2A',
    borderRadius: 12,
    padding: 4,
    height: 48,
    alignItems: 'center',
  },
  toggleBtn: {
    flex: 1,
    height: '100%',
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 8,
  },
  toggleBtnActive: {
    backgroundColor: '#0061FF',
  },
  toggleBtnText: {
    color: '#94A3B8',
    fontWeight: 'bold',
    fontSize: 12,
  },
  toggleBtnTextActive: {
    color: '#FFF',
  },
  chipRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    marginTop: 8,
    gap: 8,
  },
  chip: {
    backgroundColor: '#161616',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 20,
  },
  chipActive: {
    backgroundColor: 'rgba(0, 97, 255, 0.2)',
    borderColor: '#0061FF',
  },
  chipText: {
    color: '#94A3B8',
    fontSize: 12,
  },
  chipTextActive: {
    color: '#0061FF',
    fontWeight: 'bold',
  },
  saveButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#10B981',
    borderRadius: 12,
    padding: 16,
    marginTop: 20,
    marginBottom: 20,
  },
  saveButtonText: {
    color: '#FFF',
    fontWeight: 'bold',
    fontSize: 16,
    marginLeft: 8,
  },
  detailBox: {
    backgroundColor: '#161616',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
    borderRadius: 20,
    padding: 20,
  },
  detailDesc: {
    color: '#94A3B8',
    fontSize: 14,
    lineHeight: 22,
  },
  detailMetaGrid: {
    borderTopWidth: 1,
    borderTopColor: 'rgba(255,255,255,0.05)',
    paddingTop: 16,
  },
  metaRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  metaLabel: {
    color: '#64748B',
    fontSize: 13,
  },
  metaVal: {
    color: '#FFF',
    fontSize: 13,
  },
  statusBtn: {
    flex: 1,
    backgroundColor: '#161616',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.08)',
    borderRadius: 8,
    paddingVertical: 10,
    alignItems: 'center',
    marginHorizontal: 4,
  },
  statusBtnActive: {
    backgroundColor: 'rgba(0, 97, 255, 0.15)',
    borderColor: '#0061FF',
  },
  statusBtnText: {
    color: '#64748B',
    fontWeight: 'bold',
    fontSize: 11,
  },
  statusBtnTextActive: {
    color: '#60A5FA',
  },
  actionBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: 12,
    borderRadius: 10,
    marginTop: 10,
  },
  actionBtnText: {
    color: '#FFF',
    fontWeight: 'bold',
    marginLeft: 6,
    fontSize: 14,
  }
});
