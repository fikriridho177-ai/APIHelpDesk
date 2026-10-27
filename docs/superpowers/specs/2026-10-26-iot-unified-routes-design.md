# Desain: Penyatuan Halaman IoT Vendor dan Lokal

## Tujuan

Menyatukan halaman perangkat dan monitoring IoT Vendor serta Lokal menjadi halaman gabungan. Pengguna memilih cakupan data melalui filter sumber, bukan melalui halaman atau rute yang terpisah.

## Keputusan desain

Gunakan satu halaman kanonis untuk setiap fungsi:

- `/iot-management/daftar-perangkat` untuk registry seluruh perangkat.
- `/iot-management/monitoring-sensor` untuk monitoring seluruh sensor.

Kedua halaman menampilkan filter sumber:

- Semua sumber
- Vendor
- Internal/Lokal

Rute lama tetap dipertahankan sebagai alias yang mengarah ke halaman kanonis agar bookmark dan tautan lama tidak rusak. Tidak ada perubahan pada master menu, permission database, atau struktur API backend.

## Perubahan antarmuka

- Dashboard IoT hanya menampilkan shortcut gabungan: daftar perangkat, monitoring sensor, dan prakiraan cuaca.
- Shortcut Vendor/Lokal yang duplikatif di navbar disembunyikan atau dinormalisasi menjadi satu entry gabungan.
- Registry gabungan tetap menampilkan seluruh perangkat dan menyediakan filter sumber.
- Monitoring gabungan tetap menyediakan filter sumber dan mempertahankan detail sensor, parameter, serta histori.
- Breadcrumb menggunakan nama fungsi gabungan, bukan istilah Vendor/Lokal.

## Kompatibilitas rute

Rute lama untuk daftar perangkat dan monitoring Vendor/Lokal akan melakukan redirect ke rute kanonis. Redirect menggunakan penggantian history agar tombol Back tidak membuat pengguna kembali ke halaman duplikat.

## Batasan

- Data tetap berasal dari endpoint IoT yang sudah ada.
- Tidak mengubah data master menu dan permission di backend.
- Tidak mengubah halaman prakiraan cuaca atau integrasi BMKG.

## Validasi

1. Dashboard tidak lagi menampilkan kartu Vendor/Lokal terpisah.
2. Registry kanonis menampilkan data Vendor dan Internal sekaligus serta dapat difilter.
3. Monitoring kanonis menampilkan data lintas sumber serta dapat difilter.
4. Rute lama Vendor/Lokal mengarah ke halaman kanonis.
5. Build frontend dan pemeriksaan diagnostik tidak menghasilkan error baru.
