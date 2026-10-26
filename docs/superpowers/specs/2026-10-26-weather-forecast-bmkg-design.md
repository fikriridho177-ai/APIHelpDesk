# Weather forecast and BMKG warning layer design

## Intent

Tambahkan prakiraan cuaca 10 hari pada area IoT/WAD Water Management tanpa
menaruh API key di browser, serta tampilkan peringatan dini lokal dari BMKG
sebagai lapisan keselamatan operasional.

## Goals

- Menyediakan prakiraan harian 10 hari berbasis latitude/longitude tersimpan
  pada setiap logger.
- Menggunakan Open-Meteo sebagai provider prakiraan tanpa API key untuk tahap
  awal.
- Mengambil RSS/CAP nowcast BMKG sebagai daftar peringatan aktif.
- Menjaga semua pemanggilan provider berada di backend dan diberi cache singkat.
- Menampilkan ringkasan cuaca di Dashboard IoT dan detail 10 hari pada route
  cuaca khusus.
- Menampilkan sumber data, waktu pembaruan, serta status ketika feed BMKG tidak
  tersedia.

## Non-goals

- Tidak melakukan scraping Weather Underground.
- Tidak mengklaim prakiraan sebagai jaminan akurasi atau menggantikan instruksi
  resmi BMKG.
- Tidak membuat konfigurasi API key OpenWeather pada tahap ini.
- Tidak mengubah struktur database logger atau menebak koordinat dari teks
  `Lokasi` logger; gunakan kolom koordinat yang sudah ada pada `MstLogger`.
- Tidak membuat notifikasi push, eskalasi, atau keputusan otomatis terhadap
  pompa/valve.

## Selected architecture

Backend ASP.NET Core memperluas response `GET /api/iot/devices` dengan
`latitude` dan `longitude` dari `MstLogger`. Frontend memakai koordinat logger
terpilih saat memanggil `GET /api/weather/forecast`, yang tetap menerima
`latitude`, `longitude`, dan optional `area`. Service backend memanggil
Open-Meteo untuk prakiraan sampai 10 hari dan BMKG `/alerts/nowcast/id` untuk
peringatan aktif. Cache memory berdurasi 15 menit mencegah refresh halaman
menjadi banjir request.

Frontend React menggunakan API backend tersebut. `WeatherForecastPanel` dapat
dipasang di dashboard IoT maupun halaman detail. Dashboard mengikuti logger
yang dipilih pada daftar perangkat, sedangkan halaman detail menyediakan
pilihan logger. Lokasi awal Pekanbaru (`-0.5071, 101.4478`) hanya dipakai saat
panel belum menerima daftar logger; logger tanpa koordinat ditahan dan harus
memakai logger lain atau override koordinat manual melalui input.

## Backend response contract

`GET /api/weather/forecast?latitude=-0.5071&longitude=101.4478&days=10&area=Riau`

```json
{
  "location": {
    "latitude": -0.5071,
    "longitude": 101.4478,
    "timezone": "Asia/Jakarta"
  },
  "fetchedAt": "2026-10-26T10:00:00Z",
  "forecastSource": "Open-Meteo",
  "alertSource": "BMKG",
  "alertStatus": "available",
  "forecast": [
    {
      "date": "2026-10-26",
      "weatherCode": 61,
      "summary": "Hujan ringan",
      "temperatureMaxC": 30.2,
      "temperatureMinC": 23.8,
      "precipitationProbability": 70,
      "precipitationMm": 8.4,
      "windMaxKmh": 15.5
    }
  ],
  "alerts": [
    {
      "title": "Peringatan dini cuaca",
      "description": "...",
      "effective": "2026-10-26T09:00:00+07:00",
      "expires": "2026-10-26T12:00:00+07:00",
      "area": "Riau",
      "severity": "Moderate",
      "url": "https://www.bmkg.go.id/..."
    }
  ]
}
```

Jika BMKG gagal tetapi Open-Meteo berhasil, response tetap `200` dengan
`alertStatus: "unavailable"` dan `alerts: []`; prakiraan tidak boleh hilang
hanya karena feed peringatan sedang bermasalah. Jika provider prakiraan gagal,
endpoint mengembalikan `503`.

## UI decisions

- Header panel: `Prakiraan Cuaca 10 Hari`, lokasi, koordinat, refresh, dan
  timestamp.
- Banner BMKG berada paling atas; warna merah/amber hanya berdasarkan severity
  yang diterima dari BMKG.
- Forecast cards menampilkan tanggal, kondisi, suhu min/max, peluang hujan,
  curah hujan, dan angin.
- Dashboard IoT menampilkan lima hari pertama sebagai ringkasan dan tautan ke
  detail 10 hari; halaman detail menampilkan semua 10 hari.
- Ketika logger berganti, koordinat dan prakiraan mengikuti logger tersebut;
  logger tanpa koordinat tidak boleh diam-diam dianggap berada di lokasi lain.
- Copy membedakan `Prakiraan` provider dan `Peringatan resmi BMKG`; tidak ada
  label "akurat 100%".

## Acceptance criteria

- Endpoint backend mengembalikan maksimal 10 data harian dengan field contract
  di atas.
- Feed BMKG diparse tanpa membuat request browser langsung ke BMKG.
- Error BMKG tidak mematikan forecast provider.
- Endpoint menolak koordinat di luar rentang geografis.
- Endpoint perangkat mengembalikan latitude/longitude logger tanpa mengubah
  kontrak field lama.
- Dashboard IoT menampilkan panel cuaca dan route detail dapat dibuka.
- `dotnet test`, `dotnet build`, dan `npm run build` berhasil.
- Tidak ada API key cuaca di source frontend atau bundle.
