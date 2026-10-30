// Halaman divisi (/divisi/:slug): masalah yang ditangani divisi + menu Master / Transaksi / Monitoring.
// Menu di bawah ditulis berdasarkan KODE menu, sehingga nama & route tetap mengikuti Manajemen Menu,
// dan menu hanya tampil bila user punya hak (daftar navigasi sudah difilter hak akses).
// Saat ini RNA & GIS. Divisi lain (WAD, QC, NAT) tetap ke halaman tiket layanan.

export type DivisiSection = {
  key: 'master' | 'transaksi' | 'monitoring'
  title: string
  hint: string
  menuCodes: string[]
  labels?: Record<string, string> // ganti nama menu khusus di halaman ini
}

export type DivisiConfig = {
  slug: string
  divisionCode: string // menu divisi di LAYANAN (hak View = boleh membuka halaman divisi). Nama, nama lengkap & deskripsi diambil dari menu ini.
  problemTitle: string
  // Tab "Map Workspace" (viewer WebGIS) di panel divisi; tampil bila user punya hak Read di menu ini.
  mapWorkspaceMenu?: string
  sections: DivisiSection[]
}

export const DIVISI_CONFIG: DivisiConfig[] = [
  {
    slug: 'rna',
    divisionCode: 'LYN_RNA',
    problemTitle: 'Masalah yang ditangani',
    sections: [
      { key: 'master', title: 'Master', hint: 'Data acuan sensus', menuCodes: ['SNS_MASTER_JENIS', 'SNS_MASTER_LOKASI'] },
      {
        key: 'transaksi',
        title: 'Transaksi',
        hint: 'Input & persetujuan sensus',
        menuCodes: ['LYN_RNA', 'SNS_APPROVAL'],
        // Form layanan RNA dipakai sementara untuk input data sampai aplikasi Android siap.
        labels: { LYN_RNA: 'Input data (sementara, sebelum Android)' },
      },
      { key: 'monitoring', title: 'Monitoring', hint: 'Pantau & analisis hasil', menuCodes: ['SNS_MONITORING', 'SNS_ANALISIS', 'GIS_PETA'] },
    ],
  },
  {
    slug: 'gis',
    divisionCode: 'LYN_GIS',
    problemTitle: 'Pekerjaan yang ditangani',
    mapWorkspaceMenu: 'GIS_PETA',
    sections: [
      { key: 'master', title: 'Master', hint: 'Data acuan pekerjaan', menuCodes: ['PMT_MASTER_JENIS'] },
      {
        key: 'transaksi',
        title: 'Transaksi',
        hint: 'Permintaan & persetujuan',
        menuCodes: ['LYN_GIS', 'PMT_APPROVAL'],
        labels: { LYN_GIS: 'Form permintaan & daftar tiket GIS' },
      },
      { key: 'monitoring', title: 'Monitoring', hint: 'Progres pekerjaan', menuCodes: ['PMT_MONITORING', 'PMT_ANALISIS'] },
    ],
  },
]

// Untuk kartu Katalog di dashboard: divisi yang punya halaman divisi.
export const DIVISI_ROUTES: Record<string, string> = Object.fromEntries(
  DIVISI_CONFIG.map(c => [c.divisionCode, `/divisi/${c.slug}`]),
)
