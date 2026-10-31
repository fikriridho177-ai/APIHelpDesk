# IoT submenu and RSUP plantation focus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menambahkan submenu Monitoring IoT yang tertutup secara default di sidebar GIS yang sudah ada, memfokuskan peta sekali ke layer PT Riau Sakti United Plantations / Divisi Perkebunan, memberi label pada marker logger, serta menampilkan pembacaan TMAK/TMAT lebih dahulu pada detail logger.

**Architecture:** `MapWorkspace` tetap menjadi satu-satunya pemanggil `useIoTMapMonitoring` dan tetap mengirim state bertipe ke `GISViewer`. `GISViewer` memiliki state buka/tutup accordion serta membungkus callback seleksi logger untuk membuka submenu dan melakukan fit eksplisit dengan `getIoTMapBounds`. `MapCanvas` tetap membuat satu instance MapLibre; perubahan marker hanya menambahkan label DOM pada marker yang sudah ada. Fokus layer RSUP memakai metadata dan bounds dari katalog GIS yang sudah ada, sekali pada inisialisasi katalog.

**Tech Stack:** React 18, TypeScript, CSS Modules, MapLibre GL, Vite, Node test runner.

**Spec:** `docs/superpowers/specs/2026-10-31-iot-gis-submenu-highlight-design.md`

## Global Constraints

- `MapWorkspace` remains the sole `useIoTMapMonitoring` caller; do not move the hook or create a second monitoring state owner.
- `GISViewer` receives typed monitoring state through props; do not introduce circular imports between GIS and IoT modules.
- Keep exactly one `MapCanvas` and one MapLibre map instance in the `/gis/peta` workspace. Preserve existing GIS controls, basemap/layer/legend panels, measurement, location, close, keyboard, and lifecycle behavior; leave the existing legacy route/component untouched.
- IoT marker/list selection must stay synchronized. Only explicit logger selection or the explicit “Pusatkan logger” action may fit IoT devices, and both use `getIoTMapBounds`.
- Invalid-coordinate devices remain selectable in the list/detail flow but never create a marker or change the map view.
- Initial RSUP layer focus is a one-time catalog initialization behavior; it must not become polling-driven IoT auto-fit. Explicit `?layer=` selection and stored visibility overrides retain precedence.
- Do not change API requests, database/schema/TMAK values, routes, navbar, coordinate validation, status mapping, or sensor payloads.
- Permission-denied, monitoring-error, and metadata-error states must leave the existing GIS workspace usable.

## Review Focus

- The IoT panel is a compact, collapsed accordion inside the existing GIS sidebar; existing GIS panels remain visible and in their current order.
- Marker labels are readable, safe text content, and remain unrotated while the status pin keeps its existing visual states.
- TMAK and TMAT appear first in selected logger readings, with all other response values still retained below them.
- The preferred layer matcher is deterministic and tolerant of case/diacritics/metadata placement without hardcoding geometry or adding an endpoint.
- A stored hidden preference is respected; a later catalog refresh cannot unexpectedly re-enable or refit the preferred layer.
- Build and focused tests cover invalid coordinates, layer matching, priority readings, and existing IoT bounds behavior.

### Task 1: Add pure RSUP matching and TMAK/TMAT ordering helpers with tests

**Files:**
- Create: `src/features/webgis/lib/gis/layer-focus.ts`
- Modify: `src/features/iot/lib/monitoring.ts`
- Create: `tests/gisLayerFocus.test.mjs`
- Create: `tests/iotMonitoring.test.mjs`

**Interfaces:**
- `findPreferredGisLayer(layers: readonly MapLayer[]): MapLayer | null` returns the deterministic best catalog match for the RSUP plantation/division target, or `null` when no match exists. Matching may inspect normalized `name`, `groupName`, and `description`; it must not manufacture bounds.
- `MonitoringReading` contains `{ key: string; code: string; value: number | null }`.
- `getOrderedMonitoringReadings(data: Record<string, number | null>): MonitoringReading[]` preserves all response entries while ordering codes `TMAK` then `TMAT` before every other code. Code comparison is case-insensitive and supports existing `CODE#suffix` keys.

