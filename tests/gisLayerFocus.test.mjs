import test from 'node:test'
import assert from 'node:assert/strict'

import { findPreferredGisLayer } from '../src/features/webgis/lib/gis/layer-focus.ts'

function layer(id, { name = '', groupName = null, description = '' } = {}) {
  return {
    id,
    name,
    groupName,
    description,
    bounds: [100, -2, 101, -1],
    isVisible: false,
    sortOrder: 0,
    defaultVisible: false,
    updatedAt: '',
    layerType: 'VECTOR',
    geometryType: 'Polygon',
    featureCount: 0,
    style: {},
  }
}

test('selects the RSUP plantation division when target terms span layer metadata', () => {
  const generic = layer('generic', { name: 'Batas wilayah perkebunan' })
  const target = layer('rsup', {
    name: 'Divisi Perkebunan',
    groupName: 'PT Riau Sakti United Plantations',
    description: 'Layer operasional utama',
  })

  assert.equal(findPreferredGisLayer([generic, target]), target)
})

test('returns null when the catalog has no RSUP plantation match', () => {
  assert.equal(findPreferredGisLayer([
    layer('other', { name: 'Jaringan jalan', groupName: 'Infrastruktur' }),
  ]), null)
})

test('keeps original catalog order for equally strong matches', () => {
  const first = layer('first', { name: 'PT Riau Sakti United Plantations Divisi Perkebunan' })
  const second = layer('second', { name: 'PT Riau Sakti United Plantations Divisi Perkebunan' })

  assert.equal(findPreferredGisLayer([first, second]), first)
})
