import type { ComponentType } from 'react'

import type { WidgetProps, WidgetSize } from './types/portal'
import AksesCepatWidget from './widgets/AksesCepatWidget'
import AntrianWidget from './widgets/AntrianWidget'
import KatalogWidget from './widgets/KatalogWidget'
import KpiDivisiWidget from './widgets/KpiDivisiWidget'
import RingkasanTiketWidget from './widgets/RingkasanTiketWidget'
import TiketSayaWidget from './widgets/TiketSayaWidget'

// Kode menu WIDGET (UtlMenu) -> komponen & ukuran.
// Menambah widget baru: buat komponennya, daftarkan di sini, lalu tambahkan menu WIDGET di Manajemen Menu.
export const WIDGETS: Record<string, { component: ComponentType<WidgetProps>; size: WidgetSize }> = {
  WGT_KATALOG: { component: KatalogWidget, size: 'full' },
  WGT_ANTRIAN: { component: AntrianWidget, size: 'full' },
  WGT_TIKET_SAYA: { component: TiketSayaWidget, size: 'half' },
  WGT_AKSES_CEPAT: { component: AksesCepatWidget, size: 'half' },
  WGT_RINGKASAN_TIKET: { component: RingkasanTiketWidget, size: 'half' },
  WGT_KPI_DIVISI: { component: KpiDivisiWidget, size: 'half' },
}
