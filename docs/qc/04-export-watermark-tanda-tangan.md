# Fase 4 — Export Excel/PDF: Watermark Status & Tanda Tangan dari Approval

## Lingkup

Fase 4 menyempurnakan pipeline export frontend yang sudah ada (bukan membangun ulang):

1. **Watermark status** pada Excel (.xlsx dan .xls legacy) serta PDF untuk inspeksi yang
   belum final. Inspeksi `APPROVED`/`CLOSED` dicetak bersih tanpa watermark.
2. **Tanda tangan dari snapshot approval** — nama & tanggal_approver diisi ke area tanda
   tangan template, termasuk perbaikan pemetaan nama pada Panen Nenas (kotak merge) dan
   penghapusan duplikasi nama pelapor.

Tidak ada perubahan skema/database pada fase ini. Seluruh perubahan di frontend
(`MyHelpDesk/src/features/qc/api/`). Backend tetap seperti Fase 3 (345 test lulus + 1
kegagalan WAD pre-existing yang tidak disentuh).

## Desain watermark

Watermark ditentukan oleh `status` inspeksi, bukan oleh ada/tidaknya approval:

| Status | Label watermark |
| --- | --- |
| `DRAFT` | `DRAFT` |
| `SUBMITTED` | `SUBMITTED` |
| `IN_REVIEW` | `IN REVIEW` |
| `REVISION` | `REVISION` |
| `APPROVED` | (tidak ada) |
| `CLOSED` | (tidak ada) |

Helper `watermarkText(status)` di `qcCaseExport.ts` memetakan status → label atau `null`.
Label memakai kode status apa adanya (bahasa Inggris sesuai enum) agar tidak ambigu;
dokumen ini mencatat pilihannya. Watermark bersifat informatif, tidak mengubah data sel.

### Excel .xlsx (template Nanas)

Watermark disuntik sebagai **shape teks DrawingML** (`xdr:sp` txBox) pada drawing worksheet,
bersamaan dengan logo yang sudah direstorasi oleh `restoreExcelImages`:

- `makeWatermarkAnchor(text, sheet, id)` membuat `twoCellAnchor` yang membentang seluruh
  area terpakai (`!ref`), rotasi `-2700000` (= −45°), teks di tengah, font Arial 80pt tebal,
  warna `C9CED6` dengan `alpha 45000` (transparan ~45%), tanpa fill/garis.
- Karena berupa shape mengambang, watermark **tidak mengubah sel/layout** template dan
  terlihat baik di tampilan normal maupun cetak.
- `restoreExcelImages` kini memproses sheet yang punya logo **atau** watermark. Shape logo
  (`xdr:pic`) dan watermark hidup dalam satu drawing; id shape unik. Sheet tanpa logo tetap
  mendapat drawing berisi watermark saja. Relasi & content-types disesuaikan.
- Untuk `APPROVED`/`CLOSED` (`watermark = null`) perilaku kembali seperti semula: hanya logo.

### Excel .xls legacy (Kelapa / Tebas / Inventaris)

Format BIFF8 tidak praktis untuk shape mengambang, maka watermark dipasang sebagai
**print header** (record `HEADER` 0x0014) berisi `&C<label>` (rata tengah), sehingga muncul
di setiap halaman cetak/Page Layout. Implementasi di `qcLegacyXlsExport.ts`:

- `makeHeaderRecord(text, id)` membangun record BIFF8 (cch + grbit + string terkompresi).
- `patchWorkbookStream(input, edits, watermark)` — bila watermark diisi, record
  `HEADER`/`FOOTER` (0x0014/0x0015) bawaan template **diganti** (bukan ditumpuk) dan header
  baru disisipkan sebelum EOF tiap sheet yang diedit. Bila `null`, header asli dibiarkan.
- `fillLegacyXlsTemplate(url, edits, watermark)` meneruskan parameter ke patcher.

### PDF

`exportQcCasePdf` setelah seluruh section digambar, bila `watermarkText(item.status)` ada,
mengulang tiap halaman: `setPage`, font helvetica bold 78pt, warna abu terang (201,206,214),
`doc.text(label, width/2, height/2, { align:'center', baseline:'middle', angle:45 })`.
Watermark diagonal di tengah halaman, di atas konten namun warna terang sehingga data tetap
terbaca. Untuk status final tidak ada stamp.

