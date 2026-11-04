import test from 'node:test'
import assert from 'node:assert/strict'
import { classifyTmat, getTmatCellStyle } from '../src/features/iot/lib/tmatThresholds.ts'

test('follows vendor categories including exact zero, -40 and -60 cm boundaries', () => {
  for (const [value, expected] of [
    [10, 'flooded'], [0.001, 'flooded'],
    [0, 'normal'], [-0.001, 'normal'], [-39.999, 'normal'], [-40, 'normal'],
    [-40.001, 'vulnerable'], [-59.999, 'vulnerable'], [-60, 'vulnerable'],
    [-60.001, 'very-vulnerable'], [-79.999, 'very-vulnerable'], [-80, 'very-vulnerable'], [-150, 'very-vulnerable'],
  ]) assert.equal(classifyTmat(value)?.id, expected, `TMAT ${value} cm`)
})

test('missing and nonfinite observations never become normal or vulnerable', () => {
  for (const value of [undefined, null, NaN, Infinity, -Infinity]) {
    assert.equal(classifyTmat(value), null)
    assert.deepEqual(getTmatCellStyle(value), getTmatCellStyle(undefined))
  }
  assert.notDeepEqual(getTmatCellStyle(0), getTmatCellStyle(undefined))
})

test('uses stable category colors rather than the observed yearly range', () => {
  assert.deepEqual(getTmatCellStyle(-40), getTmatCellStyle(-20))
  assert.deepEqual(getTmatCellStyle(-40.001), getTmatCellStyle(-60))
  assert.deepEqual(getTmatCellStyle(-60.001), getTmatCellStyle(-150))
  const backgrounds = [5, -20, -50, -70].map(value => getTmatCellStyle(value).background)
  assert.equal(new Set(backgrounds).size, 4)
})
