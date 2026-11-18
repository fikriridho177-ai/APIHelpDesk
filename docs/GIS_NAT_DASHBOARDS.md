# Dashboard GIS dan NAT

Implementasi dashboard menggunakan tabel existing; tidak menambah tabel dashboard atau mengubah workflow pengajuan/operasional. Dashboard RNA/QC dipertahankan. Frontend memakai hero/cards/CSS Layanan-Sensus dan Recharts existing.

## Analisis sumber NAT

Pemeriksaan controller, entity/DbSet, reader dan pemakaian NAT di frontend menemukan `UtlMenu` divisi `LYN_NAT`, master `MstLynJenisPermintaan`, dan riwayat `TrnLynRequest`. Tidak ditemukan reader/entity transaksi alat, jam operasi, produktivitas atau biaya NAT. Karena itu dashboard NAT secara eksplisit menampilkan **master dan riwayat tiket**, bukan statistik penggunaan alat. Pembuatan pengajuan baru NAT tetap tidak ditambahkan/diaktifkan.

## KPI dan chart

| Divisi | KPI | Chart / ringkasan |
| --- | --- | --- |
| GIS | Total pekerjaan, terbuka, selesai, lewat deadline, jenis layanan aktif, menunggu approval, dalam proses/menunggu info, rata-rata hari penyelesaian | Pie status, bar per jenis pekerjaan, line jumlah pencatatan per bulan, 10 pekerjaan terbaru, alert keterlambatan |
| GIS Peta, opsional | Layer siap tayang dan total feature_count vektor dari katalog | Bar jumlah layer vektor/raster; tidak menghitung ukuran raster sebagai jumlah fitur |
| NAT | Riwayat tiket NAT, riwayat terbuka/selesai/lewat deadline, jenis layanan aktif | Pie status riwayat, bar riwayat per jenis, line pencatatan riwayat bulanan, 10 riwayat terbaru, daftar master aktif, penjelasan sumber operasional yang belum tersedia |

Tanpa riwayat, diagram menampilkan keadaan kosong. Rata-rata penyelesaian tanpa data selesai adalah null/“Belum ada”, bukan nol. Error sumber data tidak ditampilkan sebagai angka nol. Quick action GIS: form pengajuan, peta, monitoring dan master sesuai permission/menu DB. NAT: monitoring riwayat, master, serta tautan riwayat/jenis pada halaman; tidak ada tombol membuat pengajuan NAT.

## Endpoint dan query

- `GET /api/divisi/gis/dashboard?dateFrom=YYYY-MM-DD&dateTo=YYYY-MM-DD`
- `GET /api/divisi/nat/dashboard?dateFrom=YYYY-MM-DD&dateTo=YYYY-MM-DD`
- GIS peta memakai endpoint existing `GET /api/gis/layers`, hanya ketika `GIS_PETA.Read` diizinkan.

Endpoint divisi berada pada partial LayananController agar bisa memakai query `QueryVisible` dan klasifikasi divisi existing. Query SQL yang dihasilkan EF membaca `TrnLynRequest` dengan join pemohon `UtlUser` dan jenis `MstLynJenisPermintaan`; dibatasi MenuId divisi dan rentang UTC dari tanggal WIB. Owner/handler serta filter departemen tetap melalui `LayananAccess.CanSee` existing. Tiket divisi lain tidak ikut dihitung; tidak memakai batas daftar 500 tiket.

Master jenis: `MstLynJenisPermintaan WHERE Active AND DivisionMenuId = Id divisi`, diurutkan SortOrder/Name. Tabel `UtlMenu` existing menentukan relasi divisi/menu. Perhitungan status, deadline, dan hari penyelesaian memakai `LynReportCalculator` existing. Agregasi dashboard memakai query EF existing; view read-only `VwLynRequestMonitoring` disediakan untuk konsumen monitoring/analisis.

Katalog GIS memakai WebGisReader existing: `app.layers WHERE state='READY' AND is_visible=true`; vektor atau raster yang managedBy/stage-nya sesuai query existing. Read-only; tidak menambah akses PostGIS dan tidak mengubah query tile. Kegagalan katalog peta tidak menggagalkan ringkasan pekerjaan.

Periode default 180 hari, maksimal 366 hari inklusif. Filter adalah tanggal **pencatatan** dalam WIB, bukan snapshot historis status. Status dan keterlambatan memakai kondisi saat ini; chart bulanan adalah jumlah record yang dibuat per bulan. Master aktif dan layer peta tidak difilter tanggal.

## Menu dan akses

Startup API menambahkan `GIS_DASHBOARD` di bawah `LYN_GIS` dan `NAT_DASHBOARD` di bawah `LYN_NAT` jika belum ada. Route default `/gis/dashboard` dan `/nat/dashboard`. Tidak membuat root divisi baru atau group baru. Hak awal hanya View/Read disalin dari root divisi; tidak memberikan Create/Update/Delete/Approve. Super admin mengikuti PermissionService existing. Baris baru muncul di Management Menu dan Hak Akses melalui UtlMenu.

Dashboard mensyaratkan View+Read pada kode menu dashboard. Hak dashboard tidak memperluas cakupan tiket: tetap milik sendiri atau petugas dengan hak existing dan departemen yang diizinkan. Quick action dan katalog mengikuti route dari menu DB yang terlihat. Renderer custom route memakai MenuRouteResolver berdasarkan kode dashboard. Sinkronisasi berulang mempertahankan nama/route/permission/Active=false yang diubah admin.

Restart API versi terbaru pada lingkungan yang memiliki koneksi DB untuk menjalankan sinkronisasi menu, lalu refresh/login ulang frontend. Tidak mengklaim bahwa DB operasional sudah disinkronkan dari sandbox. Tes DB menggunakan EF InMemory; tidak memverifikasi koneksi DB operasional/PostGIS nyata.

## File perubahan fitur ini

Backend:
- src/AuthUtility.Api/Controllers/DivisionDashboardEndpoints.cs (baru)
- src/AuthUtility.Api/Dashboards/DivisionDashboardCalculator.cs (baru)
- src/AuthUtility.Api/Dashboards/DivisionDashboardMenuSync.cs (baru)
- src/AuthUtility.Api/Controllers/LayananController.cs (partial dan filter opsional pada query baca existing)
- src/AuthUtility.Api/Program.cs (pemanggilan sync menu)
- tests/AuthUtility.Tests/DivisionDashboardTests.cs (baru)

Frontend:
- src/features/divisi/dashboardTypes.ts (baru)
- src/features/divisi/dashboardModel.ts (baru)
- src/features/divisi/pages/DivisionDashboardPage.tsx (baru)
- src/features/divisi/divisiApi.ts
- src/features/layanan/api/layananApi.ts (katalog divisi tetap tersedia tanpa transaksi)
- src/features/portal/widgets/KatalogWidget.tsx (tujuan dashboard dari menu DB)
- src/routes/AppRoutes.tsx
- src/routes/MenuRouteResolver.tsx
- tests/divisionDashboard.test.mjs (baru)
- docs/superpowers/plans/2026-11-16-gis-nat-dashboards.md

Perubahan Sensus/Layanan lain yang sudah ada sebelum fitur ini tetap dipertahankan; tidak ikut direfactor.
