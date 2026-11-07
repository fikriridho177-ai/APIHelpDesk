# Real IoT HTTPS OTA Design

## Intent

Mengubah modul Manajemen Perangkat & OTA dari simulasi `localStorage` menjadi
control-plane nyata untuk perangkat ESP8266. Perangkat melakukan polling HTTPS
ke API HelpDesk, mengirim heartbeat/telemetry kesehatan, mengambil pekerjaan
OTA, mengirim progres, lalu mengonfirmasi versi firmware setelah reboot.

Operator tetap menggunakan MyHelpDesk untuk mendaftarkan perangkat, mengunggah
paket firmware, membuat penugasan OTA, dan melihat status aktual. Monitoring
sensor lama yang membaca database `MyKTTATest` tetap dipertahankan; data control
plane OTA ditulis ke database utama HelpDesk.

## Goals

- Mengganti service demo pada [WadDeviceManagementPage](../../../../MyHelpDesk/src/features/ecs/pages/WadDeviceManagementPage.tsx)
  dengan service API tanpa fallback diam-diam ke data demo.
- Menyediakan registry perangkat, heartbeat, telemetry kesehatan, konfigurasi
  desired/reported, firmware metadata/upload, OTA assignment, event progres,
  dan konfirmasi pascareboot.
- Menggunakan credential unik per perangkat melalui header `X-Device-Key`;
  API key tidak boleh dikirim ke browser atau disimpan plaintext di database.
- Menghitung SHA-256 firmware di server dan mengirim ukuran, hash, serta URL
  download yang terautentikasi kepada perangkat.
- Menyediakan status OTA yang jujur: sukses hanya setelah perangkat reboot dan
  melaporkan versi target yang sama.
- Menyediakan kompatibilitas migrasi yang terdokumentasi untuk client ESP8266
  yang saat ini masih memanggil endpoint PHP lama.

## Non-goals

- Tidak mengubah sumber data read-only `MstLogger`/`TrnLoggerRaw*` pada
  `MyKTTATest`.
- Tidak melakukan OTA untuk ATmega2560 pada tahap ini. Pada board Mega +
  ESP8266, hanya firmware ESP8266 yang diperbarui.
- Tidak menambahkan MQTT, Redis, Kubernetes, atau microservice baru.
- Tidak mengklaim OTA hardware berhasil tanpa heartbeat pascareboot dari device.
- Tidak melakukan provisioning publik, menyimpan secret di frontend, atau
  menaruh binary firmware di git.

## Anti-goals

- Menjadikan `device_id`, alamat IP, atau MAC address sebagai satu-satunya
  autentikasi.
- Menganggap HTTP 200 dari download sebagai firmware aktif.
- Menganggap checksum sebagai bukti keaslian firmware; signature status tetap
  field terpisah.
- Menggunakan data demo ketika API gagal.

## Constraints discovered

- Backend [IotDeviceController](../../../../APIHelpDesk/src/AuthUtility.Api/Controllers/IotDeviceController.cs)
  saat ini berfokus pada pembacaan perangkat dan histori dari SQL Server.
- Data tulis aplikasi menggunakan `AppDbContext` PostgreSQL.
- Frontend yang sudah dipush masih mengekspos `DeviceManagementService.mode` sebagai
  `demo` dan menyimpan mutasi pada `myhelpdesk.wad.device-management.demo.v1`.
- [ESP8266_OTA_Client.ino](../../../../IoTMegaESP/ESP8266_OTA_Client/ESP8266_OTA_Client.ino)
  dan client pada folder IoT masih menggunakan HTTP endpoint PHP lama tanpa
  device credential. Client tersebut harus dimigrasikan sebelum dianggap aman
  untuk produksi.
- Source firmware berada di luar dua checkout aplikasi; perubahan firmware
  harus dijaga terpisah dari perubahan API/frontend dan tidak boleh menghapus
  perubahan lokal yang sudah ada di repository IoT.

