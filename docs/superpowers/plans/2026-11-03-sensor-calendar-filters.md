# Kalender Data Sensor implementation plan

**Goal:** Extend the existing annual calendar with Curah Hujan, TMAK and TMAT
filters, showing only compatible sensors. The user approved the neutral name
Kalender Data Sensor and explicitly chose daily averages for TMAK/TMAT.

**Architecture:** Keep the existing route and permissions. Add a tested calendar
domain helper for parameter aliases, sensor selection, history queries and daily
values. Reuse the existing daily API aggregation and rainfall conversion. Query
parameters preserve kind, logger and year. Do not change raw data or the API.

## Decisions

- Filters: Jenis Data -> Logger Sensor -> Tahun. Default Curah Hujan preserves
  old links. A compatible current logger stays selected; otherwise select the
  first compatible logger. No matches produces an explicit empty state.
- Identify capability by supported parameter keys, including suffixed keys and
  LPA_TMAT. Zero is valid; null observations stay missing. A logger with a known
  parameter is not hidden simply because its latest sample is missing.
- AWQ has none of the supported parameters and must be absent from every list.
- Rainfall retains existing source-specific conversion and daily sum; the
  verified SinauBumi October regression remains 305 mm and 14 rainy days.
- TMAK/TMAT request average rather than sum, in cm. Preserve AWL-C001's existing
  +10 display offset for TMAK. Summaries show mean of available daily averages,
  minimum, maximum and days with data. Never sum water levels or use rain bands.
- Level cell colors show relative lower/higher values in the displayed year;
  they do not imply safety thresholds. Missing observations remain distinct.
- Rename the active page, WAD service card, breadcrumb and relevant menu labels.
  Preserve /ecs/kalender-curah-hujan and permission codes for compatibility.
- Guard stale responses across kind/logger/year changes, including browser Back.
- Keep pre-existing sibling changes to iotApi.ts and untracked files untouched.

## Tasks and verification

1. Write failing Node tests for aliases, capability filtering, zero/null, AWQ,
   fallback selection, average/sum query contracts, cm offsets, stale buckets,
   negative levels, missing days and retained vendor rainfall regression.
2. Implement the helper; wire the existing page to query-driven filters and
   matching summaries, legends and empty states. Adjust responsive filter grid.
3. Rename menu/card/breadcrumb labels. Run all frontend tests, production build,
   editor diagnostics and diff checks. Obtain an independent code review.
4. Mirror only task files to the sibling frontend, test there, commit and push
   both repositories. Observe Dokploy deployment and verify all three filter
   lists, averages, rainfall October totals, old links and responsive rendering.

## Review focus

- Sum must never be used for TMAK/TMAT.
- An invalid selected logger cannot remain in a filtered list.
- Previous query results cannot be rendered using the new kind or year.
- Rainfall correction remains limited to its verified source and parameter.
- Renaming labels cannot remove existing routes or access permissions.
