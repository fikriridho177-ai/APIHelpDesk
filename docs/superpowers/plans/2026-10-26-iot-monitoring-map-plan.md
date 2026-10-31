# IoT Monitoring Map and Weather Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a responsive IoT monitoring map with real device coordinates, selected-device weather context, and the requested backward-compatible IoT navigation structure.

**Architecture:** Extend the existing monitoring endpoint additively with nullable coordinates, then build a focused MapLibre IoT page that consumes one fleet response and lazily loads selected-device parameters/weather. Preserve existing pages and direct routes; use database menu migration plus frontend aliases for navigation compatibility.

**Tech Stack:** ASP.NET Core 8, SQL Server telemetry query, xUnit, React 18, TypeScript, Vite, MapLibre GL JS, Axios, CSS Modules, existing weather/Open-Meteo/BMKG services.

**Spec:** `docs/superpowers/specs/2026-10-26-iot-monitoring-map-design.md`

## Global Constraints

- Use the existing MapLibre dependency and existing WebGIS map style/configuration; do not add a mapping dependency or expose provider credentials.
- Keep `GET /api/iot/monitoring`, its `sourceType` query, existing status values, and existing response fields backward-compatible; only add nullable coordinates.
- Use all source types on the map; do not add a source filter in this phase.
- Do not poll automatically in the first phase; load on page entry and provide only a reload action consistent with existing UI patterns.
- Do not render dummy devices/coordinates. Validate finite latitude `[-90, 90]` and longitude `[-180, 180]` before marker or weather use.
- Keep `/iot-management/prakiraan-cuaca` directly routable, but remove/deactivate its primary navigation item.
- Keep `Notifikasi & Peringatan` inactive/roadmap because no notification backend or workflow exists.
- Keep actual sensor observations and external forecast data in visibly separate sections with their own timestamps/source labels.
- Preserve unrelated dirty work in both repositories and do not rewrite the existing history-range work.

## Review Focus

- Missing or invalid coordinates must never crash the map, create a marker, or trigger a fabricated weather query. Test in Task 3.
- A slow response for a previously selected marker must not replace the current device detail or weather panel. Test in Task 4.
- `UNKNOWN`, `STALE`, `NO_RECENT_DATA`, and `INACTIVE` must remain visible in summary/detail and must not be counted as online. Test in Tasks 1 and 3.
- Existing weather and monitoring/history deep links must remain routable after menu migration. Test in Task 2 and the final browser pass.
- Empty, loading, provider-error, and no-coordinate states must be explicit and distinguish sensor failure from weather failure. Test in Tasks 3–4.

## File Map

- Backend contract: `src/AuthUtility.Api/Controllers/IotMonitoringController.cs`
- Backend tests: `tests/AuthUtility.Tests/IotMonitoringControllerTests.cs`
- Menu migration: `sql/2026-10-26-iot-monitoring-map-menu.sql`
- IoT types/API: `src/features/iot/types/iot.ts`, `src/features/iot/api/iotApi.ts`
- Map component: `src/features/iot/components/IoTMonitoringMap.tsx`, `src/features/iot/components/IoTMonitoringMap.module.css`
- Map page: `src/features/iot/pages/IoTMapMonitoringPage.tsx`, `src/features/iot/pages/IoTMapMonitoringPage.module.css`
- Weather integration: `src/features/weather/components/WeatherForecastPanel.tsx`, `src/features/weather/components/WeatherForecastPanel.module.css`
- Navigation/routing: `src/components/layout/TopNavbar.tsx`, `src/routes/AppRoutes.tsx`, `src/routes/MenuRouteResolver.tsx`, `src/components/layout/AppBreadcrumb.tsx`
- Existing history reuse: `src/features/iot/pages/IoTMonitoringPage.tsx`

### Task 1: Extend monitoring response with nullable coordinates

**Files:**
- Modify: `src/AuthUtility.Api/Controllers/IotMonitoringController.cs`
- Test: `tests/AuthUtility.Tests/IotMonitoringControllerTests.cs`

**Interfaces:**
- Consumes: existing `GET /api/iot/monitoring?sourceType=...` SQL projection and status calculation.
- Produces: `MonitoringResponse` with existing fields unchanged and each device containing nullable `Latitude` and `Longitude`.

