# IoT Monitoring Compact Observability

## Goal

Merapikan halaman monitoring IoT agar status logger, kualitas data, dan histori pembacaan setiap parameter dapat dipindai tanpa panel grafik besar yang mengulang informasi kartu sensor.

## Current problem

- Panel grafik utama mengulang informasi yang sudah ditampilkan pada grafik kecil di kartu parameter.
- Grafik utama dapat terlihat kosong ketika wrapper `ResponsiveContainer` tidak meneruskan ukuran ke chart internal atau ketika nilai sensor konstan.
- State kosong, error, dan loading masih terlihat seperti ruang kosong biasa.
- Menu Vendor dan Logger Internal membagi halaman yang sebenarnya memiliki pola monitoring sama.

## Design decisions

1. Hapus panel grafik utama dan selector parameter karena grafik per parameter sudah tersedia pada kartu sensor.
2. Pertahankan seluruh pembacaan numerik yang diterima dari endpoint dalam urutan timestamp asli untuk grafik histori. Tidak ada rata-rata atau pengurangan menjadi satu titik per hari; label kartu menyatakan jumlah pembacaan yang dirender.
3. Pertahankan nilai terbaru sebagai angka utama kartu dan tampilkan titik tunggal/konstan secara jelas agar tidak terlihat seperti chart rusak.
4. Gunakan satu halaman gabungan Vendor/Internal. Filter sumber menjadi `Semua`, `Vendor`, dan `Internal`; route lama tetap dapat menjadi alias kompatibilitas.
5. Pertahankan read-only API, permission, breadcrumb, dan kontrak endpoint yang sudah ada. Perubahan menu database dicatat pada migration SQL, bukan dijalankan otomatis.

## Scope

- Modify `src/features/iot/pages/IoTMonitoringPage.module.css`.
- Modify `src/features/iot/pages/IoTMonitoringPage.tsx` for raw timestamp history and simplified markup.
- Modify `src/features/iot/pages/IoTVendorDevicesPage.tsx` only to support the same combined source filter on the unified registry route.
- Modify frontend route/menu resolver and the IoT menu migration SQL to make the combined pages canonical.
- Do not modify `src/components/layout/AppBreadcrumb.tsx` unless the canonical route requires a new breadcrumb label.

## Acceptance criteria

- Tidak ada panel grafik utama yang mengulang kartu sensor.
- Setiap kartu parameter menampilkan semua pembacaan numerik yang diterima beserta timestamp, tanpa rata-rata harian.
- Nilai konstan atau satu titik tetap memiliki penanda visual yang terbaca.
- Vendor dan Internal dapat dipilih dari satu filter sumber pada halaman gabungan.
- Ringkasan sensor menampilkan lebih banyak kartu per viewport tanpa mengorbankan keterbacaan nilai.
- Layout tetap responsif pada lebar sekitar 640px dan tidak menghasilkan overflow horizontal pada halaman.
- `npm run build` berhasil.
