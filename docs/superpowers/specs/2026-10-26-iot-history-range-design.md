# IoT History Range and Sampling Design

Date: 2026-10-26

## Intent

Improve the IoT history filter UI so users can select a single calendar date for
the daily view and explicit start/end date-times for weekly, monthly, and yearly
views. Daily charts must preserve the logger's sampling cadence instead of
averaging 5-minute and 30-minute readings into hourly points.

## Goals

- Keep the existing `day`, `week`, `month`, and `year` period selector.
- Daily mode renders one date input and requests the full local calendar day.
- Other period modes render editable local start/end date-time inputs.
- Use a half-open SQL range: `[from, to)`.
- Preserve `anchorDate` behavior for existing clients that do not send a custom
  range.
- Keep stale-response protection and meaningful loading/empty/error states.
- Make the history controls visually grouped and readable on desktop/mobile.

## Non-goals

- No changes to device inventory, latest-reading, permissions, or threshold
  behavior.
- No automatic cadence conversion or interpolation of sensor readings.

## Approach

The selected period remains the aggregation choice. The daily period uses the
single `anchorDate` input and changes its aggregation code to `sample`, meaning
each returned payload remains a chart point. This preserves both 5-minute and
30-minute logger data. Week and month continue aggregating by day; year
continues aggregating by month.

For custom ranges, the API accepts optional `from` and `to` query values in
local `yyyy-MM-ddTHH:mm` form. They are validated as a pair with `from < to`
and used directly as the half-open SQL window. If neither is supplied, the
existing `anchorDate`/server-today fallback remains in effect.

The UI derives default ranges from the selected date and period, prevents
future values with input limits, blocks requests for an invalid range, and
passes only the active request's result into state.

## API contract

`GET /api/iot/devices/{loggerId}/history?period=day|week|month|year`

- Existing calls remain valid.
- `anchorDate=yyyy-MM-dd` remains supported and defaults to the server's local
  date when omitted.
- `from=yyyy-MM-ddTHH:mm&to=yyyy-MM-ddTHH:mm` is an optional custom range.
- A custom range must include both values and satisfy `from < to`; malformed
  values or an invalid range return HTTP 400.
- The response shape and `from`/`to` metadata remain unchanged.

## UI behavior

- `day`: show one `TANGGAL` input; use the selected calendar day from 00:00 to
  the next day at 00:00.
- `week`, `month`, `year`: show `MULAI` and `AKHIR` `datetime-local` inputs;
  initialize them from the current period window, but allow edits.
- Show the effective range as a compact summary beside the controls.
- Show an inline validation message and do not request history until the range
  is valid.
- When a custom range is edited, stale responses from a previous request must
  not overwrite the current chart.

## Open implementation calls

- The frontend will send local wall-clock values without a timezone suffix;
  the backend will parse them as `DateTime` values for the existing SQL
  `DateTime2` comparison.
- The `sample` aggregation label will be displayed as `per interval logger`.
