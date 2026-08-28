// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { afterEach, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { inflateSync } from 'node:zlib';
import { setNotificationSink } from '../../../ts-sdk/dist/runtime/notifications.js';
import { SampleImageContentPage } from '../dist/pages/contentPages.js';
import { SampleListPageWithDetails } from '../dist/pages/detailsPage.js';
import { SampleIconPage } from '../dist/pages/iconPage.js';
import {
  SampleListPageWithSections,
  SectionsIndexPage,
} from '../dist/pages/sectionsPages.js';
import {
  blueTilePngBase64,
  greenTilePngBase64,
  redTilePngBase64,
} from '../dist/util.js';

const originalFetch = globalThis.fetch;
const packageRoot = fileURLToPath(new URL('..', import.meta.url));

afterEach(() => {
  globalThis.fetch = originalFetch;
  setNotificationSink(null);
});

function waitForIconLoad() {
  return new Promise((resolve) => setImmediate(resolve));
}

async function waitFor(predicate) {
  for (let attempt = 0; attempt < 100; attempt += 1) {
    if (predicate()) {
      return;
    }

    await new Promise((resolve) => setTimeout(resolve, 5));
  }

  assert.fail('Timed out waiting for the image load');
}

function runIsolated(source) {
  const result = spawnSync(process.execPath, ['--input-type=module', '--eval', source], {
    cwd: packageRoot,
    encoding: 'utf8',
  });

  assert.equal(result.status, 0, result.stderr || result.stdout);
}

function decodeOnePixelPng(base64) {
  const png = Buffer.from(base64, 'base64');
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  assert.deepEqual(png.subarray(0, signature.length), signature);

  const idatChunks = [];
  let offset = signature.length;
  let width;
  let height;
  while (offset < png.length) {
    const length = png.readUInt32BE(offset);
    const type = png.toString('ascii', offset + 4, offset + 8);
    const data = png.subarray(offset + 8, offset + 8 + length);
    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      assert.equal(data[8], 8);
      assert.equal(data[9], 6);
    } else if (type === 'IDAT') {
      idatChunks.push(data);
    } else if (type === 'IEND') {
      break;
    }

    offset += length + 12;
  }

  assert.equal(width, 1);
  assert.equal(height, 1);
  const scanline = inflateSync(Buffer.concat(idatChunks));
  assert.equal(scanline[0], 0);
  return [...scanline.subarray(1, 5)];
}

test('tile image constants are distinct one-pixel solid-color PNGs', () => {
  assert.deepEqual(decodeOnePixelPng(redTilePngBase64), [220, 60, 60, 255]);
  assert.deepEqual(decodeOnePixelPng(greenTilePngBase64), [46, 160, 67, 255]);
  assert.deepEqual(decodeOnePixelPng(blueTilePngBase64), [54, 112, 204, 255]);
  assert.equal(new Set([redTilePngBase64, greenTilePngBase64, blueTilePngBase64]).size, 3);
});

test('importing and constructing sample pages performs no hero file read', () => {
  runIsolated(`
    import assert from 'node:assert/strict';
    import { rename } from 'node:fs/promises';

    const heroPath = new URL('./dist/assets/hero.png', import.meta.url);
    const missingPath = new URL('./dist/assets/hero.png.import-test', import.meta.url);
    const unhandled = [];
    process.on('unhandledRejection', (reason) => unhandled.push(reason));
    await rename(heroPath, missingPath);

    try {
      await import('./dist/index.js');
      const [{ SampleImageContentPage }, { SampleListPageWithDetails }, { SampleIconPage }, { SampleListPageWithSections }] =
        await Promise.all([
          import('./dist/pages/contentPages.js'),
          import('./dist/pages/detailsPage.js'),
          import('./dist/pages/iconPage.js'),
          import('./dist/pages/sectionsPages.js'),
        ]);

      new SampleImageContentPage();
      new SampleListPageWithDetails();
      new SampleIconPage();
      new SampleListPageWithSections();
      await new Promise((resolve) => setImmediate(resolve));

      await rename(missingPath, heroPath);
      const { getHeroImage } = await import('./dist/util.js');
      const image = await getHeroImage();
      assert.match(image.light.data, /^iVBOR/);
      assert.strictEqual(await getHeroImage(), image);
      assert.deepEqual(unhandled, []);
    } finally {
      try {
        await rename(missingPath, heroPath);
      } catch (error) {
        if (error?.code !== 'ENOENT') {
          throw error;
        }
      }
    }

    process.exit(0);
  `);
});

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
    const packagedFileItem = items.find((item) => item.title === 'Packaged file icon');

    assert.ok(Array.isArray(items));
    assert.ok(items.length > 3);
    assert.equal(urlItem?.icon?.light?.icon, '\uE774');
    await waitFor(() => packagedFileItem?.icon?.light?.data?.startsWith('iVBOR') === true);
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

