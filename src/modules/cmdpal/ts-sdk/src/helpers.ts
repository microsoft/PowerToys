// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Helpers for building {@link IconInfo} values from the sources an extension
 * commonly has on hand: font glyphs, base64 image bytes, URLs, and local files.
 */

import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import type { IconData, IconInfo } from './types.js';

/**
 * Creates an {@link IconInfo} from a font glyph character, for example
 * `'\uE91B'` from Segoe Fluent Icons or Segoe MDL2 Assets.
 */
export function iconFromGlyph(glyph: string): IconInfo {
  const iconData: IconData = { icon: glyph };
  return { light: iconData, dark: iconData };
}

/**
 * Creates an {@link IconInfo} from base64-encoded image bytes. The value may be
 * raw base64 (for example `'iVBORw0KGgo...'`) or a full data URI.
 */
export function iconFromBase64(base64Data: string): IconInfo {
  const iconData: IconData = { data: base64Data };
  return { light: iconData, dark: iconData };
}

/**
 * Creates an {@link IconInfo} by reading a local image file and encoding its
 * bytes as base64. Supports any raster format the host can decode, such as PNG,
 * JPEG, BMP, GIF, and ICO.
 */
export async function iconFromFile(filePath: string): Promise<IconInfo> {
  const buffer = await readFile(resolve(filePath));
  return iconFromBase64(buffer.toString('base64'));
}

/**
 * Creates an {@link IconInfo} by downloading an image from a URL and encoding
 * its bytes as base64. Uses the Node.js global `fetch`.
 *
 * @throws Error when the request does not complete with a successful status.
 */
export async function iconFromUrl(url: string): Promise<IconInfo> {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(
      `Failed to fetch icon from ${url}: ${String(response.status)} ${response.statusText}`,
    );
  }
  const bytes = new Uint8Array(await response.arrayBuffer());
  return iconFromBase64(Buffer.from(bytes).toString('base64'));
}
