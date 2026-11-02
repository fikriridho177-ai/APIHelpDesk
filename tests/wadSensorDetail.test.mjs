import test from 'node:test'
import assert from 'node:assert/strict'

import { groupLiveParameters } from '../src/features/ecs/utils/wadSensorDetail.ts'

test('groups live readings by CategoryName while preserving category order', () => {
  const groups = groupLiveParameters([
    { code: 'TMAK', label: 'Tinggi muka air kanal', unit: 'cm', value: -40, categoryName: 'Ketinggian Air', categoryOrder: 2 },
    { code: 'RAIN_TIP', label: 'Curah Hujan', unit: 'mm', value: 0, categoryName: 'Curah Hujan', categoryOrder: 3 },
    { code: 'TMAT', label: 'Tinggi muka air tanah', unit: 'cm', value: -34, categoryName: 'Ketinggian Air', categoryOrder: 2 },
  ])

  assert.deepEqual(groups.map(group => group.name), ['Ketinggian Air', 'Curah Hujan'])
  assert.deepEqual(groups[0].parameters.map(parameter => parameter.code), ['TMAK', 'TMAT'])
})

test('puts readings without a category in a readable fallback section', () => {
  const groups = groupLiveParameters([
    { code: 'WIND_SPEED', label: 'Kecepatan Angin', unit: 'm/s', value: 1, categoryName: '', categoryOrder: Number.MAX_SAFE_INTEGER },
  ])

  assert.equal(groups[0].name, 'Data lainnya')
  assert.equal(groups[0].parameters[0].label, 'Kecepatan Angin')
})