- [ ] **Step 1: Write failing contract tests**

  Add focused tests for the monitoring projection/DTO contract: valid coordinates round-trip as nullable numeric values; null coordinates remain null; `UNKNOWN` is not treated as online. Follow the existing xUnit style and isolate the projection/status rule rather than requiring a live SQL Server.

- [ ] **Step 2: Run the focused tests and verify failure**

  Run: `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore --filter FullyQualifiedName~IotMonitoringControllerTests`

  Expected: FAIL because the coordinate fields/testable projection do not exist yet.

- [ ] **Step 3: Implement the additive backend contract**

  Select `Latitude` and `Longitude` from `MstLogger`, add nullable properties to `MonitoringDevice`, map database nulls safely, and preserve all existing response/status behavior. Keep the parameter identity as `LoggerId`/`loggerId` for the existing device-parameter endpoint.

- [ ] **Step 4: Run focused and full backend tests**

  Run the focused filter, then `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore`.

  Expected: all focused and existing tests pass.

### Task 2: Establish database menu mapping and route compatibility

**Files:**
- Create: `sql/2026-10-26-iot-monitoring-map-menu.sql`
- Modify: `src/routes/AppRoutes.tsx`
- Modify: `src/routes/MenuRouteResolver.tsx`
- Modify: `src/components/layout/TopNavbar.tsx`
- Modify: `src/components/layout/AppBreadcrumb.tsx`

**Interfaces:**
- Consumes: existing menu codes, existing IoT pages, and Task 1’s monitoring API only indirectly through the new page.
- Produces: `/iot-management/peta-monitoring` and `/iot-management/riwayat-data-sensor` routes, menu labels/order, and aliases for legacy codes/routes.

- [ ] **Step 1: Add an idempotent SQL migration**

  Set root label to `Manajemen IoT`; map active entries to Dashboard, Peta Monitoring, Manajemen Perangkat, Monitoring Sensor, and Riwayat Data Sensor. Deactivate/hide standalone weather and notification roadmap entries without deleting records or permissions. Keep legacy menu rows/route aliases available for direct access.

- [ ] **Step 2: Add frontend route entries and resolver aliases**

  Route the new map page at `/iot-management/peta-monitoring`; route `/iot-management/riwayat-data-sensor` to the existing history-capable monitoring page; preserve `/iot-management/prakiraan-cuaca` and old vendor/internal aliases. Update resolver and breadcrumb labels without changing unrelated routes.

- [ ] **Step 3: Update navigation presentation**

  Apply the requested labels and route overrides while tolerating pre-migration menu payloads. Render existing dashboard icon mappings when a supported menu/icon mapping is available; do not make inactive roadmap entries look clickable.

- [ ] **Step 4: Validate route/build behavior**

  Run `npm run build` from `E:\git-pkb\MyHelpDesk` and confirm all route imports compile. Browser verification is completed in Task 5.

### Task 3: Add the MapLibre device map and fleet summary

**Files:**
- Modify: `src/features/iot/types/iot.ts`
- Modify: `src/features/iot/api/iotApi.ts`
- Create: `src/features/iot/components/IoTMonitoringMap.tsx`
- Create: `src/features/iot/components/IoTMonitoringMap.module.css`
- Create: `src/features/iot/pages/IoTMapMonitoringPage.tsx`
- Create: `src/features/iot/pages/IoTMapMonitoringPage.module.css`

**Interfaces:**
- Consumes: Task 1’s `GET /api/iot/monitoring` response and existing device/parameter API conventions.
- Produces: `IoTMapMonitoringPage` and `IoTMonitoringMap` with selected `loggerId`, valid-marker bounds, device list fallback, summary counts, and explicit loading/error/empty states.

- [ ] **Step 1: Define monitoring types and API wrapper**

  Add literal status/monitoring response types with nullable `latitude`/`longitude`, latest timestamps, and `data` values. Add `getIoTMonitoring()` to the existing IoT API module without changing existing method signatures.

- [ ] **Step 2: Build the failing map behavior checks**

  Add testable pure helpers or component-level checks for coordinate validation, status summary (`online`, complement offline, issue subset), and selecting a device with no coordinates. If no frontend test runner exists, keep these helpers exported and validate them through TypeScript build plus the browser matrix in Task 5.

