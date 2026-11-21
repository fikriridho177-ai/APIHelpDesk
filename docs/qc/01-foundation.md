# Fase 1 — Fondasi & Master QC

Status: **selesai & terverifikasi** (2026-11-19). Lingkup: hanya modul QC pada PostgreSQL `helpdeskasd` + frontend MyHelpDesk. Tidak menyentuh WAD/IoT/GIS/NAT, tidak menulis ke SQL Server.

## 1. Yang dibangun

### Migrasi SQL (idempotent + rollback)
- `sql/2026-11-19-qc-foundation.sql` — tanpa `BEGIN/COMMIT` internal agar bisa dibungkus transaksi oleh test.
  - `MstQcForm`: tambah `Version`, `EffectiveFrom`, `Pattern`, `TemplateFile`, `PrintTitle`, `ExcelCode`; ganti index unik `Code` menjadi unik `(Code, Version)`; metadata 3 form pilot lama di-update; insert 10 form baru `ON CONFLICT (Code, Version) DO NOTHING`.
  - Tabel master baru: `MstQcFormApprovalStep`, `MstQcPersil`, `MstQcPemborong`, `MstQcMandor`, `MstQcJenisPupuk`, `MstQcGrade`, `MstQcStandard`, `MstQcNumberSeq` (FK, index, CHECK).
  - Seed alur approval 4 level × semua form (Dilaporkan / P. Jawab Afd / Diperiksa / Disetujui) dengan `JabatanKode` + fallback `MenuCode='QC_APPROVAL'`, `ApproverGroupId` NULL (belum ada group QC di `UtlGroup`).
  - Seed jenis pupuk & grade; menu `QC_MASTER` + hak akses group ADMIN.
- `sql/2026-11-19-qc-foundation-rollback.sql` — membatalkan seluruh perubahan di atas secara aman (tidak menghapus form yang sudah punya inspeksi).

### Backend (.NET 8 / EF Core)
- Entity + konfigurasi `AppDbContext` untuk 8 tabel master & kolom baru `QcForm`.
- `QcPersilSync.cs`: reader read-only SQL Server (MyPAD/RSUPPKB) → upsert cache `MstQcPersil`; pemborong tersync otomatis (`Sumber='SYNC'`), entri `LOCAL` tidak ditimpa; parsing desimal koma invariant; deaktivasi persil yang hilang dari sumber.
- `QcPersilSyncHostedService`: sync terjadwal (`Qc:PersilSync:Enabled`, `IntervalMinutes`, clamp 5–1440, run pertama +20s).
- `QcMasterService` + `QcMasterController` (`api/qc/master`): CRUD form (versioning otomatis, kode tidak boleh diubah), approval-steps, persil (filter/paging/sync), pemborong, mandor, jenis-pupuk, grade, standard; validasi pesan Bahasa Indonesia; permission via `QcAccess` (View untuk GET, Create/Update untuk tulis).
- `QcFormsController`: list/detail kini memilih versi terbaru per kode.

### Frontend (React + TS)
- `types/qcMaster.ts`, fungsi API master di `api/qcApi.ts`.
- `components/QcMasterDrawer.tsx`: drawer form generik (text/number/select/date/textarea/checkbox, error alert, Escape/scrim close).
- `pages/QcMasterPage.tsx`: 8 tab (Form & Versi, Alur Approval, Persil cache, Pemborong, Mandor, Jenis Pupuk, Grade, Standar); search debounce, filter aktif/nonaktif, paging persil, tombol Sync sekarang + hasil per sumber, toast + undo untuk nonaktifkan, badge status/versi.
- Route `qc/master` di `AppRoutes.tsx` + `MenuRouteResolver.tsx` (`QC_MASTER`); CSS master di `qc.css` (tab, drawer, toast, pill, pager, scrollbar teal kustom).

## 2. Cara mencoba
1. Terapkan `sql/2026-11-19-qc-foundation.sql` ke `helpdeskasd` (sudah diterapkan di DB dev).
2. Jalankan API (`dotnet run --project src/AuthUtility.Api`) dan frontend (`npm run dev`).
3. Login sebagai group yang punya hak menu `QC_MASTER` (ADMIN sudah di-seed), buka **Master QC**.
4. Tab **Persil (cache)** → **Sync sekarang** untuk menarik cache dari SQL Server; coba filter/paging.
5. Tab lain: tambah/ubah/nonaktifkan (dengan undo) form, alur, pemborong, mandor, pupuk, grade, standar.

## 3. Hasil verifikasi
- Migrasi: apply committed ×2 (idempotent), jalur DB kosong (drop semua objek QC lalu core+foundation dalam transaksi rollback), dan skrip rollback dalam transaksi rollback — semuanya lolos. Forms=13, steps=52.
- Sync live: KLP 1474 / TNM_NNS 2435 / LHN_NNS 1512 / PNN 968 (baru semua), cache 6389; sync kedua 0 perubahan (idempotent); pemborong 122 tersync.
- Backend `dotnet test -c Release`: **325 lulus, 1 gagal** = `WadReportControllerTests.DeniedPermissions_StopBeforeDatabaseAndFileAccess` (kegagalan bawaan HEAD bersih, di luar lingkup QC, tidak diperbaiki). 12 test `QcMasterTests` baru lulus semua.
- Frontend: `tsc -b && vite build` hijau; verifikasi browser (harness mock sementara, sudah dihapus): render desktop & sempit, semua tab, sync + hasil per sumber, paging, drawer create/versioning (v2 dibuat), toggle + toast + undo, tanpa error konsol.

## 4. Asumsi & keputusan
- 5 kode pilot `QC-*` dipertahankan (registry UI + template export bergantung padanya); `ExcelCode` menyimpan kode FRM bila ada.
- `ExcelCode` NULL untuk form yang file contohnya tidak berkode / kodenya bentrok (menunggu konfirmasi, tidak ditebak).
- Form baru berstatus **nonaktif** sampai UI inputnya dibangun pada fase terkait.
- Approval steps di-seed, tetapi engine approval masih memakai `appsettings` sampai Fase 2 menggantinya ke master.
- Pemborong sebagian tersync (`SYNC`) dari sumber persil; mandor tidak punya sumber → CRUD lokal.
- Belum ada cache pegawai/wilayah/divisi tersendiri; kode wilayah/divisi/afdeling menumpang pada cache persil. Approver memakai hak menu bila `ApproverGroupId` NULL.

## 5. Pertanyaan tersisa (menunggu jawaban, tidak ditebak)
- Format No. Icar lama (`NoIcarLama`) dan apakah wajib.
- Kode form kosong/ganda pada file Excel (37, 04, kebersihan) dan bentrok PPK DASAR (FRM-QC-N-02-00 vs -06-00).
- Mapping jabatan/group → level approval dan scope afdeling (sumber employee→afdeling).
- Konfirmasi standar/target % per parameter (`MstQcStandard` belum di-seed nilai).
- Entitas PT lain (RSU/RSTM/PKB) untuk form panen kelapa RSTM.