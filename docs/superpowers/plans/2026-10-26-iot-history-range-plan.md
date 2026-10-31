# IoT History Range and Sampling Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add dynamic history range controls while preserving the legacy anchor-date API and keeping daily IoT charts at each logger's native sampling cadence.

**Architecture:** The API keeps `period` as the aggregation selector. Daily history uses a `sample` aggregation that passes through each payload; week/month/year use the existing day/day/month aggregation. Optional `from` and `to` query values override the generated window for non-daily UI modes, while `anchorDate` remains the fallback for old clients. The React page renders one date input for daily mode and two local `datetime-local` inputs for the other modes.

**Tech Stack:** ASP.NET Core/.NET 8, xUnit, SQL `DateTime2` half-open ranges, React/TypeScript, Recharts, CSS modules, Vite.

**Spec:** `docs/superpowers/specs/2026-10-26-iot-history-range-design.md`

## Global Constraints

- Preserve the existing `day`, `week`, `month`, and `year` period values.
- Daily mode uses one local calendar date and returns `[date 00:00, next date 00:00)`.
- Non-daily custom ranges use local `yyyy-MM-ddTHH:mm` values and SQL `[from,to)` semantics.
- `anchorDate=yyyy-MM-dd` remains valid when `from` and `to` are omitted.
- A custom range must include both values and satisfy `from < to`; malformed or invalid values return HTTP 400.
- Daily data must preserve 5-minute and 30-minute readings as separate points; no interpolation or cadence conversion.
- No changes to device inventory, latest-reading, permissions, threshold behavior, or unrelated routes.
- Do not overwrite unrelated pre-existing working-tree changes.

## Review Focus

- Daily data with mixed logger intervals must not be averaged into hourly points — pinned by the sample aggregation test and browser chart smoke check.
- Only one of `from`/`to`, malformed local date-times, and `from >= to` must return HTTP 400 — pinned by parser/range tests and controller validation.
- Omitted `from`/`to` must retain `anchorDate`/server-today behavior — pinned by resolver compatibility tests.
- Switching periods must show the correct number of controls and send the matching query shape — pinned by TypeScript build and browser smoke check.
- Editing a range while an older request is pending must not restore stale history — pinned by the existing active-request guard and dependency review.

---

### Task 1: Calendar windows and native daily sampling

**Files:**
- Modify: `src/AuthUtility.Api/IoT/IotHistoryPeriod.cs`
- Test: `tests/AuthUtility.Tests/IotHistoryPeriodTests.cs`

**Interfaces:**
- Consumes: existing `IotHistoryPeriodDefinition`, `IotHistoryWindow`, and `IotHistoryPoint` types.
- Produces: `TryParseRangeDateTime(string?, out DateTime)`, `Resolve(DateTime now, DateOnly? anchorDate = null, DateTime? rangeFrom = null, DateTime? rangeTo = null)`, and `sample` aggregation behavior.

- [ ] **Step 1: Write failing tests**

  Add tests that assert:
  - `day` creates `AggregationCode == "sample"`.
  - `Aggregate` returns every timestamped point for daily mode in timestamp/RawID order instead of averaging points into an hour.
  - An explicit `rangeFrom`/`rangeTo` is returned unchanged as the half-open window.
  - An omitted range still defaults to the existing calendar anchor behavior.
  - `TryParseRangeDateTime` accepts `2026-10-26T14:30` and rejects malformed or impossible values.

- [ ] **Step 2: Run the focused tests to verify RED**

  Run: `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --filter FullyQualifiedName~IotHistoryPeriodTests --no-restore`

  Expected: compile/test failure because the range parser, explicit window overload, and `sample` aggregation are not implemented.

- [ ] **Step 3: Implement the period contract**

  In `IotHistoryPeriod.cs`:
  - Change the daily aggregation code to `sample`.
  - Add an invariant exact parser for `yyyy-MM-ddTHH:mm` (optionally accept seconds only if the existing client contract requires it).
  - Let explicit `rangeFrom` and `rangeTo` override generated calendar dates when both are supplied.
  - Keep the current day/week/month/year calendar formulas when no explicit range is supplied.
  - Make `IotHistoryAggregator.Aggregate` return timestamped points unchanged for `sample`; keep existing averaging for `day` and `month`.

- [ ] **Step 4: Run the focused tests to verify GREEN**

  Run the same focused test command and expect all history-period tests to pass.

### Task 2: Backward-compatible history endpoint range parameters

**Files:**
- Modify: `src/AuthUtility.Api/Controllers/IotDeviceController.cs`

**Interfaces:**
- Consumes: Task 1 `TryParseRangeDateTime` and `Resolve`.
- Produces: `GET /api/iot/devices/{loggerId}/history` accepting optional `from` and `to` query values while preserving `period`-omitted legacy behavior.

