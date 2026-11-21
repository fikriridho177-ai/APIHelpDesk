# Fase 0 — Recon Modul QC (read-only)

Tanggal: 2026-11-19 · Pelaku: Qoder · Status: **tidak ada kode/database yang diubah**; satu-satunya artefak baru adalah dokumen ini.
Metode: baca repo backend/frontend; dump skema & jumlah baris PostgreSQL `helpdeskasd` serta skema master SQL Server **hanya lewat koneksi read-only yang sudah dipakai API** (SELECT INFORMATION_SCHEMA / sys.objects / pg_catalog + sampel TOP 3); inventaris 11 file `.xls` di `../docs/file` memakai SheetJS (`xlsx`) tanpa menulis ulang file sumber. Skrip dump sementara dihapus setelah dipakai.

Arah pengguna (mengikat): master **persil, wilayah, divisi, pegawai/karyawan memakai yang sudah ada di API**; tidak membaca semua view SQL Server; SQL Server **hanya baca**, CRUD hanya di PostgreSQL `helpdeskasd`.

---

## 1. Repo & konvensi yang berlaku

| Aspek | Kondisi nyata |
|---|---|
| Backend | `APIHelpDesk` .NET 8 (`net8.0`), EF Core 8.0.33 (Npgsql 8.0.8 = `helpdeskasd`; SqlServer 8.0.33 = MyPAD/RSUPPKB/PKBPayroll read-only), JwtBearer, SkiaSharp, MQTTnet |
| Frontend | `MyHelpDesk` React 18.3 + Vite 8.3.5 + TS 5.6, react-router-dom 7, axios + interceptor token (`src/api/http.ts`), zod, recharts, jspdf+autotable, `xlsx` 0.20.3; **tanpa redux/zustand** (hooks + state lokal) |
| CSS | CSS polos per fitur: `lyn-*` (layanan), `sns-*` (sensus), `qc-*` (palet teal, `src/features/qc/qc.css`) |
| Migrasi DB | **Tidak ada EF Migrations**; SQL tulis-tangan idempoten di `APIHelpDesk/sql/YYYY-MM-DD_topik.sql` (`BEGIN/COMMIT`, `IF NOT EXISTS`, `ON CONFLICT DO NOTHING`, `DO $$`, `pg_advisory_xact_lock`, `lock_timeout`). Rollback contoh: `2026-11-18_rename_tables_rollback.sql`. Diterapkan **manual setelah review** (header `2026-11-11-qc-core-schema.sql`); pengecualian embedded-resource startup: `Sensus/sensus-business-output.sql` |
| Auth/otorisasi | `IPermissionService.HasPermissionAsync(User, menuCode, MenuPermission)`; QC memakai `Qc/QcAccess.cs` (group `UtlUser.GroupId → UtlGroup`, hak `UtlGroupMenu`, SuperAdmin bypass; scope daftar = owner + handler). Seed menu `sql/2026-11-11-qc-menu-seed.sql`: root `QC` + 7 anak (`QC_DASHBOARD/INSPEKSI/BUAT_INSPEKSI/APPROVAL/FINDING/MONITORING/REPORT`). Frontend `useMenuPermission(menuCode)` + `MenuRouteResolver` |
| Test | Backend xunit 2.5.3 + EF InMemory: `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj -c Release` (bin/Debug terkunci saat API jalan). Frontend `npx tsc -b`, `npm run build`, `node --test tests/*.test.mjs` |
| Dev | `run-local.bat`: API :5000 + Vite :5173. Klone bersarang `APIHelpDesk/MyHelpDesk` basi — abaikan |

Catatan baseline test: 313 lulus, 1 gagal (`WadReportControllerTests.DeniedPermissions_StopBeforeDatabaseAndFileAccess`) **sudah gagal di HEAD bersih** — bawaan, di luar lingkup QC, tidak disentuh.

## 2. PostgreSQL `helpdeskasd`: kondisi 5 tabel QC + view

Jumlah baris saat recon: `MstQcForm`=3, `TrnQcInspection`=0, `TrnQcApproval`=0, `TrnQcFinding`=0, `HistoryQcInspection`=0.

