import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const canvasSource = fs.readFileSync(new URL('../src/features/webgis/components/map/map-canvas.tsx', import.meta.url), 'utf8')
const styleSource = fs.readFileSync(new URL('../src/features/webgis/webgis.css', import.meta.url), 'utf8')

test('IoT markers keep valid-device filtering and add a safe identity label', () => {
  assert.match(canvasSource, /filter\(isMappableDevice\)/)
  assert.match(canvasSource, /gis-iot-marker-wrap/)
  assert.match(canvasSource, /label\.textContent = identity/)
  assert.match(canvasSource, /label\.setAttribute\("aria-hidden", "true"\)/)
  assert.match(canvasSource, /onSelectIoTLogger\?\./)
})

test('IoT marker labels are explicitly unrotated and bounded by CSS', () => {
  assert.match(styleSource, /gis-iot-marker-wrap/)
  assert.match(styleSource, /gis-iot-marker-label/)
  assert.match(styleSource, /white-space: nowrap/)
  assert.match(styleSource, /pointer-events: none/)
})