- [ ] **Step 1: Write failing tests first.** Cover a layer whose target terms are split across name/group/description, a generic/non-target catalog, deterministic tie handling, `TMAK`/`TMAT` before other values, `#suffix` keys, null values, and preservation of all entries.
- [ ] **Step 2: Run only the new focused tests** and confirm they fail for the missing exports, without running the full suite.
- [ ] **Step 3: Implement the smallest pure helpers.** Normalize Unicode/case/punctuation for matching, rank stronger target matches before weaker metadata matches, and use a stable original-order tie break. For readings, retain the original key/value and use stable priority sorting.
- [ ] **Step 4: Run `node --test tests/gisLayerFocus.test.mjs tests/iotMonitoring.test.mjs tests/iotMapBounds.test.mjs`** and confirm all focused pure tests pass.
- [ ] **Step 5: Review the helper diff** for no API imports, no coordinate fallbacks, and no changes to existing status/number formatting behavior.

### Task 2: Convert the existing IoT panel into a sidebar accordion and prioritize readings

**Files:**
- Modify: `src/features/webgis/components/IoTMonitoringPanel.tsx`
- Modify: `src/features/webgis/components/IoTMonitoringPanel.module.css`
- Test/verification: `tests/iotMonitoringPanelContract.test.mjs`

**Interfaces:**
- Extend `IoTMonitoringPanelProps` with `expanded: boolean` and `onToggle: () => void`; retain the existing typed `state` and `onFocusDevices` props.
- The panel keeps using the existing `IoTMapMonitoringState`, refresh callback, selection callback, error states, and weather detail.

- [ ] **Step 1: Add the accordion shell without changing data behavior.** Make the header an accessible toggle (`aria-expanded`/`aria-controls`) and keep the panel collapsed when `expanded` is false. Avoid nested buttons by keeping refresh as a separate control only in the expanded content/header action area.
- [ ] **Step 2: Keep all existing summary, logger list, explicit focus, error/loading, close-detail, and weather behavior inside the expanded region.** A permission-denied state still returns no IoT panel, and permission/loading errors must not hide the surrounding GIS sidebar.
- [ ] **Step 3: Use `getOrderedMonitoringReadings` in `LoggerDetail`.** Render the first TMAK/TMAT cards in a visually distinct priority group, then render every remaining reading below using the existing parameter metadata/name/unit and number formatter. Do not alter values or omit unknown parameters.
- [ ] **Step 4: Add compact accordion, priority-card, and responsive styles.** Preserve current status colors and readable mobile widths; ensure label/button focus styles remain visible.
- [ ] **Step 5: Run the TypeScript production build** after the component and CSS changes to catch prop, hook, and CSS-module errors.

### Task 3: Wire accordion opening, explicit IoT fits, and one-time RSUP layer focus in GISViewer

**Files:**
- Modify: `src/features/webgis/components/map/gis-viewer.tsx`
- Verify unchanged: the `MapWorkspace` file that calls `useIoTMapMonitoring`

**Interfaces:**
- `GISViewer` owns `iotPanelOpen` and passes `expanded`/`onToggle` to `IoTMonitoringPanel`.
- The wrapped `selectIoTLogger` opens the accordion, delegates to `iotMonitoring.selectLogger`, and calls `getIoTMapBounds([device])` only when the selected device has valid coordinates.
- The existing `focusIoTDevices` keeps its explicit `getIoTMapBounds(iotDevices)` behavior and does not run from monitoring refreshes or catalog polling.

- [ ] **Step 1: Add the closed-by-default accordion state and pass it into the panel.** Keep the existing sidebar open/close state separate from the IoT accordion state.
- [ ] **Step 2: Update the shared logger-selection callback.** Marker and list selection go through the same callback, opening the accordion and preserving the existing invalid-coordinate no-fit behavior. Do not fit merely because monitoring data arrived or changed.
- [ ] **Step 3: Add one-time preferred-layer initialization.** Import `findPreferredGisLayer`, then in the existing `selectedLayerApplied` initialization path choose a recognized explicit URL layer first; otherwise choose the preferred RSUP layer. Use only that layer’s existing valid bounds for the initial map focus, set its visibility to true only when no stored override exists, and respect an explicit stored `false` without re-enabling it. Later 30-second catalog refreshes must not repeat the focus.
- [ ] **Step 4: Keep existing visibility/session storage, layer panel, selected-layer errors, permission revocation, and map fit callbacks intact.** If the preferred layer is absent or has invalid bounds, the GIS workspace still loads normally and no fabricated geometry is used.
- [ ] **Step 5: Inspect the diff and run focused tests/build** to verify there is still one monitoring caller, one `MapCanvas`, and no route/navbar/API changes.

