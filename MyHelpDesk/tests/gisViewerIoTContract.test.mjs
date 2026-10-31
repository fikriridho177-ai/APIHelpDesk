import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'

const viewerSource = fs.readFileSync(new URL('../src/features/webgis/components/map/gis-viewer.tsx', import.meta.url), 'utf8')

test('GISViewer controls a closed IoT accordion and opens it from shared selection', () => {
  assert.match(viewerSource, /const \[iotPanelOpen, setIotPanelOpen\] = useState\(false\)/)
  assert.match(viewerSource, /setIotPanelOpen\(true\)/)
  assert.match(viewerSource, /expanded=\{iotPanelOpen\}/)
  assert.match(viewerSource, /onToggle=\{toggleIotPanel\}/)
})

test('logger selection opens detail without moving the map', () => {
  const selectionStart = viewerSource.indexOf('const selectIoTLogger')
  const selectionEnd = viewerSource.indexOf('const toggleIotPanel', selectionStart)
  assert.ok(selectionStart >= 0 && selectionEnd > selectionStart)
  const selectionSource = viewerSource.slice(selectionStart, selectionEnd)
  assert.doesNotMatch(selectionSource, /setFit|getIoTMapBounds/)
})

test('GISViewer gives explicit URL selection precedence over the preferred RSUP layer', () => {
  assert.match(viewerSource, /findPreferredGisLayer/)
  assert.match(viewerSource, /const preferredLayer = selected \? null : findPreferredGisLayer\(catalog\.layers\)/)
  assert.match(viewerSource, /overrides\[preferredLayer\.id\] === undefined/)
  assert.match(viewerSource, /overrides\[preferredLayer\.id\] !== false/)
})
