// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { iconFromGlyph } from '@microsoft/cmdpal-sdk';
import type { IconInfo, OptionalColor, Tag } from '@microsoft/cmdpal-sdk';

/**
 * Builds an {@link IconInfo} from a glyph, file path, or URL string.
 *
 * The Command Palette host resolves the string the same way the C# toolkit's
 * `new IconInfo(string)` does, so a Segoe Fluent glyph, an absolute file path,
 * and an https URL all work through the same helper.
 */
export function icon(value: string): IconInfo {
  return iconFromGlyph(value);
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
