# Desain: Tanggal Acuan History IoT

## Intent

Menambahkan pilihan tanggal acuan pada panel history monitoring IoT. Pengguna
tetap memilih periode `day`, `week`, `month`, atau `year`; tanggal acuan
menentukan window data yang ditampilkan. Nilai awal tanggal mengikuti tanggal
hari ini.

## Temuan route

Route `/iot-management/daftar-perangkat` sudah terdaftar, tetapi halaman
meminta permission `DAFTAR_PERANGKAT_VENDOR`, sedangkan endpoint daftar logger
menggunakan gate `DASHBOARD_IOT`. Permission halaman menghasilkan 403 sebelum
data dimuat. Route kanonis akan memakai gate `DASHBOARD_IOT` yang sama dengan
API.

## Approaches

1. Tanpa tanggal manual: mempertahankan window relatif terhadap waktu sekarang.
2. Tanggal acuan: satu input tanggal, lalu periode menghitung window tetap.
3. Rentang bebas `from`/`to`: paling fleksibel, tetapi memperbesar kontrak,
   validasi, dan risiko query tidak terbatas.

Pendekatan 2 dipilih karena mempertahankan kontrol periode yang sudah ada,
memberi pengguna histori tanggal tertentu, dan tetap membatasi ukuran query.

## Window tanggal

Tanggal acuan diperlakukan sebagai tanggal kalender dan batas SQL menggunakan
interval setengah terbuka `[from, to)`:

- `day`: tanggal acuan sampai awal hari berikutnya.
- `week`: tanggal acuan dan enam hari sebelumnya sampai awal hari berikutnya.
- `month`: tanggal acuan dan 29 hari sebelumnya sampai awal hari berikutnya.
- `year`: awal bulan tanggal acuan dikurangi 11 bulan sampai awal bulan berikutnya.

Tanggal masa depan tidak ditawarkan UI. Jika `anchorDate` tidak dikirim pada
mode periode, API memakai tanggal hari ini agar klien lama tetap valid. Request
tanpa `period` mempertahankan perilaku `take` lama.

## API dan UI

- Period mode menerima `anchorDate=YYYY-MM-DD` selain `period`.
- Tanggal invalid menghasilkan HTTP 400.
- Response tetap mengembalikan `period`, `aggregation`, `from`, `to`,
  `total`, `rawTotal`, dan `data`.
- Selector tanggal berada dekat selector periode dan mengirim ulang history
  saja; ringkasan latest tidak ikut dimuat ulang.
- Range summary dan label chart tetap mengikuti aggregation bucket.
- Guard request lama tetap dipertahankan saat logger, periode, atau tanggal
  berubah.

## Testing

Tambahkan unit test untuk window setiap periode, batas tanggal, default tanggal,
tanggal invalid, serta regresi no-period. Verifikasi frontend build dan smoke
test route registry serta perubahan tanggal/periode.
