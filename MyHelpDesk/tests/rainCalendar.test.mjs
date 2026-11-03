import test from 'node:test'
import assert from 'node:assert/strict'
import { toCalendarRainfallMm, summarizeCalendarRainfall } from '../src/features/iot/lib/rainCalendar.ts'

const sinauBumi = { sourceType: 'VENDOR', loggerCode: 'RSUP-AWS-01' }

// API daily sums for October 1-31, independently compared with SinauBumi Daily.
const octoberRateSums = [
  3.6, 145.2, 0, 0, 0, 0, 0, 0, 0, 1398.6, 33, 2.4, 171, 0, 0,
  382.8, 176.4, 0, 74.4, 1.2, 0, 0, 0, 966.6, 25.2, 8.4, 15, 53.4, 187.2, 3.6, 12,
]

test('matches independently verified vendor daily totals', () => {
  assert.equal(toCalendarRainfallMm(sinauBumi, 'RAIN', 1398.6), 116.55)
  assert.equal(toCalendarRainfallMm(sinauBumi, 'RAIN', 966.6), 80.55)
  assert.equal(toCalendarRainfallMm(sinauBumi, 'RAIN', 1233), 102.75)
})

test('matches vendor October total, rainy-day count and maximum', () => {
  const depths = octoberRateSums.map(value => toCalendarRainfallMm(sinauBumi, 'RAIN', value))
  assert.deepEqual(summarizeCalendarRainfall(depths), {
    total: 305, rainyDays: 14, maximum: 116.55,
  })
})

test('does not convert other sources, loggers or rainfall quantities', () => {
  assert.equal(toCalendarRainfallMm({ sourceType: 'INTERNAL', loggerCode: 'RSUP-AWS-01' }, 'RAIN', 12), 12)
  assert.equal(toCalendarRainfallMm({ sourceType: 'VENDOR', loggerCode: 'OTHER-AWS' }, 'RAIN', 12), 12)
  assert.equal(toCalendarRainfallMm(sinauBumi, 'RAIN_TIP', 12), 12)
  assert.equal(toCalendarRainfallMm(sinauBumi, 'RAINFALL', 12), 12)
  assert.equal(toCalendarRainfallMm(null, 'RAIN', 12), 12)
})

test('recognizes suffixed RAIN keys and keeps the 0.5 mm rainy-day boundary', () => {
  assert.equal(toCalendarRainfallMm(sinauBumi, 'RAIN#5', 6), 0.5)
  assert.equal(toCalendarRainfallMm(sinauBumi, 'RAIN', 0), 0)
  const depths = [0, 3.6, 6, 8.4].map(value => toCalendarRainfallMm(sinauBumi, 'RAIN', value))
  assert.deepEqual(summarizeCalendarRainfall(depths), { total: 1.5, rainyDays: 2, maximum: 0.7 })
})

test('empty observations stay missing rather than becoming a zero maximum', () => {
  assert.deepEqual(summarizeCalendarRainfall([]), { total: 0, rainyDays: 0, maximum: null })
})
