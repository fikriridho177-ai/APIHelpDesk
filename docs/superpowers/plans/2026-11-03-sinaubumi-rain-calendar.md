# SinauBumi rainfall calendar correction

## Agreed behavior

The user approved proceeding after comparing RSUP-AWS-01 with the vendor dashboard.
SinauBumi labels the five-minute RAIN samples as intensity in mm/hour. The calendar
currently sums those rates as depths. October 2026 is 3660 in HelpDesk versus
305 mm and 14 rainy days in the vendor dashboard. October 10 is 1398.6 versus
116.55 mm; October 24 is 966.6 versus 80.55 mm.

## Scope and approach

Correct calendar presentation for source VENDOR, logger code RSUP-AWS-01 and
parameter RAIN only. Convert the daily sum of the verified five-minute rates by
5/60, equivalent to converting each sample before summing. Keep the API payload,
database, other pages, other loggers and RAIN_TIP unchanged. Recompute summary,
rainy-day threshold and calendar colors from converted values.

Changing generic history aggregation would affect other consumers and parameter
types. A scoped calendar conversion is sufficient for this defect. The fixed
five-minute sampling interval is vendor-verified, rather than inferred from gaps
or mutable logger configuration; missing observations are never extrapolated.

## Execution

1. Write regression tests using the 31 observed October daily sums and independent
   vendor expectations (305 mm, 14 rainy days, peak 116.55 mm). Cover source/logger/
   parameter isolation, zero, suffixed parameter keys and rounding boundaries.
2. Observe the tests fail against the existing identity conversion, implement the
   scoped conversion and connect the calendar and summary to the tested helpers.
3. Run all frontend tests, TypeScript/Vite production build and diff checks. Review
   source isolation and logger-switch behavior. Mirror only task files into the
   sibling frontend checkout, preserving existing unrelated changes.
4. Commit/push both repositories, observe Dokploy deployment, compare the live
   October calendar against vendor daily values and verify another logger.

## Progress

- Vendor diagnosis complete, including the chart label "Intensitas Hujan (mm/jam)"
  and daily comparison at https://rsup.sinaubumi.org/dashboard/aws-rsup1/analysis.
- RED: three tests reproduced inflated totals and rainy-day counts before the fix.
- GREEN: all 16 deployment-checkout tests and all 18 sibling-checkout tests passed.
- TypeScript/Vite production build passed; existing bundle-size advisory remains.
- Independent code review found no actionable bugs or regressions.
- Publication and live verification pending.
