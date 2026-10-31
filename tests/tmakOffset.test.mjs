import test from 'node:test'
import assert from 'node:assert/strict'

import { adjustTmakValue } from '../src/features/iot/lib/tmakOffset.ts'

test('adds 10 to TMAK for AWL-C001', () => {
  assert.equal(adjustTmakValue('AWL-C001', 'TMAK', -65.95), -55.95)
})

test('adds 10 to the TMAK_DISTANCE telemetry alias for AWL-C001', () => {
  assert.equal(adjustTmakValue('awl-c001', 'tmak_distance', '-65.95'), -55.95)
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