- **MstQcForm** — `Id` serial PK; `Code` varchar(64) **UNIQUE (`IX_MstQcForm_Code`)**; `Name`, `Commodity`, `Description`, `Active` default true; audit Created/Updated. Isi: `QC-PANEN-KLP`, `QC-PUPUK-TABUR-NNS`, `QC-KEBERSIHAN-NNS` (bukan kode `FRM-QC-*` di Excel).
- **TrnQcInspection** — `Id` bigint PK (tanpa default/identity); `InspectionNo` varchar(50) nullable + unique partial (`WHERE InspectionNo IS NOT NULL`); FK `FormId → MstQcForm` RESTRICT; `InspectionDate` date; `CompanyId` + `CompanyNameSnapshot`; `WilayahKode/PersilSumber/PersilId/PersilKode/KodeAfdeling`; `Commodity`; `PeriodMonth` (CHECK 1..12) / `PeriodYear`; `Status` default `DRAFT` (**tanpa CHECK**); `CurrentApprovalLevel` default 0; `DataJson` jsonb; audit + Submitted/Approved/ClosedAt. Index: Commodity, CompanyId, FormId, InspectionDate, `(PeriodYear,PeriodMonth)`, Status, `(Status,CurrentApprovalLevel)`, WilayahKode.
- **TrnQcApproval** — FK InspectionId CASCADE, ApproverUserId/GroupId → UtlUser/UtlGroup SET NULL + snapshot nama; `Level`, `Action`, `Status` default `PENDING` (tanpa CHECK), `Comment`, `ApprovedAt`; **UNIQUE `(InspectionId, Level)`** (belum memuat dimensi Revision).
- **TrnQcFinding** — `FindingNo` unique partial; FK InspectionId RESTRICT, Pic/VerifiedBy → UtlUser SET NULL + snapshot; `Severity` default MINOR, `Status` default OPEN (tanpa CHECK), `TargetDate`, `FollowUpNote`, `VerifiedDate`; index InspectionId/Severity/Status/TargetDate.
- **HistoryQcInspection** — FK InspectionId RESTRICT, UserId SET NULL; `Action`, `FromStatus`, `ToStatus`, `ApprovalLevel`, `Note`, `CreatedAt`; index `(InspectionId, CreatedAt)`.
- **View sudah ada** (dibuat `sql/2026-11-18_monitoring_views.sql`, ada di rollback): `VwQcInspectionMonitoring` (daftar inspeksi + FormCode/Name + OpenFindingCount/OverdueFindingCount, zona waktu `Asia/Jakarta`) dan `VwQcFindingMonitoring` (temuan + `IsOverdue`). Dipakai konsumen analisis/dashboard, **bukan view hitungan detail/rekap form**.
- **Tidak ada**: `MstQcFormApprovalStep`, tabel cache master, `MstQcNumberSeq`, tabel detail per pola, `MstQcPenyesuaianPopulasi`, view `vwQc*` hitungan.

## 3. Implementasi QC yang sudah berjalan (backend + frontend)

Backend (`src/AuthUtility.Api/Controllers/Qc*.cs`, helper `Qc/`):
- `api/qc/forms` GET `/`, `/{code}` · `api/qc/inspections` GET `masters/persil`, GET `/`, GET `/{id}`, POST `/`, PUT `/{id}`, POST `/{id}/submit|resubmit|approve|reject|request-revision|close` · `api/qc/approvals` GET `flow/{formCode}`, `pending` · `api/qc/findings` GET `/`, `/{id}`, POST `/`, POST `/{id}/follow-up|verify|close` · `api/qc/dashboard` GET `/` (maks 366 hari, `Dashboards/QcDashboardCalculator.cs`).
- `QcStatusEngine`: transisi `DRAFT → WAITING_APPROVAL → APPROVED/REJECTED/REVISION_REQUIRED → CLOSED` (close wajib semua finding CLOSED). **Berbeda** dari rancangan prompt (`SUBMITTED/IN_REVIEW/REVISION`).
- Level approval **dari `appsettings.json` seksi `Qc:ApprovalFlows:{formCode}:Levels`** (3 form pilot × 3 level), bukan tabel master.
- Penomoran `QcNumberGenerator`: `QC-{prefix}-{year}-{seq6}` (contoh `QC-KLP-2027-000001`), prefix dari `Qc:Numbering:PrefixByCommodity`; seq = identity Id pad 6. **Berbeda** dari format rancangan `{FormCode}/{Wilayah}/{yyyy}/{MM}/{urut4}` dan belum atomik lewat tabel seq.
- `DataJson` = **hibrida**: baris detail per form (`samples`, `rows`, `fertilizers`) + ekstra header. Rancangan prompt melarang detail di `DataJson` → perlu migrasi ke tabel detail (data transaksi masih 0 baris, jadi migrasi murah sekarang).
- `QcPersilReader` (partial `PersilReader`): baca live MyPAD `vwPersilNNS` / `vwMstPersilKelapa`, hasil hanya `PersilId/PersilKode/Wilayah/Afdeling/Subpersil` — **belum** luas/tgl tanam/jumlah pohon/jalur/KCB/umur.