## Tanda tangan dari approval

### Akar masalah: duplikasi nama pelapor

Flow approval sebenarnya (diverifikasi read-only dari `MstQcFormApprovalStep`) memiliki
**4 level** dengan level 1 = langkah pembuat (`IsCreatorStep = true`):

| Level | Label | JabatanKode | Peran template |
| --- | --- | --- | --- |
| 1 | Dilaporkan | `INSPEKTUR_QC` | Inspektor / Quality Control (pembuat) |
| 2 | P. Jawab Afd | `ASISTEN_WILAYAH` | Asst. Afd |
| 3 | Diperiksa | `ASST_QC` | Asst. QC |
| 4 | Disetujui | `SNR_MGR_QC` | Snr Manager |

Saat submit, backend menambahkan baris approval level 1 **atas nama pelapor** (APPROVED
otomatis). Jadi `detail.approvals` sudah memuat pelapor di level 1. Kode export lama
menyusun signer sebagai `[createdByName, ...approvals.map(nama)]` sehingga pelapor
**terhitung dua kali** dan signer level 4 tergeser/hilang (kotak ke-4 tidak terisi benar).

### Perbaikan: `buildSigners(item, detail)`

Satu helper terpadu dipakai oleh semua path (generic, PNN eksplisit, legacy, PDF):

```
approvals = detail.approvals diurutkan naik menurut level
bila approvals ada:
  names = approvals.map(approvedName)      // level 1 = pelapor, tidak di-prepend lagi
  dates = approvals.map(tanggal approved)  // '' bila belum APPROVED
bila belum ada approval (DRAFT):
  names = [createdByName]
  dates = [formatDate(inspectionDate)]
```

`approvedName` hanya mengembalikan nama bila status approval `APPROVED` (snapshot nama
dipertahankan). Hasil: 4 signer tepat memetakan 4 kotak template; saat DRAFT hanya pelapor
yang terisi, kotak lain kosong; saat SUBMITTED pelapor terisi dan approver berikutnya kosong
sampai disetujui.

Path yang diperbarui:
- `fillTemplateSignatures` (generic, dipakai Pupuk/Kebersihan) — sumber names/dates dari
  `buildSigners`.
- `fillKnownPilotData` cabang PNN NNS — nama & tanggal eksplisit dari `buildSigners`.
- `getLegacyTemplateEdits` (19/37/31) — `signerNames`/`signerDates` dari `buildSigners`.
- `exportQcCasePdf` — kolom tanda tangan PDF dibangun dari `buildSigners`; label kolom pertama
  `Dilaporkan oleh`, berikutnya `Persetujuan tahap {level}`. Jumlah kolom = jumlah signer
  (4 saat final), tidak lagi menggandakan pelapor.

### Nama tanda tangan Panen Nenas (kotak merge)

Template `12-panen-nenas.xlsx` sheet `PNN NNS` menempatkan **kotak nama di atas label**:
kotak merge baris 34–36 (blok 1) dan 78–80 (blok 2), kolom B/D, E/H, I/K, L/N. Label "Nama"
ada di baris 37/81, "Jabatan" 38/82 (sudah tercetak: Quality Control / Asst. Afd / Asst. QC /
Snr Manager), "Tgl" 39/83.

Pemindai generic lama menulis nama ke samping label baris 37 (sel D/G/K/N) — bukan ke kotak.
Perbaikan: cabang PNN mengisi nama **eksplisit** ke sel pojok kiri-atas kotak merge:

- Nama: baris 0-based `[33, 77]`, kolom `[1, 4, 8, 11]` (B34/E34/I34/L34 dan B78/E78/I78/L78).
- Tanggal: baris `[38, 82]`, kolom `[2, 5, 9, 12]` (C39/F39/J39/M39), format `: {tanggal}`.
- `fillWorkbook` **melewati** `fillTemplateSignatures` untuk sheet `PNN NNS` agar tidak ada
  sel stray di baris label; pengisian nama+tanggal sepenuhnya eksplisit.

