import test from 'node:test'
import assert from 'node:assert/strict'

import { copyAwsRainfallToAwl } from '../src/features/iot/lib/rainfallSource.ts'

function monitoringResponse() {
  return {
    total: 2,
    online: 2,
    stale: 0,
    noRecentData: 0,
    inactive: 0,
    devices: [
      {
        loggerId: 1,
        loggerCode: 'AWLC-PKB-001',
        namaLogger: 'AWL-C001',
        sourceType: 'INTERNAL',
        lokasi: null,
        latitude: null,
        longitude: null,
        intervalMenit: 5,
        aktif: true,
        status: 'ONLINE',
        rawId: 10,
        lastSensorAt: null,
        lastReceivedAt: null,
        ageMinutes: 0,
        data: { RAIN_TIP: 4, TMAK_DISTANCE: -50 },
      },
      {
        loggerId: 2,
        loggerCode: 'RSUP-AWS-01',
        namaLogger: 'AWS RSUP-1 (Cuaca PT RSUP, Riau)',
        sourceType: 'VENDOR',
        lokasi: null,
        latitude: null,
        longitude: null,
        intervalMenit: 5,
        aktif: true,
        status: 'ONLINE',
        rawId: 11,
        lastSensorAt: null,
        lastReceivedAt: null,
        ageMinutes: 0,
        data: { RAIN: 12.5, WIND_SPEED: 0.2 },
      },
    ],
  }
}

test('copies AWS rainfall to AWL-C001 without changing TMAK', () => {
  const response = monitoringResponse()
  const mapped = copyAwsRainfallToAwl(response)
  const awl = mapped.devices[0]

  assert.equal(awl.data.RAIN_TIP, 12.5)
  assert.equal(awl.data.TMAK_DISTANCE, -50)
  assert.equal(response.devices[0].data.RAIN_TIP, 4)
  assert.equal(response.devices[0].data.TMAK_DISTANCE, -50)
})

test('does not change the response when AWS has no rainfall value', () => {
  const response = monitoringResponse()
  response.devices[1].data.RAIN = null

  assert.deepEqual(copyAwsRainfallToAwl(response), response)
})

test('uses another vendor AWS logger when the first one has no rainfall value', () => {
  const response = monitoringResponse()
  response.devices.splice(1, 0, {
    ...response.devices[1],
    loggerId: 3,
    loggerCode: 'RSUP-AWS-EMPTY',
    data: { RAIN: null },
  })

  assert.equal(copyAwsRainfallToAwl(response).devices[0].data.RAIN_TIP, 12.5)
})

test('uses only a vendor AWS logger as the rainfall source', () => {
  const response = monitoringResponse()
  response.devices.splice(1, 1, {
    ...response.devices[1],
    loggerCode: 'AWS-LOCAL',
    namaLogger: 'AWS local bridge',
    sourceType: 'INTERNAL',
    data: { RAIN: 99 },
  })

  assert.equal(copyAwsRainfallToAwl(response), response)
})

test('does not treat a similarly named parameter as rainfall', () => {
  const response = monitoringResponse()
  response.devices[1].data = { RAINY_DAY: 99, RAIN: 12.5 }

  assert.equal(copyAwsRainfallToAwl(response).devices[0].data.RAIN_TIP, 12.5)
})

test('clears the mapped rainfall error while preserving other errors', () => {
  const response = monitoringResponse()
  response.devices[0].errors = {
    RAIN_TIP: 'NO_RESULT',
    TMAK_DISTANCE: 'BAD_DATA',
  }

  assert.deepEqual(copyAwsRainfallToAwl(response).devices[0].errors, {
    TMAK_DISTANCE: 'BAD_DATA',
  })
})
