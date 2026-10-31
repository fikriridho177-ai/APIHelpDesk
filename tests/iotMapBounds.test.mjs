import test from 'node:test'
import assert from 'node:assert/strict'

import { getIoTMapBounds } from '../src/features/webgis/lib/iot-map.ts'

const device = (latitude, longitude, loggerId = 1) => ({
  loggerId,
  loggerCode: `IOT-${loggerId}`,
  namaLogger: `Logger ${loggerId}`,
  sourceType: 'INTERNAL',
  lokasi: null,
  latitude,
  longitude,
  intervalMenit: 5,
  aktif: true,
  status: 'ONLINE',
  rawId: null,
  lastSensorAt: null,
  lastReceivedAt: null,
  ageMinutes: null,
  data: {},
})

test('returns null for an empty device list', () => {
  assert.equal(getIoTMapBounds([]), null)
})

test('returns null when all devices have invalid coordinates', () => {
  assert.equal(getIoTMapBounds([
    device(null, 106.8),
    device(-91, 106.9, 2),
    device(6.2, Number.NaN, 3),
  ]), null)
})

test('adds a minimum extent around one logger', () => {
  assert.deepEqual(getIoTMapBounds([device(-6.2, 106.8)]), [106.795, -6.205, 106.805, -6.195])
})

test('returns the enclosing bounds for multiple loggers', () => {
  assert.deepEqual(getIoTMapBounds([
    device(-6.2, 106.8),
    device(-6.4, 107.1, 2),
    device(-6.1, 106.7, 3),
  ]), [106.7, -6.4, 107.1, -6.1])
})

test('ignores out-of-range coordinates', () => {
  assert.deepEqual(getIoTMapBounds([
    device(-6.2, 106.8),
    device(91, 200, 2),
  ]), [106.795, -6.205, 106.805, -6.195])
})

test('preserves the minimum extent at geographic boundaries', () => {
  assert.deepEqual(getIoTMapBounds([device(90, 180)]), [179.99, 89.99, 180, 90])
  assert.deepEqual(getIoTMapBounds([device(-90, -180)]), [-180, -90, -179.99, -89.99])
})
