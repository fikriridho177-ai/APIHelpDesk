# Desain Peta Monitoring IoT dan Integrasi Prakiraan Cuaca

Tanggal: 2026-10-26  
Status: Menunggu persetujuan implementasi

## 1. Tujuan

Menambahkan halaman **Peta Monitoring IoT** sebagai pusat pemantauan perangkat berbasis lokasi, merapikan navigasi IoT, dan menampilkan prakiraan cuaca sebagai konteks lokasi perangkat terpilih. Perubahan harus menggunakan data aktual yang sudah tersedia, mempertahankan kontrak lama, dan tidak menambahkan fitur backend yang belum memiliki sumber data atau alur operasional.

## 2. Konteks arsitektur yang ditemukan

- Frontend menggunakan React, TypeScript, Vite, Axios, CSS Modules, dan sudah memiliki `maplibre-gl`.
- Modul WebGIS yang ada berorientasi pada katalog layer dan workspace GIS umum. Peta IoT akan dibuat sebagai komponen MapLibre khusus agar lifecycle peta, marker perangkat, dan panel detail tetap sederhana serta tidak mengikat halaman monitoring ke fitur GIS yang tidak diperlukan.
- Data fleet dan status perangkat sudah disediakan oleh `GET /api/iot/monitoring`. Endpoint ini sudah menghitung status `ONLINE`, `STALE`, `NO_RECENT_DATA`, `INACTIVE`, dan `UNKNOWN`, serta mengembalikan pembacaan terbaru per perangkat.
- `GET /api/iot/devices` sudah mengembalikan latitude dan longitude, tetapi endpoint monitoring belum mengikutkan koordinat. Endpoint monitoring akan diperluas secara additive.
- Prakiraan cuaca sudah memiliki `WeatherForecastPanel`, `GET /api/weather/forecast`, cache backend, dan provider Open-Meteo. Metadata alert lama tetap dipertahankan di kontrak backend untuk kompatibilitas, tetapi tidak dirender pada UI karena lokasi feed tidak cukup presisi untuk keputusan logger.
- Navigasi bersumber dari menu database dan di-resolve oleh frontend melalui `TopNavbar`, `MenuRouteResolver`, dan `AppRoutes`. Karena menu lama mungkin masih tersimpan atau diakses langsung, route lama tidak dihapus.

## 3. Ruang lingkup

### Termasuk

1. Mengubah label root navigasi menjadi `Manajemen IoT`.
2. Menyusun menu aktif berikut:
   - `Dashboard` → dashboard IoT yang sudah ada.
   - `Peta Monitoring` → halaman baru.
   - `Manajemen Perangkat` → halaman daftar perangkat yang sudah ada.
   - `Monitoring Sensor` → halaman monitoring sensor yang sudah ada.
   - `Riwayat Data Sensor` → alias/presentasi ulang halaman monitoring yang sudah memiliki history range.
3. Mempertahankan route langsung prakiraan cuaca untuk backward compatibility, tetapi menonaktifkan/hilangkan item `Prakiraan Cuaca` dari navigasi utama.
4. Menambahkan peta interaktif dengan marker perangkat yang memiliki koordinat valid.
5. Menampilkan ringkasan total, online, offline/stale, dan perangkat bermasalah dari response monitoring aktual.
6. Menampilkan detail perangkat dan pembacaan sensor terbaru ketika marker dipilih.
7. Menghubungkan panel cuaca dengan koordinat perangkat terpilih, termasuk loading, error, dan data kosong; notifikasi BMKG tidak ditampilkan.
8. Menjaga hasil request yang lebih lama agar tidak menimpa pilihan perangkat atau hasil request terbaru.

### Tidak termasuk

- Tidak menghapus endpoint, route, atau implementasi halaman cuaca yang masih mungkin dipakai melalui bookmark atau integrasi lama.
- Tidak mengubah schema database, proses ingest telemetry, autentikasi, atau otorisasi.
- Tidak membuat data perangkat, koordinat, pembacaan sensor, atau cuaca dummy.
- Tidak membuat backend notifikasi/inbox baru. `Notifikasi & Peringatan` ditandai sebagai roadmap/inactive karena belum ada sumber data dan workflow notifikasi yang dapat dipertanggungjawabkan. Metadata alert lama boleh tetap dikembalikan API, tetapi tidak ditampilkan sebagai peringatan operasional.
- Tidak menambahkan dependency peta baru karena MapLibre sudah tersedia.

## 4. Keputusan navigasi dan route

### Menu database

Migration SQL baru dibuat secara idempotent dan hanya mengatur menu yang relevan. Mapping yang dituju:

