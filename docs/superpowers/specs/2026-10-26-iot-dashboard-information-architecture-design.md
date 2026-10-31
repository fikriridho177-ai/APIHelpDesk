# Professional IoT dashboard information architecture

## Intent

The current `Dashboard IoT` page combines executive status, device registry,
data quality, historical charts, and weather. This makes the first screen
long and difficult to scan. The dashboard should become an operational
overview, while each working area remains on its own existing page.

## Goals

- Make `/iot-management/dashboard-iot` a concise control-room overview.
- Keep the existing logger registry pages for device inventory and filtering.
- Keep vendor and local monitoring pages responsible for readings, metadata,
  and historical charts.
- Keep the 10-day weather and BMKG page independent from the IoT health
  dashboard.
- Preserve the existing authenticated `DASHBOARD_IOT` permission gate and API
  contracts.

## Non-goals

- No database or backend endpoint changes.
- No changes to logger freshness rules, weather provider behavior, or chart
  calculations.
- No new npm dependency or replacement of the existing navigation model.

## Approaches considered

1. **Separate operational dashboard (selected):** create a summary page with
   health KPIs, status distribution, attention queue, and links to the
   existing registry, monitoring, and weather pages. This best matches the
   requirement that the first page should not be mixed.
2. **Compact dashboard with a weather strip:** reduces the page height but
   keeps two different operational concerns together.
3. **Tabbed single page:** reduces route changes but keeps registry, detail,
   and overview state in one large component and remains harder to scan.

## Selected experience

### Dashboard IoT

The dashboard contains:

- a dark operational header with last refresh, source scope, and refresh/
  auto-refresh controls;
- KPI cards for total logger, current data, attention-needed logger, and
  active logger count;
- a freshness distribution panel with counts for current, late, empty, and
  unknown data;
- an attention queue showing up to five late, empty, or unknown logger rows;
- quick-action cards linking to `Daftar Perangkat`, `Daftar Perangkat Vendor`,
  `Daftar Logger Lokal`, `Monitoring Sensor Vendor`, `Monitoring Logger Lokal`,
  and `Prakiraan Cuaca`.

The dashboard does not render weather cards, device registry cards, parameter
charts, or full history tables.

### Existing working pages

- `IoTVendorDevicesPage` remains the registry for all, vendor, and local
  devices.
- `IoTMonitoringPage` remains the working page for vendor/local readings,
  parameter metadata, and history charts.
- `WeatherForecastPage` remains the dedicated forecast and BMKG warning page,
  including logger coordinate selection.

## Data and interaction

The dashboard reads the existing `/api/iot/devices` response and the latest
reading endpoint for each logger. It must not call history, parameter, or
weather endpoints. Selecting an attention row navigates to the appropriate
monitoring page based on `sourceType`; source-specific navigation remains
available through the quick actions.

Loading, API error, empty data, and a fully healthy fleet must each have an
explicit visual state. The layout must collapse to one column on narrow
screens without hiding the refresh action or attention queue.

## Acceptance criteria

- The dashboard opens with summary and actions visible without scrolling past
  large registry/chart sections.
- Forecast cards and BMKG alerts appear only on the dedicated weather route.
- Device cards appear only on registry routes.
- Historical parameter charts appear only on monitoring routes.
- Existing vendor/local routes continue to load their detailed monitoring
  page and use the same permission checks.
- `npm run build` succeeds and the dashboard is verified through the local
  browser with real logger data.