## Verifikasi

### Unit test (node --test)

`tests/qcExportWatermark.test.mjs` (baru, 3 test) — fetch di-stub membaca template .xls lokal:
- Legacy .xls mendapat header `DRAFT` saat belum final; edit data tetap diterapkan.
- Tanpa watermark tidak menambah header status.
- Watermark `IN REVIEW` **mengganti** header bawaan (tepat satu header watermark, tidak menumpuk).

Suite QC frontend keseluruhan: **32/32 lulus** (`node --test tests/qc*.test.mjs`).

Dua test QC lama yang merah sebelum Fase 4 (tidak terkait export) diperbaiki agar suite hijau:
- `qcFrontendPatch.test.mjs` — fixture timeline diselaraskan dengan kontrak flow sebenarnya
  (level 1 `isCreatorStep`, label `DILAPORKAN OLEH`); komponen tidak diubah.
- `qcDashboardModel.ts` — menambah warna semantik `REJECTED: '#dc2626'` (merah) yang sebelumnya
  jatuh ke warna default; sesuai maksud test "rejected retain red".

### Verifikasi browser (harness fixture + capture server, tanpa screenshot)

Export dipanggil langsung dengan fixture menyerupai DTO backend (approvals memuat baris
pembuat level 1). Blob ditangkap via patch `URL.createObjectURL`, dikirim ke server node
port 5199, di-parse node-side (`xlsx` + `fflate` + `zlib`). Harness/scratch dihapus setelah selesai.

| Skenario | Hasil |
| --- | --- |
| PNN `APPROVED` (.xlsx) | Nama kotak B34/E34/I34/L34 = Inspektor QC / Asst Afd Satu / Asst QC Dua / Snr Manager Tiga (blok 2 sama); tanggal C/F/J/M39 = 18 November 2026; **tanpa** sel stray D/G/K/N37; **tanpa** watermark; logo ada |
| PNN `APPROVED` matematika | I11=128, J11=6.25; footer D31=349, E31=14, F31=328, H31=7, I31=349, J32=6.02 (21/349) — cocok view PG |
| PNN `DRAFT` (.xlsx) | Hanya B34=Inspektor QC, kotak lain kosong; C39 terisi, F/J/M39 placeholder template dipertahankan; watermark shape ada (rot −45°, alpha, teks `DRAFT`), logo tetap ada |
| PNN `DRAFT` (.pdf) | Teks `DRAFT` ditemukan pada stream PDF (inflate) |
| Kelapa `APPROVED` (.xls legacy) | Nama D46/G46/K46/P46 = 4 signer benar tanpa duplikasi; tanggal baris 48 benar; sampel C12=J-1/D12=10; **tanpa** header watermark |
| Pupuk `SUBMITTED` (.xlsx) | Path generic: nama pelapor F29/F63=Inspektor QC (approver lain PENDING → kosong); data pupuk C13=Urea/D13=50/E13=2; watermark `SUBMITTED` di semua sheet + logo |
| Pupuk `SUBMITTED` (.pdf) | Teks `SUBMITTED` ditemukan pada stream PDF |

### Build

`npm run build` hijau (TypeScript + Vite). Tidak ada perubahan backend.

## Item §6 yang masih terbuka

- Pemetaan jabatan/group → level approval & scope afdeling: export memakai urutan level
  approval (1=pembuat … 4=Disetujui) yang cocok dengan jabatan tercetak di template PNN.
  Untuk form lain, label jabatan pada kotak tanda tangan mengikuti template masing-masing;
  konfirmasi bisnis atas korespondensi group→jabatan tetap diperlukan (lihat Fase 1/2).
- Standar/target % penerimaan per komoditas belum ditetapkan (kriteria diisi manual).

## Catatan untuk Fase 5

- Watermark & signer sudah terpadu (`watermarkText`, `buildSigners`); form kebersihan bulanan
  per periode nanti memakai path export yang sama (generic signatures + watermark status).
- Template kebersihan (`Sheet2`) memakai path generic; bila area tanda tangannya berupa kotak
  merge seperti PNN, perlakukan eksplisit sebagaimana PNN NNS.