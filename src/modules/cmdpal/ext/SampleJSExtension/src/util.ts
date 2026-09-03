// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { iconFromGlyph } from '@microsoft/cmdpal-sdk';
import type { IconInfo, OptionalColor, Tag } from '@microsoft/cmdpal-sdk';

/** A tiny PNG used when a sample needs inline base64 without a file or network request. */
export const samplePngBase64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=';

/** Builds an {@link IconInfo} from a font glyph or Unicode character. */
export function glyphIcon(glyph: string): IconInfo {
  return iconFromGlyph(glyph);
}

/** Builds an opaque {@link OptionalColor} from red, green, and blue channels. */
export function rgb(r: number, g: number, b: number): OptionalColor {
  return { hasValue: true, color: { r, g, b, a: 255 } };
}

/** Builds a random opaque {@link OptionalColor}. */
export function randomColor(): OptionalColor {
  const channel = (): number => Math.floor(Math.random() * 256);
  return rgb(channel(), channel(), channel());
}

/** Builds a simple text {@link Tag}. */
export function tag(text: string): Tag {
  return { text };
}
