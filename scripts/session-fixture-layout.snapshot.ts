/** Repository-wide canonical-layout check for committed session fixtures. */

import { resolve } from 'node:path'
import { expect, it } from 'vitest'
import { inspectSessionFixtureLayouts } from './session-fixture-layout.ts'

const root = resolve(import.meta.dirname, '..')

it('keeps every session-format JSONL fixture in canonical packed layout', () => {
  const nonCanonical = inspectSessionFixtureLayouts(root)
    .filter(fixture => fixture.source !== fixture.canonical)
    .map(fixture => fixture.path)
  expect(
    nonCanonical,
    'Committed session-format JSONL fixtures must use canonical packed layout.',
  ).toEqual([])
})
