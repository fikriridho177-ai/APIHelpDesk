# Ringkasan implementasi backend

Dokumen ini mencatat pekerjaan backend yang mendukung HelpDesk-ASD dan WAD.

## Pekerjaan yang diselesaikan

- Menyediakan endpoint monitoring WAD berbasis view SQL Server untuk Curah Hujan, TMAK, dan TMAT.
- Membatasi sumber view dengan allow-list dan menjaga filter tanggal, wilayah, serta titik tetap terparameterisasi.
- Mempertahankan pemeriksaan autentikasi dan permission menu WAD pada endpoint monitoring.
- Memperbaiki generator Swagger untuk upload firmware dengan membiarkan `IFormFile` diinfer oleh ASP.NET Core, sementara metadata form tetap memakai `[FromForm]`.
- Menambahkan regression test agar masalah Swagger tidak muncul kembali.

## Manfaat

- Swagger dapat memuat seluruh definisi API sehingga endpoint lebih mudah diuji dan diintegrasikan.
- Frontend dapat membaca data monitoring dan histori WAD tanpa query database langsung dari browser.
- Allow-list view dan query terparameterisasi mengurangi risiko akses view yang tidak sah dan SQL injection.
- Permission backend tetap menjadi lapisan keamanan terakhir walaupun request berasal dari frontend.

## Menjalankan lokal

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5000'
dotnet run --no-launch-profile --project src/AuthUtility.Api/AuthUtility.Api.csproj
```

- Swagger: `http://127.0.0.1:5000/swagger/index.html`
- API base URL: `http://127.0.0.1:5000/api`

Production tetap menggunakan konfigurasi environment Dokploy dan tidak bergantung pada port lokal ini.
