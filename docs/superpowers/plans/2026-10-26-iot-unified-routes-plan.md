# Unified IoT Vendor and Local Routes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menyatukan registry dan monitoring IoT Vendor/Internal pada halaman kanonis dengan filter sumber, sambil menjaga rute lama tetap dapat dibuka.

**Architecture:** Halaman registry yang sudah mendukung data gabungan akan menjadi halaman kanonis `/iot-management/daftar-perangkat`. Halaman monitoring akan menjadi halaman kanonis baru `/iot-management/monitoring-sensor`; keduanya dipanggil tanpa `sourceType` agar API mengembalikan seluruh sumber dan filter bekerja di sisi halaman. Rute lama serta menu backend lama diarahkan ke halaman kanonis menggunakan redirect frontend.

**Tech Stack:** React 18, TypeScript, React Router, Vite, CSS Modules.

**Spec:** `docs/superpowers/specs/2026-10-26-iot-unified-routes-design.md`

## Global Constraints

- Halaman kanonis registry: `/iot-management/daftar-perangkat`.
- Halaman kanonis monitoring: `/iot-management/monitoring-sensor`.
- Filter sumber harus menyediakan `Semua`, `Vendor`, dan `Internal`.
- Rute lama Vendor/Lokal harus redirect dengan penggantian history.
- Tidak mengubah master menu, permission database, atau endpoint backend.
- Dashboard hanya menampilkan shortcut fungsi gabungan dan prakiraan cuaca.

## Review Focus

- Rute langsung Vendor dan Lokal harus berakhir di halaman kanonis, bukan halaman dengan data yang terkunci ke satu sumber. Verifikasi melalui navigasi browser pada rute lama.
- Resolusi menu backend lama harus mengarah ke halaman kanonis. Verifikasi dengan membuka rute lokal yang tidak terdaftar eksplisit di `AppRoutes`.
- Halaman gabungan harus meminta seluruh sumber dari API; filter hanya menyaring hasil di UI. Verifikasi jumlah perangkat dan perubahan filter.
- Permission halaman kanonis harus tetap memakai permission IoT yang sudah tersedia. Verifikasi halaman tidak berubah menjadi `NoAccess` setelah redirect.
- Navbar tidak boleh menyisakan shortcut lokal/vendor yang duplikatif atau group kosong setelah item lokal disembunyikan. Verifikasi menu IoT di browser.

### Task 1: Canonical routes and legacy redirects

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/routes/AppRoutes.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/routes/MenuRouteResolver.tsx`
- Test: browser smoke test for canonical and legacy routes; no automated test runner is configured.

**Interfaces:**
- Consumes: existing `IoTMonitoringPage`, `IoTVendorDevicesPage`, and backend menu codes.
- Produces: `/iot-management/monitoring-sensor` as the unified monitoring route; legacy route redirects using `<Navigate replace />`.

- [ ] Add the explicit canonical monitoring route without `sourceType` so its existing source filter is visible and the default permission remains `DASHBOARD_IOT`.
- [ ] Replace the explicit legacy Vendor registry and Vendor monitoring page elements with redirects to the canonical registry and monitoring routes.
- [ ] Change `PAGE_BY_MENU_CODE` entries for `DAFTAR_PERANGKAT_INTERNAL`, `MONITORING_SENSOR_VENDOR`, and `MONITORING_SENSOR` to redirect to the matching canonical route; keep the Vendor registry entry mapped to the already-unified registry page.
- [ ] Run the frontend build and confirm TypeScript accepts all route elements.

### Task 2: Unified dashboard actions and attention links

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTDashboardPage.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTDashboardPage.module.css` only if the reduced action set needs responsive spacing adjustments.
- Test: browser smoke test for dashboard action count and attention links.

**Interfaces:**
- Consumes: canonical routes from Task 1.
- Produces: one registry shortcut, one monitoring shortcut, and one weather shortcut; all attention rows open unified monitoring.

- [ ] Replace the six Vendor/Local-specific quick actions with three actions: `Daftar perangkat`, `Monitoring Sensor IoT`, and `Prakiraan Cuaca 10 Hari`.
- [ ] Point every dashboard attention row to `/iot-management/monitoring-sensor` regardless of device source.
- [ ] Keep existing KPI, freshness distribution, and device-source labels unchanged.
- [ ] Run the build and verify the dashboard renders no Vendor/Lokal duplicate cards.

### Task 3: Navbar normalization and filter copy

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/components/layout/TopNavbar.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTVendorDevicesPage.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.tsx`
- Test: browser smoke test for navbar entries and filter options.

**Interfaces:**
- Consumes: backend navigation items with legacy Vendor/Internal menu codes.
- Produces: normalized visible navigation entries that point to canonical routes and clear source-filter labels.

- [ ] Filter legacy Local leaf menu codes from the navbar while preserving unrelated menu permissions.
- [ ] Normalize visible Vendor representative entries to `Daftar Perangkat` and `Monitoring Sensor IoT` with canonical routes; ensure hidden groups with no remaining children are not rendered.
- [ ] Change unified registry and monitoring filter copy to `Semua sumber`/`Semua`, `Vendor`, and `Internal` so the selected scope is explicit.
- [ ] Confirm no source-specific `sourceType` is introduced on canonical pages.

### Task 4: Breadcrumbs and final verification

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/components/layout/AppBreadcrumb.tsx`
- Test: `npm run build`, VS Code diagnostics, and browser smoke test across canonical/legacy routes.

**Interfaces:**
- Consumes: canonical route names from Tasks 1–3.
- Produces: consistent breadcrumb labels for unified IoT pages.

- [ ] Add the canonical monitoring breadcrumb and update legacy IoT breadcrumbs to use unified labels when a legacy URL is briefly visible during redirect.
- [ ] Run `npm run build` in `E:/git-pkb/MyHelpDesk`; expected result is a successful TypeScript/Vite build, with only any pre-existing bundle-size warning permitted.
- [ ] Run diagnostics on all modified TypeScript/TSX files; expected result is no new errors.
- [ ] Verify in the running app: dashboard actions, registry filter, monitoring filter, old Vendor route redirect, old Local route redirect, and weather route availability.
