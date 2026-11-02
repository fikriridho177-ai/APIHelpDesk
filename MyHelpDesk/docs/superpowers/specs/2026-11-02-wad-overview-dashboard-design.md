+# Desain Hub Layanan WAD

## Intent

Saat pengguna memilih kartu WAD dari katalog layanan, halaman /ecs menjadi ruang kerja untuk memilih layanan Water Management. Diagram yang diberikan menjadi urutan informasi, bukan bentuk visual yang disalin mentah sebagai kotak dan garis.

## Tujuan

- Menampilkan identitas WAD sebagai Water Management dengan penjelasan singkat.
- Menampilkan enam layanan dalam urutan yang tetap:
  1. Pantauan TMAT
  2. Pantauan TMAK
  3. Kondisi Pintu Air
  4. Kondisi Siphon / Gorong-gorong
  5. Pengecekan Subsiden Gambut
  6. Pengecekan Curah Hujan
- Membuat halaman terasa seperti ruang kerja aplikasi monitoring, bukan dokumen atau flowchart.
- Mempertahankan halaman sensor yang sudah ada sebagai detail/tujuan layanan yang relevan.
- Menyediakan state Segera tersedia untuk layanan yang belum memiliki sumber data atau halaman khusus.

## Bukan Tujuan

- Tidak membuat API, tabel, atau endpoint baru pada tahap hub layanan.
- Tidak menghapus halaman monitoring ECS yang sudah ada.
- Tidak menampilkan ringkasan sensor di landing WAD sebelum layanan spesifik dipilih.
- Tidak mengklaim layanan yang belum memiliki sumber data sebagai monitoring aktif.
- Tidak mengubah integrasi GIS, navbar, atau hierarki route lama.

## Pengalaman yang Dipilih

Halaman /ecs memiliki urutan berikut:

1. Header WAD dengan identitas Water Management di sisi kiri dan panel status ringkas di sisi kanan.
2. Area Ruang kerja layanan dengan indikator jumlah layanan aktif dan jumlah seluruh layanan.
3. Enam service tile dalam grid responsif. Setiap tile memiliki nomor urut, ikon, judul, deskripsi, status, dan aksi.
4. Tile aktif menggunakan hover/focus state dan membuka halaman yang tersedia:
   - Pantauan TMAT → daftar stasiun monitoring.
   - Pantauan TMAK → halaman TMAK.
   - Pengecekan Curah Hujan → halaman curah hujan.
5. Tile yang belum tersedia tetap terlihat tetapi tidak interaktif dan diberi status Segera tersedia.
6. Catatan kecil menjelaskan bahwa data sensor muncul setelah layanan pemantauan dipilih.

## Data dan Logika

- Definisi layanan disimpan sebagai konfigurasi frontend terpisah agar urutan, label, ikon, status, dan route mudah dirawat.
- Route tujuan menggunakan route ECS yang sudah tersedia.
- Layanan tanpa route diberi available: false dan tidak dirender sebagai link.
- Tidak ada fetch data sensor pada landing WAD. Fetch tetap terjadi di halaman layanan monitoring setelah pengguna memilih layanan.

## Batasan Teknis

- Pertahankan permission gate WAD pada halaman /ecs.
- Gunakan komponen dan CSS module ECS yang sudah ada.
- Jangan mengubah integrasi GIS, navbar, API, database, atau route yang sudah ada.
- Jangan menghapus alias route IoT lama yang masih diperlukan untuk bookmark/permission.
- Layanan yang belum tersedia harus terlihat jelas sebagai belum tersedia, bukan error teknis.

## Verifikasi

- npm run build harus berhasil.
- Tidak ada error TypeScript/diagnostic pada file yang disentuh.
- Uji manual halaman /ecs pada desktop dan viewport sempit: enam layanan terlihat, route aktif membuka halaman yang benar, dan layanan belum tersedia tidak dapat diklik.
