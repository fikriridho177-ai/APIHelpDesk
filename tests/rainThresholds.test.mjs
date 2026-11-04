import test from 'node:test'
import assert from 'node:assert/strict'
import { classifyRainfall, formatRainfallCellTitle, getRainfallCellStyle, RAINFALL_CATEGORIES } from '../src/features/iot/lib/rainThresholds.ts'

test('classifies rainfall using the requested inclusive boundaries', () => {
  assert.equal(classifyRainfall(null).id, 'no-data')
  assert.equal(classifyRainfall(0).id, 'below-threshold')
  assert.equal(classifyRainfall(0.499).id, 'below-threshold')
  assert.equal(classifyRainfall(0.5).id, 'light')
  assert.equal(classifyRainfall(20).id, 'light')
  assert.equal(classifyRainfall(20.001).id, 'moderate')
  assert.equal(classifyRainfall(50).id, 'moderate')
  assert.equal(classifyRainfall(50.001).id, 'heavy')
  assert.equal(classifyRainfall(100).id, 'heavy')
  assert.equal(classifyRainfall(100.001).id, 'very-heavy')
  assert.equal(classifyRainfall(150).id, 'very-heavy')
  assert.equal(classifyRainfall(150.001).id, 'extreme')
})

test('uses the TMAK/TMAT visual theme for rainfall severity colors', () => {
  assert.deepEqual(
    RAINFALL_CATEGORIES.map(category => category.legend),
    ['Tidak ada data', '< 0,5 mm', '0,5–20 mm', '>20–50 mm', '>50–100 mm', '>100–150 mm', '>150 mm'],
  )
  assert.deepEqual(
    RAINFALL_CATEGORIES.map(category => category.background),
    ['#f1f5f4', '#ffffff', '#10b981', '#f59e0b', '#f97316', '#e11d48', '#7c3aed'],
  )
  assert.deepEqual(getRainfallCellStyle(150), { background: '#e11d48', color: '#ffffff' })
})

test('includes the rainfall category in the calendar cell title', () => {
  assert.equal(
    formatRainfallCellTitle('2026-06-12', 35.5, '35,5'),
    '2026-06-12: 35,5 mm · Hujan sedang',
  )
  assert.equal(formatRainfallCellTitle('2026-06-13', undefined, undefined), '2026-06-13: tidak ada data')
})
