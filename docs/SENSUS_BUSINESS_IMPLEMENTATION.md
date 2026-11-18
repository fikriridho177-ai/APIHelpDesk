# Implementasi output bisnis RNA/Sensus — 17 November 2026

## Audit dan perbedaan workbook

Mapping **seluruh 20 sheet KIN dan 20 sheet Wilayah 1**, rentang, peran input/reference, rekap, memo, rekomendasi, master dan formula rusak ada di [SENSUS_EXCEL_AUDIT.md](SENSUS_EXCEL_AUDIT.md).

- KIN: satu `IM`, satu `From 04, Rekap HP `, satu `LapRekomendasi HP`, dan contoh Form 2 berupa gambar EMF.
- Wilayah 1: `IM organik`, `IM Non organik`, `Form Rekap Organik`, `Form Rekap Non-Organik`, dan `LapRekomendasi HP`.
- Sheet persil/sertifikasi/konversi, peta, sketsa sampel, pupuk, dan sheet kosong adalah reference. Tidak diimpor menjadi transaksi/master baru. Sumber persil tetap master existing.
- Output mengikuti kelompok kolom bisnis, bukan salinan sel/formula rusak atau cetakan identik: identitas persil, sampel per HPT, jumlah/persen serangan, pohon mati, rekomendasi/pengendalian, musuh alami/cuaca. Jumlah kolom HPT mengikuti master; PDF lebar dibagi menjadi beberapa bagian dengan identitas berulang agar terbaca.
- Memo lama, nomor/nama/tujuan memo, penanda tangan contoh, metode/dosis yang belum ada pada master tidak dipakai sebagai fakta baru. Slot tujuan/dari memo kosong, nama petugas/approver berasal dari transaksi.

## Data yang digunakan

Transaksi tetap `TrnSnsHeader`, `TrnSnsDetail`, `TrnSnsFoto`, `HistorySnsLog`. Tidak ada tabel transaksi berdasarkan wilayah, status lahan atau form; approval/foto dan permission tidak diganti.

Existing header: `JenisId`, `PersilSumber`, `PersilId`, `PersilKode`, `KodeWilayah`, `KodeAfdeling`, `TanggalSensus`, `JumlahPokokSampel`, `JumlahPokokTerserang`, `PetugasUserId`, `ApprovedByUserId`, `ApprovedAt`, `Status`.

Tambahan snapshot pada **header existing**:

| Field | Sumber / kegunaan |
| --- | --- |
| JenisForm | FORM1 default; FORM2 ditawarkan pada master yang Form2Enabled (seed: Plesispa dan Ulat Api) |
| StatusLahan, StatusLahanSumber | ORGANIK / NON_ORGANIK / RA / status lainnya; sumber PERSIL atau MANUAL; null = belum diketahui |
| FaseTanaman | TM/TBM dari persil atau input jika sumber tidak tersedia; tidak ditebak dari usia |
| LuasHa, JumlahPokokPersil, BulanTanam | Snapshot konteks persil; nilai sumber mengalahkan input client |
| JumlahJalur | Konteks pola sampel |
| PohonMatiPetir, PohonMatiHpt, PohonMatiLain | Pengamatan umum, nullable (kosong bukan nol) |
| Pengendalian, MusuhAlami, Cuaca | Catatan kondisi/tindak lanjut, nullable |

Persil dibaca lewat metode khusus Sensus pada `PersilReader`, menggunakan query existing dengan detail sumber. Alias status yang dikenali: StatusLahan/JenisLahan/StatusOrganik/OrganicStatus/KodeStatusLahan, atau IsOrganic/IsOrganik/Organik. O/N/R dinormalisasi. Jika sumber tidak menyediakan informasi, UI mengizinkan fallback; tidak otomatis menganggap Non-Organik. Field yang ada di sumber terkunci di UI dan diutamakan kembali oleh server.

`MstHpt` dan tabel parameter `MstHptParameter` digunakan. Tabel parameter existing di-rename dari `MstParameterHpt` oleh migrasi penamaan tabel; tidak ada duplikasi data. Master mendapat `Form2Enabled`, `ReportGroup`; parameter mendapat `FormScope` ALL/FORM1/FORM2. Nilai dinamis tetap disimpan di detail existing. Form 2 ulat mencakup jalur/pohon, jenis ulat, telur, ulat kecil/besar/mati, pupa hidup/kosong/mati, pelepah dan catatan sesuai contoh. Tidak diturunkan nilai per pelepah dari hitungan per pohon.

