import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const rules = fs.readFileSync(new URL('../src/AuthUtility.Api/Wad/WadMonitoringRules.cs', import.meta.url), 'utf8')
const controller = fs.readFileSync(new URL('../src/AuthUtility.Api/Controllers/WadMonitoringController.cs', import.meta.url), 'utf8')

test('backend exposes latest-only filter and ranks one row per monitoring point', () => {
  assert.match(controller, /bool isLatestOnly = false/)
  assert.match(controller, /hasPos,\s*isLatestOnly\)/)
  assert.match(rules, /ROW_NUMBER\(\) OVER/i)
  assert.match(rules, /var partitionBy = source\.RegionColumn is null/)
  assert.match(rules, /source\.PositionColumn/)
  assert.match(rules, /WHERE RowNumber = 1/i)
})