- [ ] **Step 3: Implement MapLibre lifecycle and markers**

  Create the map only after its container exists, reuse the existing MapLibre style configuration, create markers only for valid coordinates, color them by status, fit bounds only when at least one valid marker exists, and clean up markers/listeners/map on unmount. Marker click must emit `loggerId`; list selection must work for devices without coordinates.

- [ ] **Step 4: Implement page layout and states**

  Fetch all monitoring data once on entry, render summary cards and map/list areas, show sensor-side loading/error/empty states, and render selected-device identity/latest observations. Keep map dominant and switch to a stacked layout on narrow screens.

- [ ] **Step 5: Run frontend build**

  Run: `npm run build`

  Expected: TypeScript and Vite build pass without adding dependencies.

### Task 4: Integrate selected-device parameters and weather safely

**Files:**
- Modify: `src/features/iot/pages/IoTMapMonitoringPage.tsx`
- Modify: `src/features/weather/components/WeatherForecastPanel.tsx`
- Modify: `src/features/weather/components/WeatherForecastPanel.module.css`
- Modify: `src/features/iot/types/iot.ts` if selected-detail types need a small additive extension

**Interfaces:**
- Consumes: selected `loggerId` and nullable coordinates from Task 3; existing `getIoTParameters`, `WeatherForecastPanel`, and `/api/weather/forecast`.
- Produces: selected detail with actual sensor observations separated from forecast data, no-coordinate weather state, and stale-response protection.

- [ ] **Step 1: Add selected-device parameter loading**

  Fetch parameters only for the selected logger using `loggerId`; keep the fleet request as the source of latest values and avoid N+1 requests for unselected devices. Clear or retain detail intentionally during selection transitions, but never show the previous device as the current selection.

- [ ] **Step 2: Add a map-safe weather mode**

  Extend the existing weather panel with a prop/contract that disables its Pekanbaru fallback and manual location override when called from the map. Pass only the selected device coordinates; if coordinates are missing/invalid, render an unavailable state without calling the forecast endpoint. Preserve standalone weather page behavior.

- [ ] **Step 3: Guard async selection changes**

  Add a sequence/request identity or abort mechanism for parameter and weather loads. A response is applied only if it belongs to the current selected `loggerId` and component lifecycle. Weather failures remain inside the weather section and do not erase actual sensor detail.

- [ ] **Step 4: Style loading/error/forecast sections**

  Reuse existing weather visual language, label provider/update/validity/satuan, and keep actual readings visually distinct from forecast/alert content. Add responsive rules for the map detail panel without changing unrelated weather page layout.

- [ ] **Step 5: Run build and focused browser checks**

  Run `npm run build`; then verify marker selection, fast selection changes, missing coordinates, and provider-error rendering in the browser where the local services permit.

### Task 5: Reuse history route and complete verification

**Files:**
- Modify only if needed: `src/features/iot/pages/IoTMonitoringPage.tsx`
- Modify only if needed: `src/features/iot/pages/IoTMonitoringPage.module.css`

**Interfaces:**
- Consumes: Task 2’s history route and the existing period/range implementation.
- Produces: a discoverable `Riwayat Data Sensor` route without a duplicate history backend or divergent period behavior.

- [ ] **Step 1: Verify history alias behavior**

  Confirm day/week/month/year controls, date/range rules, period-aware chart labels, empty state, and request race guard remain available at both monitoring and history routes. Make only the smallest presentation adjustment if the new route needs a history heading.

- [ ] **Step 2: Run repository verification**

  Run backend tests, frontend `npm run build`, `git diff --check` in both repositories, and the editor/problem checks for changed files. Do not claim lint/test success for a command that is not configured or not run.

- [ ] **Step 3: Browser verification matrix**

  Check: new navigation labels/routes; direct weather route; map load; summary counts; valid and invalid coordinates; device list selection without coordinates; marker detail; sensor loading/error; weather loading/error/unavailable; fast selection changes; empty fleet; desktop and mobile layouts; existing monitoring/history routes.

- [ ] **Step 4: Document outcomes**

  Report changed files, API/service reuse, menu compatibility, verification commands/results, provider/environment limitations, and the notification roadmap explicitly.
