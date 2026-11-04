import test from 'node:test'
import assert from 'node:assert/strict'

import {
  adjustTmakValue,
  TMAK_CORRECTION_THRESHOLD_CM,
  TMAK_DISPLAY_OFFSET_CM,
} from '../src/features/iot/lib/tmakOffset.ts'

test('adds 10 only to AWL-C001 TMAK values below -60 cm', () => {
  assert.equal(TMAK_CORRECTION_THRESHOLD_CM, -60)
  assert.equal(TMAK_DISPLAY_OFFSET_CM, 10)
  assert.equal(adjustTmakValue('AWL-C001', 'TMAK', -65.95), -55.95)
  assert.equal(adjustTmakValue('AWL-C001', 'TMAK', -100), -90)
  assert.equal(adjustTmakValue('AWL-C001', 'TMAK', -60), -60)
  assert.equal(adjustTmakValue('AWL-C001', 'TMAK', -50), -50)
})

test('adds 10 to the TMAK_DISTANCE telemetry alias for AWL-C001', () => {
  assert.equal(adjustTmakValue('awl-c001', 'tmak_distance', '-65.95'), -55.95)
})

test('also corrects the TMAK_CM alias below the cutoff', () => {
  assert.equal(adjustTmakValue('AWL-C001', 'TMAK_CM', -80), -70)
})

test('matches the API logger code together with the displayed logger name', () => {
  assert.equal(adjustTmakValue('AWLC-PKB-001', 'TMAK_DISTANCE', -65.95, 'AWL-C001'), -55.95)
})

test('leaves other loggers and parameters unchanged', () => {
  assert.equal(adjustTmakValue('AWL-C002', 'TMAK', -65.95), -65.95)
  assert.equal(adjustTmakValue('AWL-C001', 'RAIN_TIP', 12), 12)
})

test('leaves null and nonnumeric TMAK values unchanged', () => {
  assert.equal(adjustTmakValue('AWL-C001', 'TMAK', null), null)
  assert.equal(adjustTmakValue('AWL-C001', 'TMAK', 'offline'), 'offline')
})
