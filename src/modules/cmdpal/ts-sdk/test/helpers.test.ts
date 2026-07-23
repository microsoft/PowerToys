// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { iconFromBase64, iconFromFile, iconFromGlyph, iconFromUrl } from '../src/helpers.js';

describe('iconFromGlyph', () => {
  it('places the glyph on both themes', () => {
    const icon = iconFromGlyph('\uE91B');
    expect(icon).toEqual({ light: { icon: '\uE91B' }, dark: { icon: '\uE91B' } });
  });
});

describe('iconFromBase64', () => {
  it('places the base64 data on both themes', () => {
    const icon = iconFromBase64('AAAA');
    expect(icon).toEqual({ light: { data: 'AAAA' }, dark: { data: 'AAAA' } });
  });
});

describe('iconFromFile', () => {
  it('reads a file and encodes its bytes as base64', async () => {
    const dir = await mkdtemp(join(tmpdir(), 'cmdpal-sdk-'));
    try {
      const filePath = join(dir, 'icon.bin');
      const bytes = Buffer.from([0x00, 0x01, 0x02, 0xff, 0xfe]);
      await writeFile(filePath, bytes);

      const icon = await iconFromFile(filePath);
      expect(icon).toEqual({
        light: { data: bytes.toString('base64') },
        dark: { data: bytes.toString('base64') },
      });
    } finally {
      await rm(dir, { recursive: true, force: true });
    }
  });
});

describe('iconFromUrl', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('downloads an image and encodes its bytes as base64', async () => {
    const bytes = new Uint8Array([1, 2, 3, 4, 5]);
    vi.stubGlobal(
      'fetch',
      vi.fn(() => Promise.resolve(new Response(bytes, { status: 200 }))),
    );

    const icon = await iconFromUrl('https://example.com/icon.png');
    const expected = Buffer.from(bytes).toString('base64');
    expect(icon).toEqual({ light: { data: expected }, dark: { data: expected } });
  });

  it('throws when the response is not ok', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(new Response('missing', { status: 404, statusText: 'Not Found' })),
      ),
    );

    await expect(iconFromUrl('https://example.com/missing.png')).rejects.toThrow(/404/);
  });
});