Frontend (`src/features/qc/`):
- Route: `/qc/dashboard`, `/qc/inspeksi` (+`/qc/monitoring`, `/qc/report` redirect), `/qc/buat-inspeksi`, `/qc/inspeksi/:id`, `/qc/inspeksi/:id/edit`, `/qc/approval`, `/qc/finding`(+`/:id`).
- Grid input **hardcoded per form** (`forms/*/…Form.tsx`, registri `QC_FORM_COMPONENTS` di `forms/qcFormRegistry.ts`, batas baris hardcoded 66/44/50/7/200). Belum config-driven (definisi kolom/grup judul/rumus tampil).
- Export **sudah ada**: manifest 11 template `data/qc-template-manifest.json`; `.xlsx` hasil konversi committed di `public/qc-templates/` (skrip `scripts/prepare-qc-export-templates.mjs`); `qcExportTemplates.ts` (manifest+logo), `qcLegacyXlsExport.ts` (patch biner BIFF `.xls`), `qcPdfLayout.ts` (jsPDF), `qcCaseExport.ts`, `QcExportPreviewDialog.tsx`. Fase 4 dapat memakai ulang jalur ini, bukan membangun dari nol.
- Komponen approval: `QcApprovalFlowTimeline`, `QcApprovalTimeline`, `QcStatusBadge/QcBadge`.

## 4. Master yang sudah tersedia di API (dipakai ulang, SQL Server read-only)

| Kebutuhan | Sumber yang sudah ada di API | Catatan |
|---|---|---|
| Persil kelapa/nanas/panen | `Sensus/PersilReader.cs` 4 sumber: KLP=MyPAD `tblMstPersilKelapa`, TNM_NNS=`tblNNSTrnInventarisTanamanNenas`, LHN_NNS=`tblNNSTrnInventarisLahanNenas`, PNN=RSUPPKB `tblMstPersilKelapa`; endpoint `api/sensus/persil`, `api/sensus/mobile/persil` | Kolom agronomis tersedia di tabel sumber: `KCB`, `TglTanamKelapa`/`TglTanam`, `JumlahPohonKelapa`/`JumlahTanaman`, `JumlahBaris`/`Jalur`, `LuasLahan*`/`HA`, `KodeWilayah/KodeAfdeling/KodeDivisi`, `NotActive` |
| Persil QC saat ini | `Qc/QcPersilReader.cs` → `api/qc/inspections/masters/persil` (MyPAD `vwPersilNNS`, `vwMstPersilKelapa`) | Perlu diperluas lewat reader yang sama (bukan view baru) agar membawa luas/tanam/pohon/jalur/KCB untuk header otomatis & SPH |
| Wilayah | `PersilReader.WilayahAsync` → `api/sensus/persil/wilayah`; `api/sensus/wilayah` (distinct data sensus) | Nama wilayah: `tblMstWilayah` (MyPAD `KodeWilayah/NamaWilayah/Manager/Jabatan`; RSUPPKB `Kode/Nama/...`) belum diekspos endpoint — bisa dibaca lewat konteks yang sudah ada bila diperlukan |
| Divisi/Afdeling | Melekat pada baris persil (`KodeDivisi`, `KodeAfdeling`) | Tidak ada endpoint master divisi/afdeling tersendiri; usul: turunkan dari baris persil yang sudah dibaca API, tanpa membuka view lain |
| Pegawai/karyawan | `api/utility/pkb-payroll/employees` (`PKBPayrollEmployeeController`, PKBPayroll `vwMstKaryawanPkbJkt`, read-only) | Untuk approver per jabatan/afdeling perlu dicek field yang tersedia; jabatan user aplikasi sendiri sudah ada di PostgreSQL (`UtlUser.GroupId → UtlGroup.Name`, pola dipakai laporan RNA) |
| Pemborong/Mandor | Belum ada endpoint API | Ada di sumber (`tblMstPemborong`, `VwMstMandorAll`, `vwMstPemborongMyPadDanRsupPkb`) tetapi sesuai arahan tidak dibuka sendiri → pertanyaan #6 |