## Approaches considered

1. **HTTPS polling dengan API sebagai control plane — dipilih.** Device
   melakukan heartbeat, report telemetry, polling pekerjaan, download firmware,
   dan report event melalui HTTPS. Ini memakai infrastruktur .NET/PostgreSQL
   yang sudah ada, tidak menambah broker, dan cocok dengan `ESPhttpUpdate`.
2. **MQTT + object storage.** Lebih realtime dan cocok untuk fleet besar, tetapi
   membutuhkan broker, ACL/topic design, TLS certificate provisioning, dan
   deployment baru yang tidak dibutuhkan untuk milestone ini.
3. **Mempertahankan PHP OTA lama.** Paling sedikit perubahan, tetapi tidak
   menyediakan credential per device, audit trail yang konsisten, atau kontrak
   desired/reported yang dibutuhkan UI; ditolak karena tidak memenuhi target
   produksi.

## Selected architecture

### Storage and ownership

Tambahkan entitas control-plane pada PostgreSQL utama:

- `IotDeviceAgent`: identitas perangkat, board/MCU, lokasi, status, firmware
  terakhir dilaporkan, last-seen, RSSI, IP, dan capability.
- `IotDeviceCredential`: device id, prefix key, salted hash, waktu dibuat,
  waktu dicabut. Plaintext key hanya dikembalikan sekali saat provisioning.
- `IotDeviceReport`: heartbeat/telemetry kesehatan yang immutable dan ringkas.
- `IotDeviceConfiguration`: desired dan reported configuration version serta
  ACK/NACK/reason.
- `IotFirmwarePackage`: metadata paket, target MCU/board, ukuran, SHA-256,
  path storage, signature status, dan uploader.
- `IotOtaJob` serta `IotOtaEvent`: target device, firmware, state, progress,
  failure reason, current/reported version, dan histori event.

Binary disimpan pada volume filesystem yang dikonfigurasi melalui
`IOT_FIRMWARE_STORAGE_PATH`, bukan di database atau bundle frontend.

### Admin API

Semua endpoint operator memakai JWT user yang sudah ada dan permission
`WAD_ECS_PERANGKAT` dengan fallback ke `WAD_ECS_STASIUN` untuk instalasi lama.

Kontrak minimum:

```text
GET    /api/iot/management/devices
POST   /api/iot/management/devices                 # returns one-time key
PATCH  /api/iot/management/devices/{deviceId}
GET    /api/iot/management/devices/{deviceId}
GET    /api/iot/management/devices/{deviceId}/reports
GET    /api/iot/management/devices/{deviceId}/configuration
PUT    /api/iot/management/devices/{deviceId}/configuration
GET    /api/iot/management/firmware
POST   /api/iot/management/firmware                # multipart upload
POST   /api/iot/management/ota
GET    /api/iot/management/ota
GET    /api/iot/management/activity
```

Response tidak pernah mengembalikan device key setelah response provisioning.
Delete/revoke menonaktifkan credential dan membatalkan job yang belum berjalan.

### Device API

Device API tidak memakai JWT user. Setiap request harus membawa `X-Device-Key`
dan endpoint hanya dapat mengakses device yang cocok dengan key tersebut.

```text
POST /api/iot/agent/heartbeat
POST /api/iot/agent/telemetry
GET  /api/iot/agent/configuration
POST /api/iot/agent/configuration/{version}/ack
GET  /api/iot/agent/ota/pending?currentVersion=...
POST /api/iot/agent/ota/{jobId}/events
POST /api/iot/agent/ota/{jobId}/confirm
GET  /api/iot/agent/firmware/{firmwareId}/download
```

Heartbeat minimum:

```json
{
  "deviceId": "DL-003",
  "deviceType": "MEGA_ESP8266",
  "platform": "ESP8266",
  "firmwareVersion": "1.0.0",
  "ipAddress": "192.168.1.20",
  "rssi": -63,
  "uptimeSeconds": 12345,
  "capabilities": ["heartbeat", "ota-esp8266"]
}
```

