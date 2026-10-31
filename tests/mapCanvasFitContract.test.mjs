import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const canvasSource = fs.readFileSync(new URL('../src/features/webgis/components/map/map-canvas.tsx', import.meta.url), 'utf8')

test('map centering keeps the full logger area visible like the legacy monitoring map', () => {
  assert.match(canvasSource, /fitBounds\([\s\S]*padding: 64, maxZoom: 14, duration: 450/)
})
