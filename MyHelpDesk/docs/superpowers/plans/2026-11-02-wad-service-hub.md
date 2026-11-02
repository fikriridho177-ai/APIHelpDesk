# WAD Service Hub Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Ubah halaman tujuan kartu WAD menjadi hub enam layanan Water Management sesuai alur `Pilih Layanan → WAD → layanan WAD`.

**Architecture:** Halaman `/ecs` tetap menjadi entry point yang sudah dipakai kartu WAD. Konten IoT shortcut lama diganti komponen hub layanan berbasis konfigurasi statis; layanan yang sudah memiliki halaman memakai route ECS yang ada, sedangkan layanan yang belum tersedia ditampilkan sebagai pilihan nonaktif yang informatif.

**Tech Stack:** React, TypeScript, React Router, CSS Modules, Vite.

**Spec:** `docs/superpowers/specs/2026-11-02-wad-overview-dashboard-design.md`

## Global Constraints

- Enam pilihan harus tampil dengan urutan: Pantauan TMAT, Pantauan TMAK, Kondisi Pintu Air, Kondisi Siphon / Gorong-gorong, Pengecekan Subsiden Gambut, Pengecekan Curah Hujan.
- Tidak membuat API, tabel, atau endpoint baru.
- Jangan mengubah integrasi GIS, navbar, API, database, atau route yang sudah ada.
- Jangan menghapus alias route IoT lama yang masih diperlukan untuk bookmark/permission.
- Layanan yang belum tersedia harus terlihat jelas sebagai belum tersedia, bukan error teknis.

## Review Focus

- Hub dibuka dari route `/ecs` tanpa mengubah route kartu WAD.
- Urutan dan label enam layanan tetap sama pada desktop maupun viewport sempit.
- Link hanya aktif untuk route yang benar-benar tersedia; layanan lain tidak mengarah ke halaman palsu.
- Permission gate halaman `/ecs` tetap berlaku.
- Build tetap bersih setelah komponen shortcut IoT lama tidak lagi dipakai.

### Task 1: Define WAD service catalog

**Files:**
- Create: `src/features/ecs/wadServices.ts`

**Interfaces:**
- Produces `WadServiceItem` and `WAD_SERVICE_ITEMS` for the hub component.
- Active routes: TMAT `/ecs/stasiun-monitoring`, TMAK `/ecs/tmak`, Curah Hujan `/ecs/curahhujan`.
- Unavailable services have no route and `available: false`.

- [ ] Add the typed six-item catalog with stable codes, labels, descriptions, icons, availability, and existing routes.
- [ ] Confirm the catalog order matches the supplied WAD flow diagram.

### Task 2: Build the WAD service hub UI

**Files:**
- Create: `src/features/ecs/components/WadServiceHub.tsx`
- Create: `src/features/ecs/components/WadServiceHub.module.css`

**Interfaces:**
- Consumes `WAD_SERVICE_ITEMS` and `WadServiceItem` from Task 1.
- Produces an accessible service flow with active links and disabled unavailable entries.

- [ ] Render the WAD root node and six service rows with icon, number, title, description, availability label, and arrow treatment.
- [ ] Render active entries as React Router links and unavailable entries as non-interactive rows with `aria-disabled`.
- [ ] Add responsive CSS: connected single-column flow on desktop and readable stacked rows on narrow screens.
- [ ] Keep visual language aligned with the existing green WAD landing page while reducing hero/card heaviness.

### Task 3: Make `/ecs` the WAD hub entry page

**Files:**
- Modify: `src/features/ecs/pages/OverviewPage.tsx`
- Modify: `src/features/ecs/pages/OverviewPage.module.css`
- Modify: `src/features/ecs/ecsConfig.tsx`
- Modify: `src/components/layout/AppBreadcrumb.tsx`
- Delete: `src/features/ecs/components/EnvironmentalMonitoringMenu.tsx`
- Delete: `src/features/ecs/components/EnvironmentalMonitoringMenu.module.css`

**Interfaces:**
- `/ecs` continues to be rendered by the existing `WAD_ECS_DASHBOARD` permission gate.
- Existing detail pages and route aliases remain unchanged.

- [ ] Replace the old IoT shortcut rendering with `WadServiceHub`.
- [ ] Remove the duplicate “Lihat Stasiun” hero action and tighten the hero copy to identify WAD / Water Management.
- [ ] Update the ECS dashboard label and breadcrumb to describe the WAD service hub.
- [ ] Verify no remaining imports reference the deleted IoT shortcut component.

### Task 4: Verify the hub

**Files:**
- No additional source files.

- [ ] Run `npm run build` from `MyHelpDesk` and confirm exit code 0.
- [ ] Run diagnostics for changed files and confirm no TypeScript errors.
- [ ] Run `git diff --check` and confirm no whitespace errors.
- [ ] Inspect `/ecs` locally or through the built bundle to confirm all six labels and the three active route targets are present.
