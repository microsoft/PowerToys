// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { iconFromFile, iconFromGlyph } from '@microsoft/cmdpal-sdk';
import type { IconInfo, OptionalColor, Tag } from '@microsoft/cmdpal-sdk';
import { fileURLToPath } from 'node:url';

/** A tiny PNG used when a sample needs inline base64 without a file or network request. */
export const samplePngBase64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=';

/** One-pixel solid red PNG used for repeated sample tiles. */
export const redTilePngBase64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mO4Y2PzHwAFoAJUbJbXZAAAAABJRU5ErkJggg==';

/** One-pixel solid green PNG used for repeated sample tiles. */
export const greenTilePngBase64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mPQW+D8HwAEIgIRTAgebgAAAABJRU5ErkJggg==';

/** One-pixel solid blue PNG used for repeated sample tiles. */
export const blueTilePngBase64 =
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mMwKzjzHwAExAJy0S1RegAAAABJRU5ErkJggg==';

/** Fallback used when the bundled hero image cannot be read. */
export const heroImageFallback = iconFromGlyph('\uE91B');

const heroImagePath = fileURLToPath(new URL('./assets/hero.png', import.meta.url));
let heroImageLoad: Promise<IconInfo> | undefined;

/** Loads the bundled hero once and keeps a usable fallback when the file is unavailable. */
export function getHeroImage(): Promise<IconInfo> {
  return (heroImageLoad ??= Promise.resolve()
    .then(() => iconFromFile(heroImagePath))
    .catch(() => heroImageFallback));
}

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
