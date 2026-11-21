# Fase 2 — Workflow Inspeksi, Approval Master-Driven, Penomoran Atomik

Tanggal: 2026-11-19 · Database: `helpdeskasd` (PostgreSQL) · Status: selesai, build & test hijau.

## 1. Lingkup

Fase 2 replacing alur approval hardcoded menjadi master-driven (`MstQcFormApprovalStep`),
menambah siklus revisi ber-audit, penomoran inspeksi atomik per scope, field header
pemborong/mandor/inspektur/No. ICAR lama, dan lifecycle temuan
`OPEN → IN_PROGRESS → DONE → VERIFIED`.

## 2. Perubahan skema (migrasi manual)

Script: `sql/2026-11-19-qc-fase2-workflow.sql` (idempotent, tanpa BEGIN/COMMIT internal).
Rollback: `sql/2026-11-19-qc-fase2-workflow-rollback.sql` (transaksi internal + guard).

- `MstQcFormApprovalStep.IsCreatorStep` (bool, default false); backfill `Level=1, Label='Dilaporkan'` → true (13 baris).
- `TrnQcInspection`: + `PemborongId`, `MandorId`, `InspectorUserId` (FK `ON DELETE SET NULL`),
  `InspectorNameSnapshot varchar(150)`, `NoIcarLama varchar(100)`, `Revision int >= 0`.
  `InspectionNo` diperlebar `varchar(50) → varchar(150)` (format baru lebih panjang).
- `TrnQcApproval`: + `Revision int >= 0`; index unik diganti
  `(InspectionId, Level)` → `(InspectionId, Level, Revision)` — baris revisi lama tetap disimpan.
- CHECK status baru:
  - inspeksi: `DRAFT, SUBMITTED, IN_REVIEW, REVISION, APPROVED, CLOSED`
  - approval: `PENDING, APPROVED, REVISION_REQUIRED, REJECTED` (REJECTED hanya legacy)
  - action: `'', APPROVE, REQUEST_REVISION, REJECT` (REJECT hanya legacy)
  - temuan: `OPEN, IN_PROGRESS, DONE, VERIFIED`
- Pemetaan data lama (idempotent): `WAITING_APPROVAL` → `SUBMITTED` (level ≤ 1) / `IN_REVIEW` (level > 1);
  `REVISION_REQUIRED` → `REVISION`; `REJECTED` → `CLOSED`; finding `FOLLOW_UP` → `IN_PROGRESS`,
  `WAITING_VERIFICATION` → `DONE`, `CLOSED` → `VERIFIED`.
- View baru: `VwQcInspectionList` (daftar + hitungan temuan aktif/telat + label tahap berjalan),
  `VwQcApprovalPending` (antrean per tahap + metadata group/scope).
  View monitoring Fase 1 dibuat ulang dengan kolom tambahan di akhir
  (`Revision, PemborongId, MandorId, InspectorUserId, NoIcarLama`; filter temuan aktif = `OPEN/IN_PROGRESS/DONE`).
- Semua view yang menyentuh `InspectionNo` di-drop sebelum ALTER TYPE lalu dibuat ulang —
  syarat idempotensi PostgreSQL (view mengikat tipe kolom).

### Verifikasi migrasi (live helpdeskasd)

| Uji | Hasil |
| --- | --- |
| Apply pertama | sukses (setelah perbaikan drop-view di atas) |
| Re-run ×2 (idempotensi) | 0 error |
| Rollback | sukses; `InspectionNo` kembali 50, kolom Fase 2 hilang, index unik lama kembali, view Fase 1 pulih |
| Re-apply setelah rollback | sukses; kondisi Fase 2 identik |

Catatan: uji rollback dijalankan saat seluruh tabel transaksi QC kosong (0 baris),
sesuai batasan script. Guard rollback (`RAISE EXCEPTION` bila ada revisi ganda atau
`InspectionNo` > 50 karakter) tidak dipicu karena data kosong.

## 3. Model status & aturan

- `PendingApproval = {SUBMITTED, IN_REVIEW}`; `Editable = {DRAFT, REVISION}`;
  `FindingCreatable = {DRAFT, SUBMITTED, IN_REVIEW, REVISION, APPROVED}`; terminal inspeksi = `CLOSED`.
