# IoT submenu and RSUP plantation map focus

## Intent

Keep `/gis/peta` as the existing GIS workspace and add IoT as a compact submenu inside its existing sidebar. The GIS controls and panels remain available as they are today; IoT data appears when the submenu is opened or a logger marker/list item is selected.

## Goals

- Render a collapsed `Monitoring IoT` accordion in the existing GIS sidebar.
- Open the accordion when the user clicks it, a logger list item, or an IoT marker.
- Keep the existing single `MapCanvas`/MapLibre instance and all current GIS layer, basemap, legend, measurement, location, close, and keyboard behavior.
- Use the existing GIS layer catalog to focus the map on the layer representing PT Riau Sakti United Plantations / Divisi Perkebunan when the workspace first loads. Preserve an explicit layer URL and stored visibility choices.
- Render a visible label beside each valid IoT marker using the logger name/code.
- Keep marker/list selection synchronized and show selected sensor data in the accordion detail view.
- Prioritize `TMAK` and `TMAT` readings in the selected logger detail, while retaining other readings below them.

## Non-goals and constraints

- No API, database, route, navbar, coordinate, status, or sensor-value changes.
- No second map, old `IoTMonitoringMap`, polling auto-fit, or new request path.
- Invalid-coordinate devices remain in the list and detail flow but do not receive markers or change the view.
- Existing GIS behavior remains the default when permission is unavailable or monitoring fails.

## Approaches

1. **Accordion in the existing sidebar (chosen).** Adds the smallest UI surface, preserves all existing GIS panels, and lets marker selection open the sensor detail without introducing another workspace.
2. **GIS/IoT tabs.** More compact but hides existing GIS controls while IoT is active and changes the current sidebar navigation.
3. **Floating map card.** Keeps the sidebar unchanged but is not the requested submenu and risks covering map content.

## Design

`GISViewer` owns the accordion-open state and passes it to `IoTMonitoringPanel`. The existing typed monitoring state remains owned by `MapWorkspace`; the wrapped selection callback opens the accordion, selects the logger, and performs the existing explicit fit behavior.

The preferred RSUP layer is selected from the existing layer metadata by a pure, deterministic name/group/description match. Its existing bounds are used for one initial focus, without hardcoded geometry or a new endpoint. Explicit URL layer selection and stored visibility overrides retain precedence.

`MapCanvas` continues to create the only map instance. Each valid IoT marker gets its current status icon plus an unrotated text label. Clicking the marker calls the shared selection callback. The panel detail renders priority cards for parameter codes `TMAK` and `TMAT`, using values already present in the monitoring response and the existing parameter metadata lookup.

## Verification

- Add focused pure tests for RSUP layer matching and TMAK/TMAT priority extraction before implementation.
- Run the focused tests, the existing IoT bounds/TMAK tests, and the production build.
- Verify the deployed `/gis/peta` page has one map, the existing GIS controls, a collapsed IoT submenu, RSUP-focused initial view, labeled markers, and sensor detail after marker/list selection.
