import test from 'node:test'
import assert from 'node:assert/strict'

import { getBreadcrumbBackPath } from '../src/components/layout/breadcrumbNavigation.ts'

test('calendar breadcrumb always returns to the ECS landing page', () => {
  assert.equal(getBreadcrumbBackPath('/ecs/kalender-curah-hujan'), '/ecs')
})

test('other breadcrumb routes keep the browser-history fallback', () => {
  assert.equal(getBreadcrumbBackPath('/ecs/stasiun-monitoring'), null)
})