| Label | Route | Strategi kompatibilitas |
|---|---|---|
| Dashboard | `/iot-management/dashboard-iot` | Memakai route/page existing |
| Peta Monitoring | `/iot-management/peta-monitoring` | Route baru |
| Manajemen Perangkat | `/iot-management/daftar-perangkat` | Route existing, label diperbarui |
| Monitoring Sensor | `/iot-management/monitoring-sensor` | Route existing |
| Riwayat Data Sensor | `/iot-management/riwayat-data-sensor` | Route baru yang memakai halaman/komponen history existing |
| Notifikasi & Peringatan | — | Inactive/roadmap, tidak diarahkan ke halaman palsu |

`Prakiraan Cuaca` menjadi inactive dari menu utama, tetapi `/iot-management/prakiraan-cuaca` tetap tersedia dan tetap menggunakan `WeatherForecastPage` agar link lama tidak rusak. Resolver route juga mempertahankan code menu lama sebagai alias bila masih dikirim API menu.

Top navigation akan menampilkan ikon dari mapping ikon yang sudah dipakai dashboard, bila field ikon menu tersedia. Label dan route override di frontend harus tetap toleran terhadap menu lama selama migration belum diterapkan di semua environment.

## 5. Kontrak backend monitoring

Endpoint existing `GET /api/iot/monitoring` dipertahankan. Query `sourceType` dan seluruh field response lama tetap berlaku. Perubahannya hanya additive:

- Query SQL menambahkan `Latitude` dan `Longitude` dari `MstLogger` sebagai nullable.
- DTO `MonitoringDevice` menambahkan `Latitude` dan `Longitude` nullable.
- Perhitungan status, pembacaan terbaru, error, dan jumlah agregat existing tidak diubah.
- Nilai koordinat null atau di luar rentang tidak menyebabkan query atau response gagal; frontend akan mengabaikannya dari peta dan tetap menampilkan perangkat pada daftar/ringkasan.

Frontend menambahkan API wrapper dan tipe monitoring yang memodelkan summary, status, koordinat nullable, timestamp, serta dictionary pembacaan. Detail sensor lebih lengkap hanya mengambil `GET /api/iot/devices/{id}/parameters` untuk perangkat yang sedang dipilih agar tidak membuat N+1 request untuk seluruh fleet.

## 6. Desain halaman Peta Monitoring

Halaman terdiri dari tiga area responsif:

1. **Summary cards** di atas: total, online, offline/stale, dan issue/inactive/no-recent-data.
2. **Peta utama**: mengambil ruang terbesar pada desktop dan menjadi area utama pada mobile.
3. **Panel detail**: kosong sebelum marker dipilih; setelah dipilih menonjolkan pembacaan sensor aktual sebagai area utama dan prakiraan cuaca 10 hari sebagai konteks pendamping. Identitas/status tetap ringkas di header; metadata lokasi dan waktu tidak mengambil ruang utama.

Komponen peta menggunakan MapLibre secara langsung. Lifecycle wajib:

- membuat map setelah container tersedia;
- menambahkan dan memperbarui marker dari data valid;
- memasang event click pada marker untuk memilih `loggerId`;
- melakukan fit bounds hanya jika ada koordinat valid;
- menghentikan listener dan memanggil `map.remove()` saat unmount;
- tidak menggunakan koordinat default atau marker dummy;
- memvalidasi latitude finite dalam `[-90, 90]` dan longitude finite dalam `[-180, 180]`;
- menyediakan empty state jika tidak ada perangkat, tidak ada koordinat valid, atau response gagal;
- membedakan warna/status marker tanpa mengklaim `STALE` sebagai online.

Pada layar kecil, panel detail berpindah di bawah peta atau menjadi drawer yang tidak menutupi seluruh peta. Styling mengikuti CSS Modules dan pola visual dashboard/weather existing.

## 7. Detail perangkat dan pemisahan sumber data

Detail terpilih menggunakan row monitoring sebagai sumber ringkasan, lalu mengambil parameter sensor untuk metadata tambahan. Data aktual diberi label dan timestamp pengukuran/diterima. Prakiraan cuaca diletakkan pada section terpisah dengan label provider, periode validitas, waktu update, dan satuan yang diberikan API.

Jika perangkat tidak punya koordinat valid, detail perangkat tetap dapat dilihat dari daftar/selection, tetapi panel cuaca untuk perangkat tersebut menampilkan `data tidak tersedia` dan tidak memakai fallback Pekanbaru. Fallback default yang masih diperlukan halaman cuaca standalone tidak boleh bocor ke konteks marker peta.