## Master rule dan migrasi

Master baru **MstHptRule** (satu-satunya tabel baru):

`Id, JenisId, ParameterCode, Operator, ThresholdValue numeric(19,6), ThresholdUnit, Aggregation, ConditionParameterCode?, ConditionOperator?, ConditionValue?, StatusLahan?, JenisForm?, ResultCode, Message, Color, ElseMessage, ElseColor, Active, SortOrder, SeedKey?`.

Operator: `> >= < <= =`; agregasi parameter angka: AVG/SUM/MIN/MAX. Kondisi teks memakai `=`. Parameter angka dapat berupa metrik header atau NUMBER aktif dari jenis yang sama. Evaluator tidak melakukan switch berdasarkan kode HPT. Rule aktif diproses menurut SortOrder/Id; hasil cocok pertama digunakan. Data/kondisi yang belum lengkap dan rule yang belum ada menghasilkan status belum dievaluasi/abu-abu, tidak dianggap hijau.

Seed one-time memakai `BusinessSeedVersion`, transaksi dan advisory lock; tidak menimpa rule admin yang sudah ada. Jenis yang baru dibuat admin ditandai sudah melewati migrasi, sehingga konfigurasi barunya tidak diubah berdasarkan Code saat restart. Pengulangan tidak menambah rule/parameter duplikat. Jenis HPT yang tidak mempunyai dasar rule resmi tidak diberi rule karangan.

Contoh TIRATHABA yang bisa diatur admin di Master Jenis & Parameter:

```json
{
  "parameterCode": "PERSEN_TERSERANG", "operator": ">=",
  "thresholdValue": 5, "thresholdUnit": "%", "aggregation": "AVG",
  "resultCode": "PERLU_PENGENDALIAN",
  "message": "Ambang terpenuhi, lakukan pengendalian.", "color": "red",
  "elseMessage": "Tetap lakukan monitoring.", "elseColor": "green",
  "active": true, "sortOrder": 0
}
```

Ambang seed mempertahankan flowchart/evaluasi existing: Plesispa TM >5%, Oryctes TM >3%, keduanya TBM >=1 pohon; Ulat Api/Kantong AVG >=5 ulat/pelepah; Rayap >=1 pohon; Tirathaba >=5%; penyakit yang disebut flowchart >0 pohon terserang. Memo Excel berbeda pada batas Oryctes/Tirathaba; belum ada keputusan baru sehingga existing dipertahankan dan perbedaan dilaporkan. Rekomendasi metode/dosis dapat dimasukkan admin berdasarkan referensi bisnis yang sah.

Penamaan tabel: hentikan API, jalankan `sql/2026-11-18_rename_tables_category_prefix.sql`, lalu `sql/2026-11-18_monitoring_views.sql`, baru jalankan API versi baru. Script startup sudah memakai `MstHptParameter` dan `TrnSnsHeader`, jadi urutan ini wajib. Rollback tersedia di `sql/2026-11-18_rename_tables_rollback.sql`; objek WAD dan IoT tidak termasuk cakupan rename.

SQL idempotent: `src/AuthUtility.Api/Sensus/sensus-business-output.sql` (embedded). `SensusBusinessSetup.EnsureAsync` dipanggil saat startup API setelah database dapat dihubungi. Script mengubah header/master existing, membuat master rule/index, lalu seed dijalankan. **Penerapan dan eksekusi SQL pada PostgreSQL operasional belum terverifikasi dari lingkungan agent karena koneksi jaringan ditolak.** Restart API pada host yang memiliki akses DB diperlukan untuk penerapan; log startup perlu diperiksa. Uji idempotensi seed memakai EF InMemory; uji script memeriksa resource/guard, bukan eksekusi PostgreSQL.

## Query/API laporan

`SensusBusinessReportQuery.ApprovedQuery` adalah query EF atas transaksi existing:

```sql
-- Bentuk logis query dasar (filter opsional diterapkan hanya bila diisi):
SELECT h.* FROM "TrnSnsHeader" h
WHERE h."Status" = 'APPROVED'
  AND h."TanggalSensus" BETWEEN @dateFrom AND @dateTo
  -- AND h."KodeWilayah" = @wilayah
  -- AND h."StatusLahan" = @statusLahan
  -- AND h."JenisForm" = @jenisForm
  -- AND h."JenisId" = @jenisId
```