Temuan penting soal view: **`vwMstPersilAll` dan `vwMstPersilAllQC` (MyPAD 31.777 / RSUPPKB 1.839 baris) bukan master agronomis** — isinya level subpersil/absensi Android (`KodeSubPersil`, `UserID`, koordinat, `Toleransi`, `PersilType`). Jangan dipakai sebagai sumber cache persil QC. Master agronomis justru tabel yang sudah dipakai `PersilReader` di atas.

## 5. Inventaris 11 file Excel (`../docs/file`, semua ada)

| File | Sheet | Kode form | Acuan versi | Pola | Catatan kolom/header |
|---|---|---|---|---|---|
| PL NENAS.xls | PL-AWAL | FRM-QC-N-001-00 | 2025 | A | Header ekstra "Tinggi permukaan lahan dari air"; faktor: luas gulma hijau, tinggi batangan sisa ≥3cm, tunasan/mahkota, jarak tumbang <70cm, alang-alang; satuan m²/batang/% |
| HORMON.xls | HORMON | FRM-QC-N-009-00 | 2025 | A | Kolom umur buah 60-79/80-99/100-119/>120, total nns buah musim, %; footer masih menunjuk path `12.PANEN NENAS.xls` (basi) |
| PANEN NENAS.xls | PNN NNS | FRM-QC-N-012-00 | 2025 | A | `No.Icar` + `No.Icar lama`, header "Umur Panen"; buah tdk dipetik/tlh dipetik/terpotong/total/%; footer path basi `17.PEMUPUKAN KELAPA` |
| PUPUK TABUR NNS.xls | PPK TABUR (2) / PPK TABUR / PPK DASAR / Sheet1(kosong) | FRM-QC-N-006-00 (tabur, 2 sheet versi 2025 & 2014); **PPK DASAR ganda: FRM-QC-N-02-00 (L32) & FRM-QC-N-06-00 (L69)** | 2025 untuk tabur; PPK DASAR 2012 | B | Baris per jenis pupuk (UREA/MOP/CuSO4/CaO/FeSO4/…); takaran kg/ember, jml phn/baris, jml baris, dosis schedule vs riil grm/phn, deviasi %; PPK DASAR adds luas sampel (ha) & No jalur sampel |
| PUPUK POWER NNS.xls | PPK POWER | FRM-QC-N-007-00 | 2025 | B | Jenis: Urea, MOP, CuSO4, CaO, FeSO4, ZnSO4, RP, Ethrel; kg/drum, phn/baris, baris, dosis schedule/riil, % |
| PEMUPUKAN KELAPA.xls | sheet1 | FRM-QC-K-017-00 | 2026 | A | Faktor: tertinggal, pupuk menggumpal, CW kotor (gulma/serasah), tdk sesuai dosis/tdk rata, penempatan ppk non standar; total non spec % |
| PANEN KELAPA.xls | PANEN REV-1 / PANEN (RSTM) | FRM-QC-K-019-01 (RSUP) / FRM-QC-K-019-00 (RSTM) | REV-1 2025 | A | **Varian perusahaan**: PT Riau Sakti United Plantations vs PT Riau Sakti Transmandiri; set kolom berbeda (losses panen/peletakan sabut/kondisi tanaman vs faktor non spec); footer jabatan berbeda |
| INVENTARIS KELAPA.xls | INEVNTARIS KELAPA / (2) | **FRM-QC-011-00 (tanpa huruf komoditas)** | sheet (2) lebih baru | C | Bulan tanam, jumlah tanam hitungan Wilayah vs QC/QMR, TM/TBM, tumbang/mati/stres/non-hybrid, bibit non spec, total + %; footer lama (PL Quality/Mandor, Ka.Mandor, SAMgr QMR, Manager Wilayah) vs baru (QC, Asisten QC, Senior Manager, Manager Wilayah) |
| TEBAS + SANITASI.xls | CONTOH / TEBAS TOTAL+SANITASI | **tidak ada kode FRM di file** | TEBAS TOTAL+SANITASI 2025 | A | Varian manual/mekanis di judul; versi baru: tinggi sisa >20/10cm & <20/10cm (m²+% per faktor), sanitasi (plph/tandan kering/sabut, gulma merambat, buah bajang, kelapa tumbuh, perdu), kolom H/B; CONTOH 2016 pakai >10cm |
| MONITORING PENGIRIMAN NENAS.xls | Book1 | **tidak ada kode FRM** | — | D | Header No.IB, operator, berangkat, tiba di P2, Ka Pemborong; detail persil/KCB/jenis kegiatan/TK BOR-KHL/qty N1–N5 dengan sub NA/NB/NC/total/mulai-selesai muat/tindakan preventif/keterangan/ttd wilayah; footer hanya Mandor & Ka.Mandor (bukan 4 tanda tangan) |
| Kondisi Kebersihan Kebun Nanas (Oktober 2026).xls | Sheet2 / GRAFIK | **tidak ada kode FRM** | Okt 2026 | E | Per persil: Kcb, Bln/Thn Tanam, Umur Tanam, Jlh Inv Tanaman, Jumlah Jlr, Luas (Ha), SPH; kelas gulma 0-<5, 5-<10, 10-<25, 25-<50, >50 (ha); Grade H/AK-AM/K-M; vegetatif warna daun + Sbr/Sdg/Krs/Toksik; legenda di bawah; contoh penyesuaian populasi `003/BRA/WIL.2/01/2026 (PENGURANGAN POHON)`; **judul sheet GRAFIK masih "September 2026" dan nilainya 0** (basi) |

