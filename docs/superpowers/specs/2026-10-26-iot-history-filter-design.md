# Desain: Filter History Monitoring IoT

## Intent

Menambahkan pembacaan historis harian, mingguan, bulanan, dan tahunan pada
satu halaman kanonis `/iot-management/monitoring-sensor`. Pengguna memilih
logger dan sumber seperti sekarang, lalu memilih rentang history tanpa
berpindah ke halaman baru.

## Goals

- Menyediakan empat periode operasional: 24 jam, 7 hari, 30 hari, dan 12 bulan.
- Menampilkan grafik yang tetap ringan dengan agregasi di backend.
- Mempertahankan pembacaan terbaru sebagai nilai aktual, terpisah dari nilai
  historis yang diringkas.
- Menjaga rute lama tetap diarahkan ke halaman monitoring gabungan.
- Mempertahankan prakiraan cuaca sebagai halaman khusus `/iot-management/prakiraan-cuaca`.

## Non-goals

- Tidak membuat halaman history baru untuk setiap periode.
- Tidak mengubah data sensor atau menyimpan hasil agregasi ke database.
- Tidak mengubah kontrak endpoint `latest` atau endpoint logger/parameter.
- Tidak menambahkan ekspor CSV/PDF pada tahap ini.

## Approaches

1. **Filter di browser dari 100 payload terakhir.** Implementasinya kecil,
   tetapi data bulanan dan tahunan akan tidak lengkap sehingga tidak dipilih.
2. **Mengambil semua payload mentah sesuai rentang.** Akurat, tetapi berisiko
   mengirim ribuan sampai puluhan ribu detail sensor ke browser.
3. **Rentang dan agregasi di backend (dipilih).** Backend mengambil data pada
   rentang periodenya, lalu mengembalikan titik numerik rata-rata per bucket:
   jam untuk harian, hari untuk mingguan/bulanan, dan bulan untuk tahunan.
   Nilai terbaru tetap diambil dari endpoint `latest`.

## API contract

Endpoint yang ada diperluas secara backward-compatible:

`GET /api/iot/devices/{loggerId}/history?period=day|week|month|year`

Response mempertahankan `loggerId`, `total`, dan `data`, lalu menambahkan
`period`, `aggregation`, `from`, `to`, dan `rawTotal`. Setiap item `data`
memakai bentuk pembacaan ringkas yang sama; `data` berisi nilai numerik rata-
rata per parameter dalam bucket dan timestamp bucket/representatif terbaru.

Request tanpa `period` tetap menggunakan perilaku lama berbasis `take` agar
tautan atau konsumen lama tidak rusak.

## UI decisions

- Kontrol periode berada di panel `HISTORICAL TELEMETRY`, dekat judul grafik.
- Label yang terlihat pengguna: `Harian · 24 jam`, `Mingguan · 7 hari`,
  `Bulanan · 30 hari`, `Tahunan · 12 bulan`.
- Subjudul grafik menampilkan jumlah titik hasil agregasi dan rentang waktunya.
- Format sumbu dan tooltip menyesuaikan bucket sehingga grafik tahunan tidak
  menampilkan jam/menit yang tidak bermakna.
- Saat logger atau periode berubah, hanya history dan metadata terkait logger
  yang dimuat ulang; ringkasan kesehatan tetap berasal dari data terbaru.

## Constraints and acceptance

- Period selain `day`, `week`, `month`, atau `year` menghasilkan HTTP 400.
- History tidak lagi dibatasi secara diam-diam menjadi 100 payload untuk mode
  periode.
- Tidak ada data history mengembalikan response kosong yang dapat ditampilkan
  sebagai state kosong, bukan error server.
- `dotnet test`, `dotnet build`, `npm run build`, dan smoke test browser harus
  berhasil.