test('missing hero image falls back once without an unhandled rejection', () => {
  runIsolated(`
    import assert from 'node:assert/strict';
    import { rename } from 'node:fs/promises';

    const heroPath = new URL('./dist/assets/hero.png', import.meta.url);
    const missingPath = new URL('./dist/assets/hero.png.missing-test', import.meta.url);
    const unhandled = [];
    process.on('unhandledRejection', (reason) => unhandled.push(reason));
    await rename(heroPath, missingPath);

    try {
      const { getHeroImage, heroImageFallback } = await import('./dist/util.js');
      const first = await getHeroImage();
      await rename(missingPath, heroPath);
      const second = await getHeroImage();

      assert.strictEqual(first, heroImageFallback);
      assert.strictEqual(second, first);
      assert.deepEqual(unhandled, []);
    } finally {
      try {
        await rename(missingPath, heroPath);
      } catch (error) {
        if (error?.code !== 'ENOENT') {
          throw error;
        }
      }
    }
  `);
});

test('details items return promptly and only the hero item is updated', async () => {
  const notifications = [];
  setNotificationSink((method, params) => notifications.push({ method, params }));
  const page = new SampleListPageWithDetails();

  const items = page.getItems();
  const heroItem = items.find((item) => item.title === 'This one has a hero image');
  assert.ok(Array.isArray(items));
  assert.equal(heroItem?.details?.heroImage?.light?.icon, '\uE91B');

  await Promise.resolve();
  assert.equal(notifications.length, 0);
  await waitFor(() => heroItem?.details?.heroImage?.light?.data?.startsWith('iVBOR') === true);

  assert.match(heroItem?.details?.heroImage?.light?.data, /^iVBOR/);
  assert.strictEqual(page.getItems(), items);
  assert.equal(notifications.length, 1);
  assert.equal(notifications[0]?.method, 'command/propChanged');
  assert.equal(notifications[0]?.params?.commandId, 'details-hero');
  assert.ok(notifications[0]?.params?.properties?.details);
});

test('repeated content and section images keep the full hero out of list payloads', async () => {
  const heroBase64 = await readFile(new URL('../dist/assets/hero.png', import.meta.url), 'base64');
  const content = new SampleImageContentPage().getContent();
  const sectionPages = new SectionsIndexPage().getItems().map((item) => item.command);

  assert.ok(Array.isArray(content));
  assert.ok(content.every((item) => item.type !== 'image' || item.image.light.data !== heroBase64));
  assert.deepEqual(
    sectionPages.map((page) => page.gridProperties),
    [
      null,
      { type: 'small' },
      { type: 'medium', showTitle: true },
      { type: 'gallery', showTitle: true, showSubtitle: true },
      { type: 'gallery', showTitle: false, showSubtitle: false },
    ],
  );

  for (const page of sectionPages) {
    const sectionItems = page.getItems();
    const sectionJson = JSON.stringify(sectionItems);
    assert.equal(sectionJson.includes(heroBase64), false);
    assert.ok(Buffer.byteLength(sectionJson) < 5_000);
    assert.equal(
      new Set(
        sectionItems
          .filter((item) => item.icon?.light?.data)
          .map((item) => item.icon.light.data),
      ).size,
      3,
    );
  }
});
