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

test('selected logger detail is brought into the sidebar viewport', () => {
  assert.match(panelSource, /const detailRef = useRef<HTMLDivElement \| null>\(null\)/)
  assert.match(panelSource, /detailRef\.current\?\.scrollIntoView\(\{ block: 'nearest' \}\)/)
  assert.match(panelSource, /ref=\{detailRef\}/)
})

test('closed logger detail can be reopened from the visible sidebar state', () => {
  assert.match(panelSource, /Detail logger ditutup/)
  assert.match(panelSource, /Buka detail/)
  assert.match(panelSource, /state\.selectLogger\(state\.selectedLoggerId\)/)
})

test('IoT detail uses the shared ordered monitoring readings helper', () => {
  assert.match(panelSource, /getOrderedMonitoringReadings/)
  assert.match(panelSource, /priorityReadings/)
})

test('IoT panel keeps UTF-8 separators readable', () => {
  assert.doesNotMatch(panelSource, /Â/)
})