- Temuan: `Active = {OPEN, IN_PROGRESS, DONE}` — **DONE masih menghitung sebagai terbuka**
  (belum diverifikasi) dan masih memblokir CLOSE inspeksi. Terminal temuan = `VERIFIED`.
- APPROVED terkunci; koreksi lewat REVISION (wajib komentar) lalu RESUBMIT.
  Setiap perubahan status tercatat di `HistoryQcInspection` dalam transaksi yang sama.

## 4. Alur approval (master-driven)

Sumber kebenaran: langkah aktif per form, urut `Level` (`QcApprovalFlow`).
Tanpa langkah → fallback satu langkah manual "APPROVED OLEH" (hak approve `QC_APPROVAL`).

1. **Submit** → tahap pertama. Bila `IsCreatorStep` (level 1 "Dilaporkan"):
   baris approval pembuat otomatis `APPROVED` (snapshot nama pembuat), lalu baris
   `PENDING` di tahap berikutnya; status `SUBMITTED` (atau langsung `APPROVED` bila
   tidak ada tahap lanjutan). Tanpa langkah pembuat → `PENDING` di tahap pertama.
2. **Approve** → baris `APPROVED`, baris `PENDING` berikutnya pada **Revision yang sama**,
   status `IN_REVIEW`; tahap terakhir → `APPROVED` + `ApprovedAt`.
3. **Minta revisi** (komentar wajib) → baris `REVISION_REQUIRED`, inspeksi `REVISION`,
   `CurrentApprovalLevel` tetap (resubmit kembali ke tahap yang meminta revisi).
4. **Resubmit** → `Revision + 1`, baris creator otomatis baru + `PENDING` baru pada revisi baru;
   baris revisi lama tidak dihapus (audit). `SubmittedAt` diperbarui.
5. **Close** → hanya dari `APPROVED` dan bila tidak ada temuan `Active`.
6. Endpoint **reject dihapus** (nilai `REJECTED` hanya dibaca untuk data legacy).

Kelayakan approver (`QcAccess.CanApproveStep`): superadmin, atau `GroupId` user =
`ApproverGroupId` langkah, atau punya hak Approve pada `MenuCode` langkah.
Scope afdeling (`MatchesAfdeling`): superadmin lolos; kode afdeling inspeksi kosong lolos;
`DeptId` user kosong lolos (asumsi, lihat §8); selain itu harus sama (trim, case-insensitive).
Langkah pembuat tidak bisa di-approve manual.

## 5. Penomoran atomik

- `MstQcNumberSeq` + `IQcNumberSeqStore`: `INSERT … ON CONFLICT (ScopeKey) DO UPDATE … RETURNING`
  (parameterized, ikut transaksi EF berjalan) → tidak ada nomor ganda saat konkuren.
- Nomor inspeksi: `{FormCode}/{Wilayah}/{yyyy}/{MM}/{seq4}` per scope
  `form|wilayah|tahun|bulan`; wilayah kosong → `-`. Pola dapat dikonfigurasi
  (`Qc:Numbering:InspectionPattern`, token `{formCode} {wilayah} {yyyy} {MM} {seq}` + padding).
- Nomor temuan: `QC-{KLP|NNS}-F-{tahunWIB}-{seq6}` berbasis identity.
- Versi form: create memakai versi aktif terbaru (`Code, Version` unik); inspeksi lama
  tetap menampilkan `FormVersion` miliknya.

## 6. API

`QcInspectionsController` (ditulis ulang):
- `GET masters/referensi` — pemborong & mandor aktif (hak Create `QC_BUAT_INSPEKSI`),
  dipakai dropdown form buat/edit.
- Create/update: validasi FK (null/absen = tetap, `0` = hapus relasi, selain itu wajib
  master aktif); `InspectorUserId` me-resolve snapshot nama, `InspectorName` manual menimpa.
- `POST {id}/submit|resubmit|approve|request-revision|close`; `reject` dihapus.
- List/detail mengembalikan `revision`, nama pemborong/mandor/inspektur, `noIcarLama`,
  `formVersion`, `allowedActions` per user (owner/superadmin vs approvereligible).

