import test from 'node:test'
import assert from 'node:assert/strict'

import { getHistoryTooltipValue } from '../src/features/iot/lib/historyChart.ts'

test('uses the source point value for directional scatter tooltips', () => {
  assert.equal(getHistoryTooltipValue({ rawValue: 1793553000000, sourceValue: 225, directional: true }), 225)
})

test('keeps the payload value for regular chart tooltips', () => {
  assert.equal(getHistoryTooltipValue({ rawValue: 67, sourceValue: 67, directional: false }), 67)
})
