import test from 'node:test'
import assert from 'node:assert/strict'
import {
  getCalendarKind, getCalendarSelection, getCalendarHistoryQuery,
  getSensorCalendar, getCalendarStatistics,
} from '../src/features/iot/lib/sensorCalendar.ts'

function device(loggerId, loggerCode, data, sourceType = 'VENDOR') {
  return {
    loggerId, loggerCode, namaLogger: loggerCode, sourceType, data,
    lokasi: null, latitude: null, longitude: null, intervalMenit: 5, aktif: true,
    status: 'ONLINE', rawId: null, lastSensorAt: null, lastReceivedAt: null, ageMinutes: null,
  }
}

const awl = device(1, 'AWLC-PKB-001', { TMAK_DISTANCE: 0, RAIN_TIP: 0 }, 'INTERNAL')
const aws = device(2, 'RSUP-AWS-01', { RAIN: 0, TEMPERATURE: 27 })
const awq = device(3, 'RSUP-AWQ-01', { PH: 7, WATER_TEMPERATURE: 27 })
const lpa = device(4, 'RSUP-LPA-01', { LPA_TMAT: -30, LPA_RAIN_HOUR: 0 })

function history(loggerId, aggregationMode, data, from = '2026-01-01T00:00:00') {
  return {
    loggerId, aggregationMode, from, to: '2027-01-01T00:00:00', period: 'month', aggregation: 'day',
    total: data.length, rawTotal: data.length,
    data: data.map((reading, i) => ({ rawId: i + 1, loggerId, receivedAt: reading.ts, ...reading })),
  }
}

test('filters by actual parameter support, keeps zero and excludes AWQ', () => {
  const devices = [awq, awl, aws, lpa]
  assert.deepEqual(getCalendarSelection(devices, 'tmak', 3).devices.map(d => d.loggerId), [1])
  assert.deepEqual(getCalendarSelection(devices, 'tmat', 3).devices.map(d => d.loggerId), [4])
  assert.deepEqual(getCalendarSelection(devices, 'rain', 3).devices.map(d => d.loggerId), [1, 2, 4])
})

test('preserves compatible selection and falls back when switching kind', () => {
  const devices = [awq, awl, aws, lpa]
  assert.equal(getCalendarSelection(devices, 'rain', 2).selectedDevice.loggerId, 2)
  assert.equal(getCalendarSelection(devices, 'tmat', 2).selectedDevice.loggerId, 4)
  assert.equal(getCalendarSelection(devices, 'tmak', 4).selectedDevice.loggerId, 1)
  assert.deepEqual(getCalendarSelection([awq], 'rain', 3), { devices: [], selectedDevice: null })
})

test('recognizes aliases and suffixed keys without accepting unrelated names', () => {
  const devices = [
    device(5, 'OFFLINE-LPA', { 'LPA_TMAT#26': null }),
    device(6, 'CHANNEL', { TMAK_CM: 0 }),
    device(7, 'NOT-RAIN', { DRAINAGE: 2, RAINY_DAY: 1 }),
  ]
  assert.deepEqual(getCalendarSelection(devices, 'tmat', 0).devices.map(d => d.loggerId), [5])
  assert.deepEqual(getCalendarSelection(devices, 'tmak', 0).devices.map(d => d.loggerId), [6])
  assert.equal(getCalendarSelection(devices, 'rain', 0).devices.length, 0)
  assert.equal(getCalendarKind(null), 'rain')
  assert.equal(getCalendarKind('unknown'), 'rain')
  assert.equal(getCalendarKind('tmat'), 'tmat')
})

test('requests daily averages for levels and daily sums for rainfall', () => {
  for (const kind of ['tmak', 'tmat']) {
    assert.deepEqual(getCalendarHistoryQuery(kind, 2026), {
      from: '2026-01-01T00:00', to: '2027-01-01T00:00', aggregationMode: 'average',
    })
  }
  assert.equal(getCalendarHistoryQuery('rain', 2026).aggregationMode, 'sum')
})

test('displays TMAK daily averages with the AWL correction below -60 cm, preserving gaps', () => {
  const response = history(1, 'average', [
    { ts: '2026-10-01T00:00:00', data: { TMAK_DISTANCE: -70, RAIN_TIP: 200 } },
    { ts: '2026-10-02T00:00:00', data: { TMAK_DISTANCE: 0 } },
    { ts: '2026-10-03T00:00:00', data: { TMAK_DISTANCE: null } },
    { ts: '2026-10-04T00:00:00', data: { TMAK_DISTANCE: '' } },
  ])
  assert.deepEqual([...getSensorCalendar(response, awl, 'tmak', 2026)], [
    ['2026-10-01', -60], ['2026-10-02', 0],
  ])
  assert.equal(response.data[0].data.TMAK_DISTANCE, -70)
})

test('keeps negative and zero TMAT values and summarizes averages without summing levels', () => {
  const response = history(4, 'average', [
    { ts: '2026-10-01T00:00:00', data: { LPA_TMAT: -40, LPA_RAIN_HOUR: 90 } },
    { ts: '2026-10-02T00:00:00', data: { 'LPA_TMAT#26': '0' } },
    { ts: '2026-10-03T00:00:00', data: { LPA_TMAT: -20 } },
  ])
  const calendar = getSensorCalendar(response, lpa, 'tmat', 2026)
  assert.deepEqual([...calendar.values()], [-40, 0, -20])
  assert.deepEqual(getCalendarStatistics(calendar.values()), { count: 3, minimum: -40, maximum: 0, average: -20 })
  assert.deepEqual(getCalendarStatistics([]), { count: 0, minimum: null, maximum: null, average: null })
})

test('keeps the verified vendor rainfall conversion and does not convert RAIN_TIP', () => {
  const response = history(2, 'sum', [{ ts: '2026-10-10T00:00:00', data: { RAIN: 1398.6 } }])
  assert.equal(getSensorCalendar(response, aws, 'rain', 2026).get('2026-10-10'), 116.55)
  const internal = history(1, 'sum', [{ ts: '2026-10-10T00:00:00', data: { RAIN_TIP: 12, TMAK_DISTANCE: -50 } }])
  assert.equal(getSensorCalendar(internal, awl, 'rain', 2026).get('2026-10-10'), 12)
})

test('rejects stale logger, year or aggregation data when filters change', () => {
  const response = history(1, 'sum', [{ ts: '2026-10-10T00:00:00', data: { RAIN_TIP: 12 } }])
  assert.equal(getSensorCalendar(response, aws, 'rain', 2026).size, 0)
  assert.equal(getSensorCalendar(response, awl, 'tmak', 2026).size, 0)
  assert.equal(getSensorCalendar(response, awl, 'rain', 2025).size, 0)
  assert.equal(getSensorCalendar(null, awl, 'rain', 2026).size, 0)
})
