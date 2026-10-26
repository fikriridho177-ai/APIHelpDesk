# Weather forecast and BMKG warning layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a cached 10-day Open-Meteo forecast and BMKG local warning layer to the IoT/WAD monitoring experience.

**Architecture:** The ASP.NET Core API owns provider calls, coordinate validation, parsing, and a 15-minute memory cache. React consumes one normalized endpoint through the existing authenticated API client; a reusable weather panel renders alerts and forecast cards on the IoT dashboard and a full detail route.

**Tech Stack:** ASP.NET Core 8, `HttpClient`, `IMemoryCache`, `System.Text.Json`, `XDocument`, React, TypeScript, CSS Modules, Vite.

**Spec:** `docs/superpowers/specs/2026-10-26-weather-forecast-bmkg-design.md`

## Global Constraints

- Keep provider requests in the backend; no weather API key or direct BMKG request may be added to frontend code.
- Use Open-Meteo for up to 10 daily forecast records and BMKG nowcast for official local warnings.
- A BMKG outage must return the forecast with `alertStatus: "unavailable"`, not fail the whole request.
- Keep IoT access read-only and protected by the existing `DASHBOARD_IOT` permission.
- Read logger coordinates from the existing `Latitude`/`Longitude` columns in
  `MstLogger`; never infer them from the free-text `Lokasi` field.
- Preserve unrelated working-tree changes, including `APIHelpDesk/.tmp-db-threshold-probe/`.

## Review Focus

- Coordinates outside latitude/longitude bounds: the API must return `400`, not call providers.
- Open-Meteo returns fewer daily rows than requested: the response must reflect actual rows and never fabricate days.
- BMKG returns namespaced RSS/CAP XML or malformed optional fields: parse valid items without crashing the forecast.
- BMKG is unavailable: forecast remains usable and the UI communicates degraded warning status.
- Refreshing the dashboard repeatedly: identical location requests must use the cache window instead of issuing a provider call each time.

### Task 1: Add the backend weather contract and provider service

**Files:**
- Create: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/Weather/WeatherDtos.cs`
- Create: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/Weather/WeatherOptions.cs`
- Create: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/Weather/WeatherService.cs`
- Create: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/Controllers/WeatherController.cs`
- Modify: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/Program.cs`
- Test: `E:/git-pkb/APIHelpDesk/tests/AuthUtility.Tests/WeatherServiceTests.cs`

**Interfaces:**
- Consumes: `IPermissionService`, `HttpClient`, Open-Meteo JSON, BMKG RSS/CAP XML.
- Produces: `WeatherService.GetForecastAsync(WeatherQuery, CancellationToken)` and `GET /api/weather/forecast` with the response contract in the spec.

- [x] **Step 1: Write failing service tests** for 10-day mapping, invalid coordinate validation, BMKG alert parsing, and BMKG outage fallback using a fake `HttpMessageHandler`.
- [x] **Step 2: Run `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore --filter FullyQualifiedName~WeatherServiceTests`** and confirm the tests fail because the weather service/contract does not exist.
- [x] **Step 3: Implement DTOs, options, provider parsing, cache keying, and graceful BMKG fallback** in the new weather files.
- [x] **Step 4: Register memory cache, typed HTTP client, and options in `Program.cs`; add controller validation and the existing IoT permission gate.**
- [x] **Step 5: Run the focused test command again and confirm all weather tests pass.**
- [x] **Step 6: Commit backend weather endpoint changes with `git add` and `git commit -m "feat: add weather forecast and BMKG warning API"`.**

### Task 2: Add the frontend weather API and reusable panel

**Files:**
- Create: `E:/git-pkb/MyHelpDesk/src/features/weather/types/weather.ts`
- Create: `E:/git-pkb/MyHelpDesk/src/features/weather/api/weatherApi.ts`
- Create: `E:/git-pkb/MyHelpDesk/src/features/weather/components/WeatherForecastPanel.tsx`
- Create: `E:/git-pkb/MyHelpDesk/src/features/weather/components/WeatherForecastPanel.module.css`
- Create: `E:/git-pkb/MyHelpDesk/src/features/weather/pages/WeatherForecastPage.tsx`

**Interfaces:**
- Consumes: `GET /weather/forecast` through the authenticated axios client and the `DASHBOARD_IOT` permission.
- Produces: `WeatherForecastPanel` with `compact?: boolean`, editable coordinates, BMKG alert state, refresh action, and forecast card rendering.

- [x] **Step 1: Define frontend response types and the API function** `getWeatherForecast(query)` matching the backend contract.
- [x] **Step 2: Implement loading, error, unavailable-alert, and forecast rendering states** with the location fallback specified by the spec.
- [x] **Step 3: Implement compact and detail layouts** with an explicit BMKG attribution and Open-Meteo attribution.
- [x] **Step 4: Run `npm run build` and confirm the new TypeScript/CSS code compiles before wiring routes.**

### Task 3: Integrate weather into IoT navigation and dashboard

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/routes/AppRoutes.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/routes/MenuRouteResolver.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/components/layout/AppBreadcrumb.tsx`

**Interfaces:**
- Consumes: `WeatherForecastPanel` and existing IoT page permission state.
- Produces: compact five-day preview on `/iot-management/dashboard-iot`, full detail on `/iot-management/prakiraan-cuaca`, and a dashboard link to the detail page.

- [x] **Step 1: Add the compact panel after the logger health summary** without changing existing IoT API calls or permission behavior.
- [x] **Step 2: Add the detail route and breadcrumb label** using the same `DASHBOARD_IOT` access gate.
- [x] **Step 3: Add the route resolver mapping only if the menu API can expose the route; otherwise keep the direct route as the stable UI entry point.**
- [x] **Step 4: Run `npm run build` and inspect the rendered page through the existing local dev server.**

### Task 4: Verify the integrated feature

**Files:**
- Test: `E:/git-pkb/APIHelpDesk/AuthUtility.sln`
- Test: `E:/git-pkb/MyHelpDesk/package.json`

- [x] **Step 1: Run `dotnet test AuthUtility.sln --no-restore`.**
- [x] **Step 2: Run `dotnet build AuthUtility.sln --no-restore`.**
- [x] **Step 3: Run `npm run build`.**
- [x] **Step 4: Run `git diff --check` in both repositories and inspect changed-file scope.**
- [x] **Step 5: Call the backend endpoint with valid and invalid coordinates, then verify the frontend root remains reachable.**
- [x] **Step 6: Commit the frontend integration with `git add` and `git commit -m "feat: add IoT weather forecast panel"`; report any provider/network limitation separately.**

### Task 5: Drive weather location from logger coordinates

**Files:**
- Modify: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/Controllers/IotDeviceController.cs`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/types/iot.ts`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/weather/types/weather.ts`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/weather/components/WeatherForecastPanel.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/weather/pages/WeatherForecastPage.tsx`

**Interfaces:**
- Consumes: `Latitude` and `Longitude` columns selected from `dbo.MstLogger`.
- Produces: automatic forecast relocation when the selected logger changes and
  a logger selector on the detail page.

- [x] **Step 1: Include nullable latitude/longitude in `/api/iot/devices`** while preserving all existing response fields.
- [x] **Step 2: Extend frontend device/weather types and pass selected logger coordinates into the compact dashboard panel.**
- [x] **Step 3: Add a logger selector to the full weather page and show a clear fallback state for loggers without coordinates.**
- [x] **Step 4: Run backend tests/build and frontend build; verify the provided logger coordinates map to the expected forecast query.**
- [ ] **Step 5: Commit the coordinate-aware integration.**
