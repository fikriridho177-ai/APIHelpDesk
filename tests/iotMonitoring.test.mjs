import test from 'node:test'
import assert from 'node:assert/strict'

import { getOrderedMonitoringReadings } from '../src/features/iot/lib/monitoring.ts'

test('orders TMAK then TMAT while retaining every monitoring response entry', () => {
  const readings = getOrderedMonitoringReadings({
    'ph#main': 6.8,
    'TMAT#main': 31.25,
    'TMAK#main': null,
    battery: 93,
  })

  assert.deepEqual(readings.map(reading => reading.code.toUpperCase()), ['TMAK', 'TMAT', 'PH', 'BATTERY'])
  assert.deepEqual(readings.map(reading => reading.key), ['TMAK#main', 'TMAT#main', 'ph#main', 'battery'])
  assert.equal(readings[0].value, null)
  assert.equal(readings[1].value, 31.25)
  assert.equal(readings.length, 4)
})
