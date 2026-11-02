# Desain Hub Layanan WAD

## Intent

Saat pengguna memilih kartu WAD dari katalog layanan, halaman `/ecs` harus menjadi pusat pilihan layanan WAD. Alurnya mengikuti rancangan: `Pilih Layanan → WAD → enam layanan operasional`. Halaman depan WAD tidak boleh terasa seperti dashboard IoT atau daftar menu Manajemen IoT.

## Tujuan

- Menampilkan identitas WAD sebagai `Water Management` dengan penjelasan singkat.
- Menampilkan enam pilihan layanan secara jelas dan berurutan:
  1. Pantauan TMAT
  2. Pantauan TMAK
  3. Kondisi Pintu Air
  4. Kondisi Siphon / Gorong-gorong
  5. Pengecekan Subsiden Gambut
  6. Pengecekan Curah Hujan
- Membuat setiap pilihan menjadi titik masuk layanan yang dapat dikembangkan tanpa mengubah alur kartu WAD.
- Mempertahankan halaman sensor yang sudah ada sebagai detail/tujuan layanan yang relevan.
- Menyediakan state “modul disiapkan” untuk layanan yang belum memiliki sumber data atau halaman khusus.

## Bukan Tujuan

- Tidak membuat API, tabel, atau endpoint baru pada tahap hub layanan.
- Tidak menghapus halaman monitoring ECS yang sudah ada.
- Tidak menampilkan kartu ringkasan sensor di landing WAD sebelum layanan spesifik dipilih.
- Tidak mengklaim layanan yang belum memiliki sumber data sebagai monitoring aktif.

## Pengalaman yang Dipilih

Halaman `/ecs` akan memiliki urutan berikut:

1. Header ringkas dengan breadcrumb konteks `Pilih Layanan / WAD`, label `Water Management`, dan deskripsi pekerjaan WAD.
2. Area hub dengan node WAD sebagai sumber alur.
3. Daftar enam layanan berbentuk baris pilihan yang konsisten, memiliki nomor, ikon, judul, deskripsi singkat, status modul, dan panah tujuan.
4. Layanan aktif membuka halaman yang sudah tersedia:
   - Pantauan TMAT → daftar stasiun monitoring untuk memilih logger/lokasi.
   - Pantauan TMAK → halaman TMAK yang sudah ada.
   - Pengecekan Curah Hujan → halaman curah hujan yang sudah ada.
5. Layanan yang belum memiliki halaman khusus tetap terlihat sebagai pilihan, tetapi diberi status `Segera tersedia` dan tidak mengarah ke halaman palsu.
6. Navigasi IoT lama tidak ditampilkan sebagai isi utama hub WAD.

## Data dan Logika

- Definisi layanan disimpan sebagai konfigurasi frontend terpisah agar urutan, label, ikon, status, dan route mudah dirawat.
- Route tujuan menggunakan route ECS yang sudah tersedia; layanan yang belum punya route diberi `available: false`.
- Tidak ada fetch data sensor pada landing WAD. Fetch tetap terjadi di halaman layanan monitoring setelah pengguna memilih layanan.

## Batasan Teknis

- Pertahankan permission gate WAD pada halaman `/ecs`.
- Gunakan komponen dan CSS module ECS yang sudah ada.
- Jangan mengubah integrasi GIS, navbar, API, database, atau route yang sudah ada.
- Jangan menghapus alias route IoT lama yang masih diperlukan untuk bookmark/permission.
- Layanan yang belum tersedia harus terlihat jelas sebagai belum tersedia, bukan error teknis.

## Verifikasi

- `npm run build` harus berhasil.
- Tidak ada error TypeScript/diagnostic pada file yang disentuh.
- Uji manual halaman `/ecs` pada desktop dan viewport sempit: enam layanan terlihat, dua route aktif membuka halaman yang benar, dan layanan belum tersedia tidak dapat diklik.
