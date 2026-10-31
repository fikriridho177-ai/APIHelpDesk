import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const panelSource = fs.readFileSync(new URL('../src/features/webgis/components/IoTMonitoringPanel.tsx', import.meta.url), 'utf8')

test('IoT panel exposes controlled accordion props and accessible toggle markup', () => {
  assert.match(panelSource, /expanded: boolean/)
  assert.match(panelSource, /onToggle: \(\) => void/)
  assert.match(panelSource, /aria-expanded=\{expanded\}/)
  assert.match(panelSource, /aria-controls="iot-monitoring-content"/)
})

test('IoT detail uses the shared ordered monitoring readings helper', () => {
  assert.match(panelSource, /getOrderedMonitoringReadings/)
  assert.match(panelSource, /priorityReadings/)
})
