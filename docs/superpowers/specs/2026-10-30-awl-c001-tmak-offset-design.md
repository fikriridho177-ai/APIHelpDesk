# Penyesuaian TMAK AWL-C001

## Intent

Pada halaman Monitoring Sensor, tampilkan nilai TMAK untuk logger `AWL-C001`
dengan penyesuaian `nilai asli + 10`. Nilai sumber dari API dan database tidak
diubah.

## Goals

- Menerapkan penyesuaian hanya untuk logger `AWL-C001`.
- Menerapkan penyesuaian hanya pada parameter TMAK.
- Menyamakan nilai pada kartu nilai terbaru dan grafik riwayat.
- Membiarkan logger lain, parameter lain, API, dan database tetap apa adanya.

## Non-goals and constraints

- Tidak mengubah query API atau data SQL Server.
- Tidak menerapkan offset ke layar frontend lain di luar halaman Monitoring
  Sensor.
- `+10` berarti menambahkan 10 ke nilai TMAK asli, bukan mengganti nilainya
  menjadi angka tetap 10.

## Approaches

1. Mengubah data di API/backend: ditolak karena menyentuh perilaku API dan
   melampaui batasan pengguna.
2. Menormalisasi respons di shared frontend API: tidak dipilih karena dapat
   mengubah semua layar yang memakai data IoT.
3. Menyesuaikan nilai di halaman Monitoring Sensor saat membaca data terbaru
   dan riwayat: dipilih karena cakupannya paling sempit dan tidak menyentuh
   API/DB.

## Implementation and verification

Tambahkan helper frontend yang mengenali logger `AWL-C001` dan parameter TMAK,
lalu gunakan helper tersebut untuk nilai terbaru dan titik grafik. Uji dengan
test untuk logger/parameter yang cocok dan tidak cocok, kemudian jalankan test
serta build frontend.
