import test from 'node:test'
import assert from 'node:assert/strict'

import { getVisualizationKind } from '../src/features/iot/lib/historyVisualization.ts'

test('uses trend charts for continuous air and wind-speed readings', () => {
  assert.equal(getVisualizationKind('HUMIDITY', 'Kelembapan Udara'), 'trend')
  assert.equal(getVisualizationKind('WIND_SPEED', 'Kecepatan Angin'), 'trend')
})

test('uses specialized charts only for semantically matching parameters', () => {
  assert.equal(getVisualizationKind('RAIN_TIP', 'Curah Hujan'), 'rainfall')
  assert.equal(getVisualizationKind('WIND_DIRECTION', 'Arah Angin'), 'direction')
  assert.equal(getVisualizationKind('TMAK', 'Tinggi Muka Air Kanal'), 'level')
  assert.equal(getVisualizationKind('SOLAR_RADIATION', 'Radiasi Matahari'), 'intensity')
})
