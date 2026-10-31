# IoT History Anchor Date Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a date-anchor filter to period-based IoT history, keep legacy history requests compatible, and make the device registry route use the same permission gate as the IoT API.

**Architecture:** The API accepts an optional `anchorDate=YYYY-MM-DD` only for period mode. `IotHistoryPeriodDefinition` resolves calendar windows with the existing half-open SQL range, while omitted dates default to the server's current date. The monitoring page sends the selected local date and keeps its existing latest-data and stale-request guards. The registry page's default permission code is aligned with the API's `DASHBOARD_IOT` gate.

**Tech Stack:** ASP.NET Core 8, SQL Server, xUnit, React, TypeScript, CSS Modules, Vite.

**Spec:** `docs/superpowers/specs/2026-10-26-iot-history-anchor-date-design.md`

## Global Constraints

- Period mode supports exactly `day`, `week`, `month`, and `year`.
- `anchorDate` uses `YYYY-MM-DD` and is interpreted as a local calendar date.
- SQL ranges remain half-open: `[from, to)`.
- `day` covers the anchor date, `week` covers the anchor date plus the previous six days, `month` covers the anchor date plus the previous 29 days, and `year` covers the anchor month plus the previous 11 calendar months.
- Requests without `period` retain the existing `take` behavior and response shape.
- The IoT registry and IoT API use `DASHBOARD_IOT` as their access gate.
- Do not change the separate weather route or unrelated working-tree changes.

## Review Focus

- Missing `anchorDate` must default to today's date without changing no-period behavior; test both paths in Task 1.
- Invalid anchor dates must return a clear 400; test date parsing in Task 1.
- Date boundaries must not include the exclusive `to` instant; test all period windows in Task 1.
- Switching logger, period, or anchor date must prevent an older history response from overwriting the chart; verify in Task 2's browser smoke test.
- The registry must not stop at `NoAccess` because its page permission code differs from the API gate; verify the route in Task 3.

---

### Task 1: Add date-aware period windows to the API

**Files:**
- Modify: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/IoT/IotHistoryPeriod.cs`
- Modify: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/Controllers/IotDeviceController.cs`
- Modify: `E:/git-pkb/APIHelpDesk/tests/AuthUtility.Tests/IotHistoryPeriodTests.cs`

**Interfaces:**
- Consumes: existing `period` query values and current `DateTime.Now` fallback.
- Produces: `IotHistoryPeriodDefinition.Resolve(DateTime now, DateOnly? anchorDate = null)` and `GetHistory(..., string? anchorDate = null)` support.

- [ ] **Step 1: Add failing unit tests** for explicit anchor windows: day `[D,D+1)`, week `[D-6,D+1)`, month `[D-29,D+1)`, and year `[first-of-month(D)-11 months, first-of-next-month(D))`. Add tests for valid `YYYY-MM-DD`, invalid date text, and default-date resolution when no anchor is supplied.
- [ ] **Step 2: Run the focused tests and confirm they fail** because the resolver and controller contract do not accept an anchor date yet.
- [ ] **Step 3: Implement the minimal date parsing and window resolution** in `IotHistoryPeriod.cs`, preserving the existing no-argument `Resolve(DateTime)` call behavior through the optional argument.
- [ ] **Step 4: Extend `GetHistory`** with optional `anchorDate`; validate it only when period mode is used, return 400 for malformed dates, pass `DateOnly` into `GetPeriodHistory`, and keep the existing no-period call to `GetPayloads` unchanged. Continue binding `@from` and `@to` as `DateTime2` parameters.
- [ ] **Step 5: Run the focused xUnit tests** and confirm all date-window, parsing, and existing aggregation tests pass.

### Task 2: Add the monitoring date control and request contract

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/api/iotApi.ts`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.module.css`

**Interfaces:**
- Consumes: `IoTHistoryPeriod` and optional `anchorDate` from Task 1.
- Produces: `getIoTHistory(loggerId, period, anchorDate?)` and a local `YYYY-MM-DD` date input defaulted to today.

- [ ] **Step 1: Add a focused frontend test or executable browser assertion** for the date input's today default, `max=today`, and request query containing both `period` and `anchorDate`; if the repository has no frontend test runner, record the browser assertion as the smoke-test evidence.
- [ ] **Step 2: Run the new assertion before implementation** and confirm it fails because no date control/query exists.
- [ ] **Step 3: Update `getIoTHistory`** to accept an optional anchor date and send `{ period, anchorDate }` while omitting the optional parameter when absent.
- [ ] **Step 4: Add the date state and `<input type="date">`** beside the period selector, initialize it with the browser's local current date, cap it at today, and include it in the history effect dependency list and request. Keep the existing `active` response guard and latest summary untouched.
- [ ] **Step 5: Update the history heading styles** so the date control is usable beside the period selector on desktop and stacks within the existing mobile layout.
- [ ] **Step 6: Run the frontend assertion/build** and verify the request changes when the anchor date or period changes.

### Task 3: Align the device registry permission gate

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTVendorDevicesPage.tsx`

**Interfaces:**
- Consumes: the existing `DASHBOARD_IOT` permission and `/api/iot/devices` authorization gate.
- Produces: a registry page that does not request the unavailable `DAFTAR_PERANGKAT_VENDOR` permission by default.

- [ ] **Step 1: Add a regression assertion** that the default registry page permission code is `DASHBOARD_IOT`.
- [ ] **Step 2: Run the assertion and confirm it fails** against the current `DAFTAR_PERANGKAT_VENDOR` default.
- [ ] **Step 3: Change the page's default `MENU_CODE`** to `DASHBOARD_IOT`, leaving explicit `menuCode` overrides intact.
- [ ] **Step 4: Run diagnostics/build and open `/iot-management/daftar-perangkat`** on the authenticated origin; verify the page loads device data instead of `NoAccess`.

### Task 4: Integrated verification

**Files:**
- Test: `E:/git-pkb/APIHelpDesk/AuthUtility.sln`
- Test: `E:/git-pkb/MyHelpDesk/package.json`
- Test: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.tsx`
- Test: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTVendorDevicesPage.tsx`

- [ ] **Step 1: Run the focused backend tests.**
- [ ] **Step 2: Run the backend build.**
- [ ] **Step 3: Run the frontend build and diagnostics.**
- [ ] **Step 4: Smoke test today and a historical anchor date for each period**, verifying the displayed range and chart labels, then switch dates quickly to verify stale responses do not overwrite the current chart.
- [ ] **Step 5: Smoke test the registry route** on the same host used for login and verify no 403/NoAccess state occurs for the authenticated super-admin.
- [ ] **Step 6: Run `git diff --check` in both repositories and inspect changed-file scope.**