- [ ] **Step 1: Add controller validation paths**

  Extend `GetHistory` with optional query strings named `from` and `to`. In period mode:
  - reject only-one-present;
  - parse both using the invariant local date-time parser;
  - reject `from >= to`;
  - pass the parsed values to `GetPeriodHistory`.

  If both are omitted, pass `anchorDate` as before. If `period` is omitted, continue calling `GetPayloads` with the existing `take` and `format` behavior.

- [ ] **Step 2: Preserve the half-open SQL query**

  Keep `H.TanggalSensor >= @from AND H.TanggalSensor < @to`, bind both values as `DbType.DateTime2`, and return the unchanged response metadata shape.

- [ ] **Step 3: Build the API**

  Run: `dotnet build src/AuthUtility.Api/AuthUtility.Api.csproj --no-restore -p:UseAppHost=false -p:OutputPath=E:\git-pkb\APIHelpDesk\.tmp-dotnet-range-build\`

  Expected: successful build without touching the running API apphost.

### Task 3: Frontend API/types contract

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/types/iot.ts`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/api/iotApi.ts`

**Interfaces:**
- Consumes: Task 2 query contract.
- Produces: `IoTHistoryAggregation` including `sample` and `getIoTHistory(loggerId, period, options?)` where `options` can contain `anchorDate`, `from`, and `to`.

- [ ] **Step 1: Update the TypeScript contract**

  Add `sample` to the history aggregation union and define a small optional history-query type so callers cannot accidentally send a partial custom range.

- [ ] **Step 2: Update the request builder**

  Send `period` plus either `anchorDate` or both `from`/`to`, omitting absent optional values. Do not change the legacy endpoint path or response type.

- [ ] **Step 3: Run the frontend typecheck**

  Run: `npm run build` from `E:\git-pkb\MyHelpDesk`.

  Expected: TypeScript and Vite build pass.

### Task 4: Dynamic range UI and chart presentation

**Files:**
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.tsx`
- Modify: `E:/git-pkb/MyHelpDesk/src/features/iot/pages/IoTMonitoringPage.module.css`

**Interfaces:**
- Consumes: Task 3 `getIoTHistory` options and `sample` aggregation.
- Produces: daily single-date control, non-daily start/end controls, range validation, period-aware labels, and stale-response-safe history loading.

- [ ] **Step 1: Add deterministic local date/time helpers and state**

  Keep one `historyAnchorDate` for daily mode and add local `historyRangeStart`/`historyRangeEnd` values formatted for `datetime-local`. Derive canonical defaults from the selected anchor date: week is seven calendar days, month is thirty calendar days, and year is twelve calendar months.

- [ ] **Step 2: Render the dynamic controls**

  Replace the single date control with:
  - one `date` input labelled `TANGGAL` when `historyPeriod === 'day'`;
  - `datetime-local` inputs labelled `MULAI` and `AKHIR` for week/month/year;
  - a compact effective-range summary, period selector, and aggregation badge.

  Apply `max` using the current local date/time and show an inline error when the range is incomplete or `start >= end`.

- [ ] **Step 3: Connect query selection and race guard**

  Send `anchorDate` for daily mode and `{ from, to }` for non-daily modes. Add all active range values to the history effect dependencies. Do not issue a request for an invalid range; retain the existing `active` cleanup guard so stale responses cannot overwrite current history/loading/error state.

- [ ] **Step 4: Make daily chart labels sampling-aware**

  Format `sample` timestamps with date and time, display the aggregation badge as `per interval logger`, and retain day/month formatting for week/month/year. Keep the empty-history message explicit when no numeric points are returned.

- [ ] **Step 5: Improve the filter panel styling**

  Group the controls in a responsive filter card, reduce unused whitespace, keep labels readable, and make the start/end fields stack cleanly on narrow screens without changing the surrounding dashboard layout.

- [ ] **Step 6: Run the frontend build**

  Run `npm run build`; expect success.

### Task 5: Integrated verification

**Files:**
- Inspect only: all files changed by Tasks 1–4 and the approved design/plan docs.

- [ ] **Step 1: Run backend tests**

  Run: `dotnet test tests/AuthUtility.Tests/AuthUtility.Tests.csproj --no-restore -p:UseAppHost=false -p:OutputPath=E:\git-pkb\APIHelpDesk\.tmp-dotnet-range-test\`

  Expected: all existing tests pass, including the new range and sampling cases.

- [ ] **Step 2: Run diagnostics and diff checks**

  Use the workspace problems checker for every changed source/test file and run `git diff --check` in both repositories. Expected: no code diagnostics and no whitespace errors in the feature changes.

- [ ] **Step 3: Run browser smoke checks**

  With the API running, verify:
  - daily mode has one date input, sends `period=day&anchorDate=...`, and shows raw points;
  - week/month/year expose two date-time inputs and send both `from` and `to`;
  - invalid end-before-start shows validation without a request;
  - changing the range while loading does not restore stale chart data;
  - the empty-state message remains visible for a range with no readings.

- [ ] **Step 4: Clean temporary build output and review scope**

  Remove only the temporary output directories created by verification, leave unrelated existing changes untouched, and inspect the final diff for accidental edits.