Footer umum 4 tanda tangan: Dilaporkan (QC/Inspektor) → P. Jawab Afd (Asisten Wilayah/Asst. Afd) → Diperiksa (Asst. QC) → Disetujui (Snr. Mgr QC); nama+jabatan+tanggal. Penyimpangan: INVENTARIS (4 pihak berbeda), PENGIRIMAN (2 pihak), PL NENAS (jabatan Askep afd nenas/Asst. Mgr QC/Snr.Asst.Mgr + satu tanggal typo `/ /2015`).

## 6. Gap terhadap desain prompt induk + usul

1. **Versi form**: `MstQcForm` belum punya `Version/EffectiveFrom/Pattern/TemplateFile/PrintTitle`; unique `(Code)` menghalangi multi-versi → tambah kolom + unique `(Code, Version)`; seed 11 form kode `FRM-QC-*`; putuskan nasib 3 baris `QC-*` (pertanyaan #10).
2. **Approval master-driven**: level kini di appsettings → pindah ke `MstQcFormApprovalStep` (seed 4 level; level 1 = pembuat). Status engine kini `WAITING_APPROVAL/REJECTED/REVISION_REQUIRED` → selaraskan ke `DRAFT/SUBMITTED/IN_REVIEW/REVISION/APPROVED/CLOSED` + CHECK constraint; tambah `Revision` dan unique `(InspectionId, Level, Revision)`.
3. **Detail di DataJson** → migrasikan ke tabel detail per pola (A–E). Transaksi masih 0 baris: migrasi data trivial, tetapi struktur `DataJson` frontend harus ikut pindah (registry kolom config-driven sekalian, Fase 3).
4. **View hitungan**: belum ada view detail/rekap per form; hanya 2 view monitoring. Tambahkan view per pola + index pendukung `(FormId, PeriodYear, PeriodMonth)`, `(WilayahKode, KodeAfdeling)`, `(InspectionId)` di tabel detail.
5. **Cache & sync master**: belum ada tabel cache/service sync; persil dibaca live. Sesuai arahan: cache dibangun dari **reader API yang sudah ada** (`PersilReader`/`QcPersilReader`, employees endpoint), upsert idempoten + tombol admin + jadwal; bukan membuka view SQL Server baru.
6. **Header otomatis & SPH/umur**: reader QC belum membawa luas/tgl tanam/pohon/jalur/KCB → perluas reader yang sama; umur tanam & SPH tetap **dihitung di view**, bukan disimpan.
7. **Penomoran**: format kini `QC-{prefix}-{year}-{seq6}` vs rancangan `{FormCode}/{Wilayah}/{yyyy}/{MM}/{urut4}`; belum ada `MstQcNumberSeq` atomik (`INSERT … ON CONFLICT DO UPDATE … RETURNING`). Format final menunggu jawaban #3.
8. **Kebersihan bulanan (E)**: belum ada tabel periode/penyesuaian populasi; aksi "Buka periode" dan view rekap wilayah pengganti sheet GRAFIK belum ada; periode harus parameter (file contoh sendiri basi bulan).
9. **Export**: jalur template sudah ada (`.xlsx` committed + patch BIFF legacy + jsPDF). Fase 4 = rekonsiliasi manifest dengan versi form terpilih + isi dari view + footer tanda tangan dari `TrnQcApproval` + watermark DRAFT; hindari dua jalur export yang tumpang tindih.
10. **Grid config-driven**: komponen form hardcoded → pindahkan definisi kolom/grup judul/rumus tampil ke konfigurasi (Fase 3) agar pola A berikutnya hanya menambah konfigurasi.
11. **Constraint/index**: `Status` di 3 tabel tanpa CHECK; `TrnQcApproval` unique tanpa Revision; belum ada FK/index tabel detail (belum ada tabelnya).
12. **Dokumentasi & menu**: seed menu QC sudah ada (7 anak); tambah menu master QC bila Fase 1 menambah halaman master, ikuti pola `UtlMenu/UtlGroupMenu`.

## 7. Pertanyaan untuk pengguna (maks 10, urut prioritas)

1. **Kode form hilang/ganda**: kode resmi untuk TEBAS+SANITASI, MONITORING PENGIRIMAN NENAS, dan KEBERSIHAN BULANAN (tidak ada di file); konfirmasi `FRM-QC-011-00` (tanpa huruf N/K) untuk Inventaris Kelapa; dan kode ganda PPK DASAR (`FRM-QC-N-02-00` vs `FRM-QC-N-06-00`) — mana yang dipakai?
2. **Versi acuan**: konfirmasi sheet terbaru sebagai acuan (PPK TABUR (2) 2025, PANEN REV-1, INVENTARIS sheet (2), TEBAS TOTAL+SANITASI 2025); apakah varian **RSTM** (`FRM-QC-K-019-00`, kolom & footer berbeda) menjadi form terpisah dengan `CompanyId`, atau satu form dengan varian layout?
3. **Format No. Icar lapangan**: contoh nyata yang dipakai di lapangan (rancangan default `{FormCode}/{WilayahKode}/{yyyy}/{MM}/{urut4}`); apakah `No. Icar lama` wajib saat revisi dokumen lama?
4. **Pemetaan approval**: group/jabatan MyHelpdesk mana untuk level 2 (P. Jawab Afd/Asisten Wilayah), 3 (Asst. QC), 4 (Snr. Mgr QC); apakah level 2 **wajib sesuai afdeling persil**, dan dari mana relasi pegawai→afdeling diambil (endpoint employees yang ada belum tentu memuat afdeling)?
5. **Sumber cache persil**: konfirmasi cache dibangun dari reader API yang sudah ada (KLP/TNM_NNS/LHN_NNS/PNN + perluasan `QcPersilReader`), **bukan** `vwMstPersilAll`/`vwMstPersilAllQC` (terbukti level subpersil absensi)?
6. **Pemborong & Mandor**: karena belum ada endpoint API-nya, apakah dibuat master lokal `MstQcPemborong`/`MstQcMandor` (CRUD di PostgreSQL), atau reader read-only baru terbatas pada tabel sumber yang ditunjuk pengguna?
7. **Standar/target % per form** untuk validasi input & tombol "Buat temuan": ada dokumen resmi, atau mulai dari ambang yang terlihat di contoh Excel?
8. **Kebersihan bulanan**: header ringan per periode per wilayah, atau ikut header inspeksi generik? Sumber "Jlh Inv Tanaman" = `JumlahTanaman` inventaris Nenas + total `MstQcPenyesuaianPopulasi` — konfirmasi?
9. **Entitas/PT**: RSUP vs RSTM vs PKB dalam satu sistem dengan `CompanyId` (kolom sudah ada) — apakah footer/jabatan tanda tangan ikut perusahaan, dan apakah form kelapa RSTM dipakai aktif?
10. **Nasib seed lama**: 3 baris `MstQcForm` berkode `QC-*` (transaksi 0 baris) — nonaktifkan dan seed 11 form `FRM-QC-*`, atau rename kode lama ke kode baru agar referensi tetap hidup?

## 8. Cara memverifikasi recon ini

- Skema PG: dump `information_schema.columns` + `pg_constraint` + `pg_indexes` + `COUNT(*)` per tabel QC (read-only).
- SQL Server: `INFORMATION_SCHEMA.COLUMNS` / `sys.objects` + `SELECT TOP 3` hanya pada objek yang sudah dipakai API atau disebut prompt; tidak ada penulisan apa pun ke MyPAD/RSUPPKB/PKBPayroll.
- Excel: baca `.xls` via SheetJS; dump struktur (sheet, merge, 16 baris awal, 8 baris akhir, sel berkode FRM) disimpan sementara di luar repo lalu dihapus.
- Build/test tetap seperti §1; tidak ada perubahan kode pada Fase 0.

**BERHENTI — menunggu perintah fase berikutnya ("lanjut" / "Kerjakan Fase 1").**