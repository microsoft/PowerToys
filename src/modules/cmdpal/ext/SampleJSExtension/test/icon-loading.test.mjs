// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import assert from 'node:assert/strict';
import { rename } from 'node:fs/promises';
import { afterEach, test } from 'node:test';
import { SampleListPageWithDetails } from '../dist/pages/detailsPage.js';
import { SampleIconPage } from '../dist/pages/iconPage.js';

const originalFetch = globalThis.fetch;

afterEach(() => {
  globalThis.fetch = originalFetch;
});

function waitForIconLoad() {
  return new Promise((resolve) => setImmediate(resolve));
}

test('constructing the icon page does not fetch or reject', async () => {
  let fetchCount = 0;
  const unhandled = [];
  const onUnhandled = (reason) => unhandled.push(reason);
  process.on('unhandledRejection', onUnhandled);
  globalThis.fetch = async () => {
    fetchCount += 1;
    throw new Error('offline');
  };

  try {
    new SampleIconPage();
    await waitForIconLoad();

    assert.equal(fetchCount, 0);
    assert.deepEqual(unhandled, []);
  } finally {
    process.off('unhandledRejection', onUnhandled);
  }
});

test('requesting icon items returns locally and caches an offline fallback', async () => {
  let fetchCount = 0;
  const unhandled = [];
  const onUnhandled = (reason) => unhandled.push(reason);
  process.on('unhandledRejection', onUnhandled);
  globalThis.fetch = async () => {
    fetchCount += 1;
    throw new Error('offline');
  };
  const page = new SampleIconPage();

  try {
    const items = page.getItems();
    const urlItem = items.find((item) => item.title === 'First-party URL icon');

    assert.ok(Array.isArray(items));
    assert.ok(items.length > 3);
    assert.equal(urlItem?.icon?.light?.icon, '\uE774');
    await waitForIconLoad();
    assert.equal(fetchCount, 1);
    assert.equal(urlItem?.icon?.light?.icon, '\uE774');

    page.getItems();
    await waitForIconLoad();
    assert.equal(fetchCount, 1);
    assert.deepEqual(unhandled, []);
  } finally {
    process.off('unhandledRejection', onUnhandled);
  }
});

test('missing hero image falls back and is loaded only once', async () => {
  const heroPath = new URL('../dist/assets/hero.png', import.meta.url);
  const missingPath = new URL('../dist/assets/hero.png.missing-test', import.meta.url);
  await rename(heroPath, missingPath);

  try {
    const page = new SampleListPageWithDetails();
    const firstItems = await page.getItems();
    await rename(missingPath, heroPath);
    const secondItems = await page.getItems();
    const firstHero = firstItems.find((item) => item.title === 'This one has a hero image');
    const secondHero = secondItems.find((item) => item.title === 'This one has a hero image');

    assert.equal(firstHero?.details?.heroImage?.light?.icon, '\uE91B');
    assert.equal(secondHero?.details?.heroImage?.light?.icon, '\uE91B');
  } finally {
    try {
      await rename(missingPath, heroPath);
    } catch (error) {
      if (error?.code !== 'ENOENT') {
        throw error;
      }
    }
  }
});