## 8. Integrasi cuaca dan race guard

`WeatherForecastPanel` akan diperluas secukupnya agar caller peta dapat:

- mengunci query pada koordinat perangkat terpilih;
- menonaktifkan fallback lokasi default dan override manual pada konteks peta;
- menampilkan keadaan koordinat tidak tersedia secara eksplisit;
- tetap menggunakan cache backend existing dan lifecycle request existing.

Pemilihan marker baru atau unmount harus membatalkan/menjadikan usang hasil request cuaca dan parameter sebelumnya. Guard sequence/request id atau `AbortController` dapat dipilih sesuai pola existing, dengan syarat response lama tidak mengubah detail perangkat atau panel cuaca yang sedang dipilih. Error provider ditampilkan sebagai error cuaca, bukan sebagai kegagalan pemuatan data sensor.

## 9. History sensor

Route `Riwayat Data Sensor` memakai komponen/page monitoring existing yang sudah mendukung period day/week/month/year, range, agregasi, empty state, period-aware labels, dan stale-response guard. Tidak ada endpoint history baru. Jika diperlukan prop ringan untuk memfokuskan tampilan ke bagian history, perubahan tersebut harus tetap menjaga route monitoring lama.

## 10. Testing dan verifikasi

Sebelum menyatakan selesai, jalankan dan laporkan hasil nyata untuk:

- test backend existing serta test yang memverifikasi koordinat nullable pada response monitoring;
- build frontend dan pemeriksaan problem/lint yang tersedia;
- `git diff --check` pada kedua repository;
- navigasi menu baru dan route alias lama;
- summary dan marker dengan koordinat valid;
- perangkat tanpa koordinat dan koordinat invalid;
- marker click, detail sensor, dan state kosong/error;
- query cuaca berdasarkan koordinat marker, missing-coordinate state, provider error, dan perubahan marker cepat;
- route history dan filter period yang sudah ada;
- ukuran desktop dan mobile melalui browser bila environment tersedia.

Keterbatasan provider cuaca, data database lokal, permission, atau environment browser akan dilaporkan sebagai keterbatasan verifikasi, bukan dianggap lulus secara implisit.

## 11. Keputusan yang sengaja didelegasikan ke implementer

- Detail warna, ukuran marker, spacing, dan icon yang paling cocok dengan design system existing.
- Apakah panel detail desktop menggunakan split pane atau card overlay, selama peta tetap dominan dan tidak tertutup berlebihan.
- Pemilihan sequence guard versus abort signal sesuai helper HTTP yang sudah ada.
- Nama file komponen turunan selama tetap berada di feature IoT dan tidak menggandakan implementasi weather/history.

Semua pilihan di atas tidak boleh mengubah kontrak API, menambah data palsu, atau membuat menu roadmap tampak aktif.

## 12. Keputusan hasil builder-check dan konfirmasi

- Halaman peta mengambil semua `sourceType`; tidak ditambahkan filter source yang tidak diminta.
- Fase pertama tidak melakukan polling otomatis. Data diambil ketika halaman dibuka dan dapat dimuat ulang melalui aksi pengguna yang konsisten dengan pola halaman existing. Ini menjaga beban API dan mencegah perubahan selection yang mengejutkan.
- Style/basemap menggunakan konfigurasi MapLibre yang sudah tersedia di modul WebGIS; tidak ada style URL atau credential baru.
- Perangkat tanpa koordinat valid tetap ditampilkan pada daftar perangkat di panel. Perangkat tersebut dapat dipilih untuk melihat detail sensor, tetapi tidak dibuatkan marker atau query cuaca berbasis koordinat palsu.
- Endpoint parameter memakai `loggerId`, mengikuti identifier yang dipakai oleh API device existing.
- `ONLINE` dihitung sebagai online. `STALE`, `NO_RECENT_DATA`, `INACTIVE`, dan `UNKNOWN` dihitung sebagai tidak online; kategori issue menampung seluruh status selain `ONLINE` agar perangkat dengan status tidak dikenal tidak hilang dari perhatian operator. Kartu offline boleh merupakan komplemen online, sedangkan kartu issue adalah subset penjelas dan diberi label agar tidak disalahartikan sebagai jumlah yang saling eksklusif.
- Marker clustering tidak diperlukan untuk fase awal; marker tetap harus menggunakan data aktual dan fit bounds. Jika volume fleet terbukti besar saat verifikasi, kebutuhan clustering dicatat sebagai follow-up, bukan ditambahkan tanpa data performa.