### Task 4: Add readable labels to the existing IoT MapLibre markers

**Files:**
- Modify: `src/features/webgis/components/map/map-canvas.tsx`
- Modify: `src/features/webgis/webgis.css`
- Test/verification: `tests/iotMarkerLabelContract.test.mjs`

**Interfaces:**
- `MapCanvas` continues to own the sole `new maplibregl.Map(...)` call and existing marker cleanup lifecycle.
- Valid IoT devices receive the current status/selection pin plus a sibling text label derived from `namaLogger`, then `loggerCode`, then `Logger {loggerId}`. The shared selection callback remains the only click action.

- [ ] **Step 1: Wrap each existing IoT pin in a marker element** containing the current accessible button and an `aria-hidden` visual label. Use `textContent`, not HTML, and keep the root/label non-interactive so the pin remains the click target.
- [ ] **Step 2: Keep filtering through `isMappableDevice` and preserve status tone, selected class, title, event propagation, marker cleanup, and the `latest` callback ref.
- [ ] **Step 3: Style the label as unrotated text beside/above the pin, with bounded width, ellipsis/wrapping, contrast, and mobile-safe sizing. Do not alter existing MapLibre controls or map lifecycle CSS.
- [ ] **Step 4: Run the build and inspect the marker diff** for one marker per valid device, no marker for invalid coordinates, and no second map/container.

### Task 5: Verify, publish, and confirm the hosted Docker deployment

**Files/repositories:**
- Verify the final diff in `E:/git-pkb/MyHelpDesk`.
- Publish the frontend commit from `E:/git-pkb/MyHelpDesk`.
- Refresh the bundled frontend subtree in `E:/git-pkb/APIHelpDesk` using the existing deployment workflow; do not edit API/database files for this feature.

- [ ] **Step 1: Run focused tests** for layer matching, monitoring ordering, and existing IoT bounds.
- [ ] **Step 2: Run `npm run build`** and inspect diagnostics for all modified frontend files.
- [ ] **Step 3: Review `git diff --stat` and the complete diff** for the global constraints: no API/database/TMAK value/route/navbar changes, no second map, no polling auto-fit, and no accidental edits to the user’s pre-existing `public/` files.
- [ ] **Step 4: Commit and push the frontend changes** to the configured `MyHelpDesk` remote, preserving unrelated worktree changes.
- [ ] **Step 5: Pull the pushed frontend into the `APIHelpDesk` `MyHelpDesk` subtree, commit only the subtree update, and push the deployment branch** so Dokploy’s configured compose build receives the new bundle.
- [ ] **Step 6: Monitor the Dokploy deployment until it completes successfully, then open the hosted `/gis/peta` page.** Confirm the deployed asset/build is newer, the GIS map and controls remain usable, the IoT accordion starts collapsed, the RSUP layer is initially focused when metadata permits, labels appear on valid logger markers, marker/list selection opens detail, TMAK/TMAT lead the detail cards, invalid coordinates do not move the view, and permission/error states do not blank GIS.

## Final acceptance checklist

- [ ] `MapWorkspace` remains the sole `useIoTMapMonitoring` caller.
- [ ] The GIS sidebar still contains the existing layer, basemap, legend, and measurement controls, with IoT added as a collapsed submenu.
- [ ] The `/gis/peta` workspace has one `MapCanvas`/MapLibre instance and no newly mounted parallel IoT map; the existing legacy route remains untouched.
- [ ] RSUP focus uses existing catalog metadata/bounds once, with URL and stored visibility precedence.
- [ ] IoT fits occur only from explicit selection/focus through `getIoTMapBounds`; invalid coordinates do not fit.
- [ ] Valid logger markers have unrotated identity labels and synchronized selection.
- [ ] TMAK and TMAT are first without losing other sensor readings.
- [ ] Focused tests and `npm run build` pass, and Dokploy serves the pushed bundle.
