import test from 'node:test'
import assert from 'node:assert/strict'
import {
  classifyTmak,
  getTmakCellStyle,
  KLHK_TMAK_THRESHOLD_CM,
} from '../src/features/iot/lib/tmakThresholds.ts'

test('uses the KLHK 40 cm boundary for signed TMAK values', () => {
  assert.equal(KLHK_TMAK_THRESHOLD_CM, -40)
  assert.equal(classifyTmak(0)?.id, 'within')
  assert.equal(classifyTmak(-40)?.id, 'within')
  assert.equal(classifyTmak(-40.001)?.id, 'below')
  assert.equal(classifyTmak(-120)?.id, 'below')
})

test('colors TMAK cells by the KLHK threshold and leaves missing data neutral', () => {
  assert.notDeepEqual(getTmakCellStyle(-40), getTmakCellStyle(-40.001))
  assert.deepEqual(getTmakCellStyle(undefined), getTmakCellStyle(null))
  assert.deepEqual(getTmakCellStyle(Number.NaN), getTmakCellStyle(undefined))
})
