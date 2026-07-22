// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it } from 'vitest';
import { readdirSync, readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { buildFixtures, canonicalJson } from './wire-fixtures.build.js';

const here = dirname(fileURLToPath(import.meta.url));
const fixturesDir = join(here, '..', 'wire-fixtures');

/**
 * These fixtures are consumed by the phase-3 C# tests. Set UPDATE_FIXTURES=1 to
 * rewrite them from the serializer after an intentional wire change.
 */
const shouldUpdate = process.env.UPDATE_FIXTURES === '1';

describe('wire fixtures', () => {
  it('match the committed canonical JSON (regenerate with UPDATE_FIXTURES=1)', async () => {
    const fixtures = await buildFixtures();

    if (shouldUpdate) {
      if (!existsSync(fixturesDir)) {
        mkdirSync(fixturesDir, { recursive: true });
      }
    }

    for (const [name, value] of Object.entries(fixtures)) {
      const expected = canonicalJson(value);
      const file = join(fixturesDir, `${name}.json`);

      if (shouldUpdate) {
        writeFileSync(file, expected, 'utf8');
        continue;
      }

      expect(existsSync(file), `missing fixture file: ${name}.json`).toBe(true);
      const actual = readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
      expect(actual, `fixture drift in ${name}.json`).toBe(expected);
    }
  });

  it('has no orphaned fixture files', async () => {
    if (shouldUpdate || !existsSync(fixturesDir)) {
      return;
    }
    const fixtures = await buildFixtures();
    const expectedNames = new Set(Object.keys(fixtures).map((name) => `${name}.json`));
    const onDisk = readdirSync(fixturesDir).filter((file) => file.endsWith('.json'));
    for (const file of onDisk) {
      expect(expectedNames.has(file), `orphaned fixture file: ${file}`).toBe(true);
    }
  });
});