`QcApprovalsController`: `GET flow/{formCode}` (level + label + jabatan + group + scope +
isCreatorStep) dan `GET pending` (antrean sesuai kelayakan + scope afdeling user).

`QcFindingsController`: `POST {id}/follow-up` (OPEN|IN_PROGRESS → IN_PROGRESS, note
distempel `[dd/MM/yyyy HH:mm nama]`), **baru** `POST {id}/done` (→ DONE, note opsional),
`POST {id}/verify` (hanya dari DONE → VERIFIED + verifikator + tanggal WIB);
endpoint close temuan dihapus.

Dashboard: field `waitingApproval/revisionRequired` → `pendingApproval/revision`;
temuan DONE dihitung aktif.

## 7. Frontend (MyHelpDesk)

- Types/status/config mengikuti model baru; label Indonesia (`Menunggu Approval`,
  `Dalam Review`, `Perlu Revisi`, `Selesai (Menunggu Verifikasi)`, dst).
- Halaman buat & edit inspeksi: dropdown Pemborong/Mandor (mandor terfilter pemborong),
  input Inspektur (nama) & No. ICAR lama; edit mengirim `0` untuk menghapus relasi.
- Detail inspeksi: ringkasan pemborong/mandor/inspektur/versi form/revisi;
  timeline approval per revisi berjalan (`QcApprovalFlowTimeline`), revisi sebelumnya
  dalam `<details>`; aksi hanya Approve/Minta Revisi (komentar wajib).
- Halaman approval: metadata tahap (jabatan, group, scope afdeling) + badge revisi.
- Temuan: daftar & detail dengan aksi Tindak lanjut / Tandai selesai / Verifikasi.
- Master QC tab Alur: checkbox "Langkah pembuat (Dilaporkan)" + kolom Pembuat di tabel.
- Dashboard & daftar inspeksi memakai field/status baru; daftar menampilkan "Revisi ke-n".

## 8. Asumsi & pertanyaan §6 yang masih terbuka

- `DeptId` kosong dianggap tanpa scope afdeling (lolos semua tahap scope) — menunggu
  konfirmasi mapping jabatan/group → level dan sumber afdeling user.
- `NoIcarLama` teks bebas (format nomor ICAR belum dikonfirmasi).
- Pemborong/mandor diambil dari master QC lokal (`MstQcPemborong`/`MstQcMandor`),
  bukan dari SQL Server sumber — menunggu konfirmasi asal data.
- Standar/target % per form belum diisi (menunggu §6); belum ada validasi target di Fase 2.

## 9. Test & build

- `dotnet test -c Release`: suite penuh hijau kecuali 1 kegagalan WAD pre-existing
  (`WadReportControllerTests.DeniedPermissions_StopBeforeDatabaseAndFileAccess`, sudah
  gagal di HEAD bersih — di luar lingkup QC).
- Test QC baru/diperbarui: `QcApprovalFlowTests` (8: alur 4 tahap + creator otomatis,
  fallback, creator-only, siklus revisi, kelayakan group/menu, scope afdeling,
  metadata flow/antrean, guard status), `QcNumberingTests` (6: sekuensial per scope,
  scope independen, wilayah kosong, pola konfigurasi, nomor temuan, 200 konkuren),
  `QcFindingFlowTests` (3: lifecycle penuh + note terstempel, close inspeksi,
  done langsung dari OPEN + validasi severity), `QcMasterTests` (+creator hanya level 1),
  `QcDashboardTests` (status baru, DONE aktif).
- Frontend: `npm run build` (tsc -b + vite) hijau.
- Verifikasi browser (harness fixture sementara, sudah dihapus): daftar inspeksi
  (status baru + badge revisi), detail revisi (timeline revisi berjalan + riwayat
  revisi lama), antrean approval (metadata tahap + dialog revisi wajib komentar),
  temuan (aksi per status + note terstempel), buat inspeksi (kaskade pemborong→mandor,
  simpan draft), edit (inisialisasi header + simpan), dashboard (pendingApproval/revision),
  master Alur (kolom Pembuat + checkbox langkah pembuat). Tanpa error konsol terkait QC.