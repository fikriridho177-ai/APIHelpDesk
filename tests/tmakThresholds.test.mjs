import test from 'node:test'
import assert from 'node:assert/strict'
import {
  classifyTmak,
  formatTmakCategoryLabel,
  getTmakCellStyle,
  KLHK_TMAK_THRESHOLD_CM,
} from '../src/features/iot/lib/tmakThresholds.ts'

test('uses the SiTMAT/KLH TMAK bands', () => {
  assert.equal(KLHK_TMAK_THRESHOLD_CM, -40)
  assert.equal(classifyTmak(10)?.id, 'high')
  assert.equal(classifyTmak(0.001)?.id, 'high')
  assert.equal(classifyTmak(0)?.id, 'normal')
  assert.equal(classifyTmak(-39.999)?.id, 'normal')
  assert.equal(classifyTmak(-40)?.id, 'vulnerable')
  assert.equal(classifyTmak(-79.999)?.id, 'vulnerable')
  assert.equal(classifyTmak(-80)?.id, 'very-vulnerable')
  assert.equal(classifyTmak(-120)?.id, 'very-vulnerable')
})

test('colors four TMAK bands and leaves missing data neutral', () => {
  const backgrounds = [10, -20, -50, -80].map(value => getTmakCellStyle(value).background)
  assert.equal(new Set(backgrounds).size, 4)
  assert.deepEqual(getTmakCellStyle(undefined), getTmakCellStyle(null))
  assert.deepEqual(getTmakCellStyle(Number.NaN), getTmakCellStyle(undefined))
})

test('provides early-warning labels for calendar and monitoring views', () => {
  assert.equal(formatTmakCategoryLabel(5), 'Siaga Banjir')
  assert.equal(formatTmakCategoryLabel(-20), 'Normal')
  assert.equal(formatTmakCategoryLabel(-50), 'Waspada')
  assert.equal(formatTmakCategoryLabel(-100), 'Kritis')
  assert.equal(formatTmakCategoryLabel(null), 'Tidak ada data')
})