Join/navigation existing: master HPT + rules, detail + parameter, petugas dan approver. `Build` memproyeksikan hasil ke matrix/memo/analisis; tidak ada tabel dashboard atau transaksi baru dan tidak perlu SP/function tambahan. View baru hanya alias master parameter, bukan penyimpanan laporan.

Pilihan sensus laporan: satu **approved terakhir per sumber-persil/HPT/form/status lahan/bulan**, urutan tanggal sensus lalu Id. Preview menyebut jumlah sebelum/sesudah pemilihan ini. Analisis menghitung observasi dan persil unik, bukan menjumlahkan populasi berulang antar HPT. Sampel/persen tetap per HPT; nilai bersama berbeda dibiarkan kosong dengan catatan. Memo menjumlahkan sampel hanya di dalam kelompok HPT/form/status/bulan yang sama. Data lama yang belum memiliki status lahan dikelompokkan terpisah sebagai BELUM_DIKETAHUI.

| Endpoint baru | Kegunaan |
| --- | --- |
| GET /api/sensus/business-report/lookups | Wilayah/status dari data existing dan jenis dari master, tanpa daftar wilayah hardcode |
| GET /api/sensus/business-report | Dataset APPROVED untuk preview kelima laporan + lampiran Form 2 |
| GET /api/sensus/business-report/export | Dataset yang sama, dengan validasi Export tambahan sebelum download |
| GET /api/sensus/business-report/analysis | Organik/status lain, wilayah, HPT, tren bulan dan persil memenuhi ambang |
| POST /api/sensus/master/jenis/{jenisId}/rules | Tambah rule |
| PUT /api/sensus/master/rules/{id} | Ubah rule |
| DELETE /api/sensus/master/rules/{id} | Nonaktifkan rule; tidak hapus data |

Read memakai hak Read Monitoring/Analisis/Dashboard existing; export juga wajib Export Monitoring/Analisis. CRUD rule memakai Update master existing. Seluruh endpoint berada dalam controller Authorize existing. Tidak mengubah menu/permission atau route web.

Filter: dateFrom/dateTo (maksimal 366 hari), wilayah, statusLahan, jenisForm, jenisId. Lebih dari 5.000 header APPROVED menghasilkan HTTP 422 agar filter dipersempit; tidak memotong data diam-diam.

## Penggunaan web dan output terverifikasi

1. **Layanan → RNA → Input Sensus**: pilih jenis, persil, Form 1/2; status/konteks otomatis jika tersedia; isi parameter dinamis dan simpan/kirim. Foto tetap opsional.
2. **RNA → Approval**: workflow existing; detail menampilkan form/status/konteks. Report bisnis baru hanya mengambil APPROVED.
3. **RNA → Monitoring & Analisis → Laporan bisnis**: pilih filter dan output, tampilkan preview. Download Excel/PDF tersedia di preview bagi pengguna Export; server memeriksa ulang permission/data sebelum download. Jika data berubah, preview diperbarui dan pengguna harus memeriksa ulang.
4. Tab Analisis dan Dashboard menampilkan tambahan analisis approved Organik/Non-Organik/status lain, wilayah, HPT, bulan dan daftar persil memenuhi ambang master.

Kelima output **Rekap Hama & Penyakit, Rekap Organik, Rekap Non-Organik, Internal Memo, Laporan Rekomendasi** telah dihasilkan sebagai XLSX dan PDF dalam tes fixture. Form 2 dilampirkan sesuai observasi, bukan tabel baru. Excel memakai merge kelompok kolom, fill merah/hijau dari master, logo `src/docs/logoRsup2020.png`, freeze/repeated headers dan print setup. PDF memakai judul berulang, tabel rapi, pembagian kolom lebar dengan identitas persil berulang, dan pagination sesuai isi. Output tidak menyalin gambar/formula rusak, memo historis atau angka sample Excel sebagai transaksi.