Telemetry kesehatan menyimpan timestamp device, firmware version, sensor values
yang tersedia, dan metadata koneksi sebagai JSON tervalidasi ukuran maksimum.
Nilai sensor utama yang sudah ada tetap dapat dibaca melalui endpoint monitoring
lama dan tidak dipaksa masuk ke tabel baru.

### OTA state machine

```text
queued -> downloading -> verifying -> installing -> rebooting
       -> waiting_confirmation -> succeeded
       \-> failed
```

Server hanya mengizinkan transisi berurutan. Device mengirim event sebelum dan
sesudah instalasi; setelah reboot, heartbeat dengan `firmwareVersion` target
memungkinkan server mengubah job menjadi `succeeded`. Jika timeout atau versi
tidak cocok, job menjadi `failed` dengan alasan yang dapat dilihat operator.

ESP8266 memakai `ESPhttpUpdate` melalui HTTPS, mengunduh URL server dengan
credential device, memverifikasi ukuran/hash yang dikembalikan API, lalu reboot.
Firmware URL tidak boleh berupa URL publik tanpa kontrol akses.

### Frontend behavior

Pertahankan interface service yang dipakai halaman sekarang, tetapi tambahkan
`api` implementation. Mode dipilih melalui `VITE_WAD_DEVICE_MANAGEMENT_MODE`:

- `demo`: hanya untuk development, banner dan simulasi tetap terlihat.
- `api`: default untuk deployment production; semua data berasal dari API dan
  error ditampilkan sebagai error.

UI menampilkan `lastSeen`, status koneksi, firmware reported, desired/reported
configuration, OTA progress/event, hash, signature status, dan pesan kegagalan.
Tombol simulasi status dihapus atau dinonaktifkan pada mode API.

### Security and rollout

- Produksi wajib memakai HTTPS dengan CA yang dapat diverifikasi ESP8266.
- Device key dibuat oleh operator, ditampilkan sekali, lalu disimpan di NVS
  firmware; key dapat dicabut dan dibuat ulang.
- Endpoint heartbeat dan telemetry menerapkan batas payload, rate limit ringan,
  dan idempotency berdasarkan device/timestamp untuk retry jaringan.
- Upload firmware membatasi ukuran dan ekstensi, menghitung SHA-256 server-side,
  dan menolak target MCU/board yang tidak valid.
- Deployment menambahkan volume firmware dan environment variables tanpa
  mencetak secret di log.

## Acceptance criteria

- Test backend membuktikan key valid/invalid, device isolation, heartbeat,
  telemetry validation, config ACK/NACK, firmware hash, OTA transition guard,
  post-reboot confirmation, timeout, dan revoke.
- Test frontend membuktikan API service tidak memakai localStorage demo, error
  tidak fallback ke fixture, dan status OTA/API ditampilkan dengan benar.
- `dotnet build`, backend test suite, frontend test suite, dan frontend build
  lulus.
- Firmware ESP8266 memiliki kontrak endpoint baru, header credential, HTTPS,
  event progres, dan confirmation setelah restart; sketch harus diverifikasi
  dengan toolchain Arduino/ESP8266 yang tersedia.
- Tidak ada perubahan pada `.gitignore` lokal atau artefak deployment yang
  tidak terkait.

## Open hardware boundary

Verifikasi terakhir—device menerima paket, reboot, dan mengirim heartbeat versi
baru—membutuhkan board ESP8266 nyata, firmware yang sudah di-flash, CA/server
URL produksi, key hasil provisioning, dan paket `.bin` yang dibangun untuk board
yang tepat. Kode aplikasi dapat selesai dan diuji tanpa hardware, tetapi bukti
OTA fisik tidak boleh dipalsukan sebagai hasil test lokal.
