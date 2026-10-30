# IoT Monitoring Compact Observability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menyederhanakan halaman monitoring IoT dengan menghapus chart utama, memakai seluruh histori bertimestamp pada kartu parameter, dan menggabungkan filter Vendor/Internal.

**Architecture:** Halaman monitoring tetap memakai endpoint read-only yang ada, merender seluruh histori mentah yang dikembalikan endpoint sebagai grafik per kartu, dan mempertahankan urutan timestamp di frontend. Route lama tetap dipertahankan sebagai alias, sedangkan SQL migration mengarahkan menu aktif ke halaman gabungan.

**Tech Stack:** React 18, TypeScript, CSS Modules, Recharts, Vite.

**Spec:** `docs/superpowers/specs/2026-10-23-iot-monitoring-compact-observability.md`

## Global Constraints

- Perubahan hanya berlaku untuk halaman monitoring IoT.
- Endpoint, permission, route, dan sumber data tidak berubah.
- Jangan menebak threshold sensor yang tidak diberikan API.
- Pertahankan responsivitas pada desktop dan mobile.
- Grafik histori memakai semua pembacaan numerik yang diterima endpoint; `take` tetap mengikuti batas endpoint saat ini.

## Review Focus

- Histori kosong: area kartu harus menampilkan pesan yang jelas, bukan area kosong yang tampak rusak.
- Histori error/loading: state harus tetap terbaca pada panel ringkasan.
- Nilai konstan atau hanya satu titik per hari: kartu tetap memberikan penanda visual dan konteks jumlah hari.
- Lebar mobile: header, chart, dan kartu tidak boleh saling menekan atau terpotong.
- Sumber Vendor/Internal: filter gabungan tidak boleh mencampur label sumber pada kartu perangkat.

### Task 1: Timestamp history cards and simplified monitoring markup

**Files:**
- Modify: `src/features/iot/pages/IoTMonitoringPage.tsx`
- Test/verification: `npm run build`

**Interfaces:**
- Consumes: `IoTHistoryResponse`, existing numeric parsing helpers, and the combined source filter.
- Produces: `getChartPoints(readings, parameterCode)` and card-only monitoring markup.

- [ ] **Step 1: Add a failing static contract check** for daily aggregation and absence of the duplicate main chart.
- [ ] **Step 2: Preserve every numeric reading as a chart point with its original timestamp** and label cards with `pembacaan`.
- [ ] **Step 3: Remove the main analytics panel, parameter rail, and selection-only state; retain loading/error/empty states near the daily card board.
- [ ] **Step 4: Make sensor cards non-interactive summaries and add a visible dot for a one-point/constant daily series.
- [ ] **Step 5: Run `npm run build`** and confirm the TypeScript/CSS module references compile.

### Task 2: Clear history card styling

**Files:**
- Modify: `src/features/iot/pages/IoTMonitoringPage.module.css`
- Test/verification: `npm run build`

**Interfaces:**
- Consumes: the existing page, category, sensor-card, and daily-board class names from Task 1.
- Produces: a compact timestamp-history card board with no unused chart panel and no page-level horizontal overflow.

- [ ] **Step 1: Remove obsolete analytics styles and add a history-board heading** that explains the raw timestamp basis.
- [ ] **Step 2: Tighten category heading, sensor grid, and card dimensions** so more daily parameters are visible without excessive scrolling.
- [ ] **Step 3: Add responsive treatment** for daily board headings and sensor cards below 1100px and 640px.
- [ ] **Step 4: Run `npm run build`** and confirm the production bundle completes successfully.

### Task 3: Unified Vendor/Internal registry and menu aliases

**Files:**
- Modify: `src/features/iot/pages/IoTVendorDevicesPage.tsx`
- Modify: `src/routes/AppRoutes.tsx`
- Modify: `src/routes/MenuRouteResolver.tsx`
- Modify: `E:/git-pkb/APIHelpDesk/sql/2026-10-23_iot_menu_restructure.sql`
- Verify: `src/components/layout/AppBreadcrumb.tsx`

**Interfaces:**
- Consumes: `getIoTDevices(sourceType?)`, existing menu codes, and existing route aliases.
- Produces: one canonical combined device/monitoring menu with source filtering while retaining old direct links.

- [ ] **Step 1: Add a failing route/menu contract check** for the canonical combined routes and source filter.
- [ ] **Step 2: Make the registry page accept all sources when no `sourceType` prop is supplied**, with `Semua/Vendor/Internal` filtering.
- [ ] **Step 3: Point the canonical frontend route and menu resolver entry to the combined pages; keep legacy Vendor/Internal paths as filtered aliases.
- [ ] **Step 4: Update the SQL migration** so the active menu exposes one combined monitoring/registry group under IoT Management and deactivates duplicate Vendor/Development links without changing permission rows for unrelated menus.
- [ ] **Step 5: Run `npm run build` and inspect diagnostics** for all modified frontend files.

### Task 4: Final verification

**Files:**
- Verify: `src/features/iot/pages/IoTMonitoringPage.tsx`
- Verify: `src/features/iot/pages/IoTMonitoringPage.module.css`

- [ ] **Step 1: Run the full frontend build** with `npm run build`.
- [ ] **Step 2: Inspect diagnostics** for the modified files and confirm there are no TypeScript, CSS module, or lint-like editor errors.
- [ ] **Step 3: Review the diff** to ensure the prior breadcrumb/menu changes remain untouched and no API files were modified by this task.
