import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const stylesheet = fs.readFileSync(new URL('../src/features/ecs/pages/WadSensorOverviewPage.module.css', import.meta.url), 'utf8')

test('uses a two-column sensor card grid on wide screens', () => {
  const sensorGrid = stylesheet.match(/\.sensorGrid\s*\{[^}]*\}/s)?.[0] ?? ''

  assert.match(sensorGrid, /grid-template-columns:\s*repeat\(2,\s*minmax\(0,\s*1fr\)\)/)
})
