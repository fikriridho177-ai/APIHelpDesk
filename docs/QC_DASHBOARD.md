# Dashboard QC

Dashboard existing `/qc/dashboard` dilengkapi hero, filter tanggal, 8 KPI, 6 diagram, ringkasan/alert, akses cepat sesuai hak akses, dan 10 inspeksi terbaru. Style dan Recharts mengikuti dashboard divisi lainnya.

KPI: total inspeksi, WAITING_APPROVAL, APPROVED, REVISION_REQUIRED, total temuan, temuan terbuka, temuan terlambat, dan temuan kritis terbuka. Menunggu approval adalah seluruh inspeksi terlihat berstatus WAITING_APPROVAL, bukan jumlah antrean pribadi approver.

Diagram: pie status inspeksi; bar per form dan wilayah; line tren tanggal inspeksi per bulan; pie severity temuan terbuka; bar status tindak lanjut temuan. Severity kritis/rejected berwarna merah. Tidak ada data ditampilkan sebagai keadaan kosong.

Endpoint baru read-only: `GET /api/qc/dashboard?dateFrom=YYYY-MM-DD&dateTo=YYYY-MM-DD`.

View monitoring read-only tersedia untuk konsumen analisis: `VwQcInspectionMonitoring` (inspeksi + form + hitungan temuan terbuka/terlambat) dan `VwQcFindingMonitoring` (temuan + inspeksi + PIC + flag overdue). Endpoint dashboard memakai query EF existing; view tidak mengubah kontrak API.

Sumber: `TrnQcInspection` join `MstQcForm`, dan `TrnQcFinding` yang InspectionId-nya termasuk inspeksi terpilih. Periode default 180 hari dan maksimal 366 hari, berdasarkan InspectionDate. Temuan mengikuti periode inspeksi induk, bukan CreatedAt temuan. Status dan keterlambatan menggunakan kondisi saat ini/WIB. Definisi terbuka/terlambat tetap mengikuti daftar existing: Status != CLOSED dan TargetDate < hari ini. VERIFIED masih terbuka sampai CLOSED; target null atau hari ini tidak terlambat.

Hak akses: `QC_DASHBOARD.View` dan `.Read`, lalu cakupan owner/handler memakai QcAccess existing. Hak dashboard tidak otomatis membuat pengguna menjadi handler. Tidak ada batas 500 baris untuk angka agregat; DataJson tidak dimuat. Query finding memakai EXISTS terhadap query inspeksi yang sudah difilter. Tidak ada tabel/dashboard menu baru karena QC_DASHBOARD sudah existing.

Tidak mengubah QcStatusEngine, approval, finding lifecycle, form atau export. Quick action membaca route UtlMenu yang terlihat dan memerlukan permission aksi tujuan. UI tidak memanggil endpoint daftar approval hanya untuk menghitung grafik.

File: `Controllers/QcDashboardController.cs`, `Dashboards/QcDashboardCalculator.cs`, `tests/AuthUtility.Tests/QcDashboardTests.cs`; frontend `features/qc/pages/QcDashboardPage.tsx`, `api/qcApi.ts`, `qcDashboardModel.ts`, `types/qcDashboard.ts`, dan `tests/qcDashboard.test.mjs`.

Tes mencakup scope owner/handler, permission menu, periode kosong/invalid, null/today/closed target, severity kritis, hitungan lebih dari 500, serta regression approval existing. Tes memakai EF InMemory; koneksi DB operasional belum diuji dari sandbox. Restart API versi terbaru dan refresh frontend untuk menggunakan endpoint baru.