Artefak tes sintetis, **bukan laporan operasional**: `%TEMP%\sensus-business-output-check\` berisi lima pasangan XLSX/PDF dan `REKOMENDASI_100_ROWS.pdf`. Report DTO fixture backend juga diuji melalui generator frontend. File PDF berhasil dimuat/render dengan Windows PDF API, selain pemeriksaan teks/baris terakhir dan OOXML yang dibaca kembali dengan SheetJS.

## Validasi

```powershell
# dari root workspace:
dotnet build APIHelpDesk/src/AuthUtility.Api/AuthUtility.Api.csproj --no-restore -p:UseAppHost=false -o "$env:TEMP/sensus-business-verify" -v minimal
dotnet test APIHelpDesk/tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore --filter "FullyQualifiedName~Sensus|FullyQualifiedName~RnaRingkasan" -p:UseAppHost=false -o "$env:TEMP/sensus-business-tests" -v minimal
# dari MyHelpDesk:
node --test tests/sensusBusinessOutput.test.mjs tests/sensusInput.test.mjs tests/sensusNavigation.test.mjs tests/sensusEvaluation.test.mjs
npm.cmd run build
```

Focused backend: **49 lulus**, mencakup rule existing/batas/operator generik, CRUD + permission master, seed idempotent, persil snapshot, Form 2 scope, clear revision/legacy omission, APPROVED filter/dedup/denominator, query/DTO, photo/approval/menu regression. Focused frontend: **18 lulus**, termasuk generator kelima output, native colors/logo/OOXML, Form 2 dinamis, filter status/zero/null, PDF lebar/100 baris, input source, navigation dan fixture DTO backend (tersedia setelah backend tests).

Build backend/frontend lulus. Warning existing: nullable pada file QC yang tidak diubah, serta ukuran bundle / static-dynamic import XLSX di Vite. Tidak ada error build tersisa. Tidak melakukan commit/reset/stage.

## Daftar file source yang berubah

Backend (path relatif APIHelpDesk):

- `src/AuthUtility.Domain/Entities/SensusEntities.cs`, `SnsHptRule.cs`
- `src/AuthUtility.Application/DTOs/Sensus/SensusDtos.cs`, `SnsBusinessDtos.cs`
- `src/AuthUtility.Infrastructure/Data/AppDbContext.cs`
- `src/AuthUtility.Api/AuthUtility.Api.csproj`, `Program.cs`
- `src/AuthUtility.Api/Controllers/SensusController.cs`, `SensusMobileController.cs`, `SensusMasterController.cs`, `SensusMasterRules.cs`, `SensusBusinessReports.cs`
- `src/AuthUtility.Api/Sensus/PersilReader.cs`, `SensusHptEvaluation.cs`, `SensusBusinessContext.cs`, `SensusBusinessSetup.cs`, `SensusBusinessReport.cs`, `sensus-business-output.sql`
- `tests/AuthUtility.Tests/SensusHptEvaluationTests.cs`, `SensusBusinessTests.cs`
- Dokumen audit dan laporan ini.

Frontend (path relatif MyHelpDesk):

- `src/features/sensus/types/sensus.ts`, `types/sensusBusiness.ts`, `api/sensusBusinessApi.ts`
- `src/features/sensus/pages/SensusInputPage.tsx`, `MasterJenisPage.tsx`, `SensusDashboardPage.tsx`, `SensusMonitoringPage.tsx`
- `src/features/sensus/components/SensusBusinessFields.tsx`, `SensusRuleEditor.tsx`, `SensusBusinessReports.tsx`, `SensusBusinessAnalysis.tsx`, `SensusParts.tsx`, `SensusDetailModal.tsx`
- `src/features/sensus/sensusBusinessOutput.ts`, `sensusBusinessExport.ts`
- `tests/sensusBusinessOutput.test.mjs`
- `docs/superpowers/plans/2026-11-17-sensus-business-reports.md`

Dua workbook input tetap tidak diubah dan tetap untracked sebagaimana saat tugas dimulai. Tidak ada perubahan source bisnis QC/GIS/WAD/NAT, navbar, approval flow atau foto.


## Perbaikan penyimpanan rule

Tambah/ubah rule menormalisasi teks kode, operator, agregasi, warna, serta field opsional kosong sebelum validasi. Frontend mengirim hanya field request, membersihkan kondisi yang tidak dipilih, dan memvalidasi parameter aktif, angka, prioritas, serta rekomendasi sebelum POST/PUT. Ambang dan whitelist operator tidak berubah. Kegagalan reload daftar sesudah penyimpanan sukses tidak lagi ditampilkan sebagai kegagalan save.

Uji regresi mencakup respons 400 sebelum normalisasi, create/update sesudah perbaikan, validasi MVC untuk payload form, serta penolakan operator invalid. Focused backend 52/52 dan frontend 26/26 lulus. Frontend tests tambahan: tests/sensusRuleForm.test.mjs dan tests/sensusApiError.test.mjs. Verifikasi memakai EF InMemory/MVC; request pengguna pada server berjalan belum dapat diulang dari sandbox. Restart API dan muat ulang frontend untuk memakai kode terbaru.
