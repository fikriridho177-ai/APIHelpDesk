# IoT History Filter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add period-aware, aggregated IoT history to the unified monitoring page.

**Architecture:** The API keeps the existing raw `take` history behavior for
backward compatibility and adds a period mode that reads a bounded time window
from SQL Server and aggregates numeric values into predictable time buckets.
The React monitoring page sends the selected period and renders the returned
bucket timestamps with period-aware labels.

**Tech Stack:** ASP.NET Core 8, SQL Server, xUnit, React, TypeScript, CSS Modules, Recharts, Vite.

**Spec:** `docs/superpowers/specs/2026-10-26-iot-history-filter-design.md`

## Global Constraints

- Keep one canonical monitoring page at `/iot-management/monitoring-sensor`.
- Support exactly `day`, `week`, `month`, and `year` period values in period mode.
- Use hourly buckets for `day`, daily buckets for `week` and `month`, and monthly buckets for `year`.
- Preserve the existing no-period `take` history contract.
- Preserve unrelated working-tree changes and existing weather-page separation.

## Review Focus

- A period request must cover its complete window rather than silently returning only the previous 100 rows.
- Invalid period values must return a clear 400 response.
- Empty periods must render as an empty state, not a generic server failure.
- Parameter keys with null or missing values must not break bucket averages.
- Switching logger and period quickly must not allow an older request to overwrite the current chart.

---

### Task 1: Add period-aware history contract and aggregation

**Files:**
- Create: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/IoT/IotHistoryPeriod.cs`
- Create: `E:/git-pkb/APIHelpDesk/tests/AuthUtility.Tests/IotHistoryPeriodTests.cs`
- Modify: `E:/git-pkb/APIHelpDesk/src/AuthUtility.Api/Controllers/IotDeviceController.cs`

**Interfaces:**
- Consumes: existing compact IoT payload DTOs and `TrnLoggerRawHdr`/detail data.
- Produces: `period` history query support and period metadata in the response.

- [x] **Step 1: Write failing tests** for period parsing, each lookback/bucket mapping, bucket boundaries, and averaging numeric values while ignoring nulls.
- [x] **Step 2: Run the focused xUnit test and confirm it fails because the period contract/aggregator does not exist.**
- [x] **Step 3: Implement `IotHistoryPeriodDefinition` and the minimal aggregation helpers.**
- [x] **Step 4: Run the focused test and confirm it passes.**
- [x] **Step 5: Extend `GetHistory` with `period`, bounded SQL range parameters, empty responses, aggregation metadata, and a 400 for invalid periods while leaving no-period behavior intact.**
- [x] **Step 6: Run the focused backend tests and build the API.**

### Task 2: Add frontend period filter and chart formatting

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/types/iot.ts`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/api/iotApi.ts`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.module.css`

**Interfaces:**
- Consumes: `GET /api/iot/devices/{loggerId}/history?period=...`.
- Produces: period selector, request cancellation guard, period-aware axis/tooltips, and range summary.

- [x] **Step 1: Add the period types and API parameter contract.**
- [x] **Step 2: Add the period selector and update the history effect to request the selected period.**
- [x] **Step 3: Pass aggregation metadata through the chart components and format hourly/daily/monthly timestamps appropriately.**
- [x] **Step 4: Style the control responsively without changing the existing dashboard layout.**
- [x] **Step 5: Run the frontend build and diagnostics.**

### Task 3: Verify the integrated experience

**Files:**
- Test: `E:/git-pkb/APIHelpDesk/AuthUtility.sln`
- Test: `E:/git-pkb/MyHelpDesk/package.json`
- Test: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.tsx`

- [x] **Step 1: Run the full backend test suite.**
- [x] **Step 2: Run the backend build.**
- [x] **Step 3: Run the frontend build and diagnostics.**
- [x] **Step 4: Smoke test each period in the browser with the current logger and verify the request/result changes.**
- [x] **Step 5: Run `git diff --check` in both repositories and inspect changed-file scope.**
