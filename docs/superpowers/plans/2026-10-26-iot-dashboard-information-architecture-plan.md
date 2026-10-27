# Professional IoT dashboard information architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn `Dashboard IoT` into a professional operational overview and keep registry, monitoring, history, and weather on their appropriate pages.

**Architecture:** Add a focused `IoTDashboardPage` that fetches only the existing device and latest-reading data. Keep `IoTVendorDevicesPage` as the registry and `IoTMonitoringPage` as the vendor/local detail workbench, then wire the dashboard route and menu resolver to the new page.

**Tech Stack:** React, TypeScript, CSS Modules, React Router, existing IoT axios API client, existing permission hook.

**Spec:** `docs/superpowers/specs/2026-10-26-iot-dashboard-information-architecture-design.md`

## Global Constraints

- No database, backend, or IoT API contract changes.
- The dashboard must not call history, parameter, or weather endpoints.
- Preserve the existing `DASHBOARD_IOT` permission gate.
- Preserve the existing vendor/local monitoring and registry routes.
- Do not add npm dependencies or replace the existing navigation model.
- Keep unrelated working-tree changes untouched.

## Review Focus

- API loading/error/empty states: the dashboard must not show misleading zeroes while the device request is pending.
- Mixed source data: attention links must route vendor and internal loggers to the appropriate monitoring page.
- Freshness classification: counts and attention rows must use the existing interval-aware `getFreshness` behavior.
- Permission denial: the new dashboard must use `NoAccess` and `DASHBOARD_IOT` exactly like the current page.
- Route resolver behavior: both direct routes and menu-code navigation must open the new dashboard without affecting weather or source-specific pages.

### Task 1: Build the focused operational dashboard

**Files:**
- Create: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTDashboardPage.tsx`
- Create: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTDashboardPage.module.css`
- Reuse: `E:/git-pkb/MyHelpDesk/src/features/iot/api/iotApi.ts`
- Reuse: `E:/git-pkb/MyHelpDesk/src/features/iot/types/iot.ts`

**Interfaces:**
- Consumes: `useMenuPermission('DASHBOARD_IOT')`, `getIoTDevices()`, and `getIoTLatest(loggerId)`.
- Produces: a dashboard page containing health KPIs, freshness distribution, attention queue, and quick-action links.

- [x] **Step 1: Define the dashboard view model and freshness helpers** in the new page, keeping the existing interval-aware labels and treating API failures as `unknown`.
- [x] **Step 2: Implement the loading, error, empty, healthy, and attention states** without rendering registry cards, history charts, or weather components.
- [x] **Step 3: Implement the responsive CSS Module** for the header, KPI grid, distribution panel, attention queue, and quick-action cards.
- [x] **Step 4: Run `npm run build` and confirm the new page compiles.**

### Task 2: Remove detail content from the existing monitoring workbench

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.module.css`

**Interfaces:**
- Consumes: existing source-specific route props and IoT monitoring APIs.
- Produces: vendor/local monitoring pages that retain device selection, data quality, parameter groups, and history charts without dashboard weather/overview coupling.

- [x] **Step 1: Remove the dashboard-only weather panel and coordinate projection** from `IoTMonitoringPage` now that the dashboard has its own page.
- [x] **Step 2: Remove only CSS selectors made unreachable by the extraction**; preserve monitoring and chart styles.
- [x] **Step 3: Run `npm run build` and inspect the vendor/local route components for TypeScript errors.**

### Task 3: Wire routes and navigation to the separated pages

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/routes/AppRoutes.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/routes/MenuRouteResolver.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/components/layout/AppBreadcrumb.tsx` only if the new dashboard label needs adjustment.

**Interfaces:**
- Consumes: `IoTDashboardPage` from Task 1 and existing page mappings.
- Produces: direct and menu-resolved `DASHBOARD_IOT` navigation to the focused dashboard; existing weather, registry, vendor, and local monitoring routes remain unchanged.

- [x] **Step 1: Point the direct `iot-management/dashboard-iot` route to `IoTDashboardPage`.**
- [x] **Step 2: Point `DASHBOARD_IOT` in `MenuRouteResolver` to `IoTDashboardPage`.**
- [x] **Step 3: Add/adjust dashboard quick-action destinations using existing stable routes only.**
- [x] **Step 4: Run `npm run build` and verify route imports resolve.**

### Task 4: Verify the information architecture in the running app

**Files:**
- Test: `E:/git-pkb/MyHelpDesk/package.json`
- Verify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTDashboardPage.tsx`
- Verify: `E:/git-pkb/MyHelpDesk/src/routes/AppRoutes.tsx`

- [x] **Step 1: Run `npm run build`.**
- [x] **Step 2: Run the workspace problem checker on all changed TypeScript/TSX files.**
- [x] **Step 3: Load `/iot-management/dashboard-iot` in the local browser and confirm the summary/quick actions are visible without weather, registry, or charts.**
- [x] **Step 4: Load `/iot-management/prakiraan-cuaca` and one source-specific monitoring route to confirm detail content remains on the appropriate pages.**
- [x] **Step 5: Run `git diff --check`, inspect changed-file scope, and commit with `feat: separate IoT dashboard from detail views`.**
