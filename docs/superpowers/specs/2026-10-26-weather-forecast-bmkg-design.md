# Weather forecast and BMKG warning layer design

## Intent

Tambahkan prakiraan cuaca 10 hari pada area IoT/WAD Water Management tanpa
menaruh API key di browser, serta tampilkan peringatan dini lokal dari BMKG
sebagai lapisan keselamatan operasional.

## Goals

- Menyediakan prakiraan harian 10 hari berbasis koordinat latitude/longitude.
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
  `Lokasi` logger.
- Tidak membuat notifikasi push, eskalasi, atau keputusan otomatis terhadap
  pompa/valve.

## Selected architecture

Backend ASP.NET Core menyediakan `GET /api/weather/forecast` yang menerima
`latitude`, `longitude`, dan optional `area`. Service backend memanggil
Open-Meteo untuk prakiraan sampai 10 hari dan BMKG `/alerts/nowcast/id` untuk
peringatan aktif. Cache memory berdurasi 15 menit mencegah refresh halaman
menjadi banjir request.

Frontend React menggunakan API backend tersebut. `WeatherForecastPanel` dapat
dipasang di dashboard IoT maupun halaman detail. Lokasi awal memakai Pekanbaru
(`-0.5071, 101.4478`) sebagai fallback yang terlihat oleh pengguna dan dapat
diubah melalui input koordinat; deployment dapat mengganti fallback ini di
source/config berikutnya setelah koordinat lokasi WAD dikonfirmasi.

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
- Copy membedakan `Prakiraan` provider dan `Peringatan resmi BMKG`; tidak ada
  label "akurat 100%".

## Acceptance criteria

- Endpoint backend mengembalikan maksimal 10 data harian dengan field contract
  di atas.
- Feed BMKG diparse tanpa membuat request browser langsung ke BMKG.
- Error BMKG tidak mematikan forecast provider.
- Endpoint menolak koordinat di luar rentang geografis.
- Dashboard IoT menampilkan panel cuaca dan route detail dapat dibuka.
- `dotnet test`, `dotnet build`, dan `npm run build` berhasil.
- Tidak ada API key cuaca di source frontend atau bundle.
