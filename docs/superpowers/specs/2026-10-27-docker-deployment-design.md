# Docker Deployment Design

## Intent

Menjalankan `AuthUtility.Api` sebagai container .NET 8 yang dapat diperbarui dari repository Git tanpa memasukkan connection string atau secret ke image maupun repository.

## Goals

- Build API dengan Docker multi-stage dari source project yang sudah ada.
- Menjalankan API pada port container `8080` dan memetakan port host default `5100`.
- Mengambil konfigurasi runtime melalui `.env` lokal yang tidak di-commit.
- Mempertahankan database yang sudah digunakan aplikasi: PostgreSQL dan beberapa SQL Server eksternal.
- Memulai ulang container otomatis setelah host restart atau proses API berhenti.
- Membuat alur update eksplisit: `git pull` lalu `docker compose up -d --build --remove-orphans`.

## Non-goals

- Menambahkan container database, schema initialization, atau migration otomatis.
- Mengubah kode aplikasi, routing, CORS, autentikasi, atau konfigurasi database.
- Membuat pipeline CI/CD atau image registry.
- Menjalankan frontend `MyHelpDesk` di Compose ini.

## Constraints

- Target framework API adalah `net8.0`.
- `Program.cs` membaca `ASPNETCORE_URLS`, sehingga container harus mengatur URL ke `http://+:8080`; default aplikasi `127.0.0.1` tidak dapat dipakai untuk akses dari host.
- Konfigurasi lokal `src/AuthUtility.Api/appsettings.json` sudah di-ignore Git dan harus tetap dikeluarkan dari Docker build context.
- Semua connection string dan JWT key harus diberikan saat runtime melalui variabel konfigurasi .NET dengan separator `__`.

## Approaches considered

1. **API-only dengan database eksternal — dipilih.** Paling sesuai dengan dependency aplikasi saat ini dan tidak mengubah data production.
2. **API + PostgreSQL lokal.** Tidak lengkap untuk aplikasi ini karena masih membutuhkan beberapa SQL Server eksternal dan belum ada migration/schema bootstrap yang dapat dijalankan secara aman.
3. **Image registry/CI.** Lebih cocok untuk deployment matang, tetapi membutuhkan registry dan pipeline tambahan di luar permintaan saat ini.

## Design

Tambahkan empat file operasional:

- `Dockerfile`: restore project references, publish `AuthUtility.Api`, lalu jalankan hasil publish pada image `mcr.microsoft.com/dotnet/aspnet:8.0` sebagai user non-root.
- `docker-compose.yml`: service `api` dengan build lokal, `env_file: .env`, port `${API_PORT:-5100}:8080`, dan `restart: unless-stopped`.
- `.dockerignore`: keluarkan build output, secret/config lokal, artefak deployment, temporary probes, test, dokumentasi, dan metadata Git dari build context.
- `.env.example`: daftar nama variabel yang dibutuhkan tanpa nilai rahasia nyata.

Compose sengaja tidak melakukan bind-mount source code atau `appsettings.json`; source baru masuk melalui image baru saat `--build`. Dengan begitu update server tetap deterministik dan container lama dapat diganti berdasarkan commit terbaru.

## Operational procedure

Pada server, salin `.env.example` menjadi `.env` satu kali dan isi nilai sebenarnya. Untuk pembaruan berikutnya:

```powershell
git pull
docker compose up -d --build --remove-orphans
docker compose ps
docker compose logs --tail=100 api
```

`git pull` mengambil source terbaru, sedangkan `docker compose up -d --build` membuat image dan container baru dari source tersebut. File `.env` tetap berada di server dan tidak berubah oleh `git pull`.

## Verification

- `docker compose config` berhasil setelah `.env` tersedia.
- `docker build -t apihelpdesk-api:verification .` berhasil.
- `dotnet test AuthUtility.sln --no-restore` tetap berhasil untuk regression check.
- Perubahan hanya menyentuh file deployment dan dokumentasi desain; tidak ada source aplikasi yang diubah.
