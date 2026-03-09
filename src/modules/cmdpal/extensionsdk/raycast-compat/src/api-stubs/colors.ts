// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast Color enum compatibility stub.
 *
 * Maps Raycast's named color constants to hex color strings.
 * These colors approximate Raycast's design tokens. When rendered in
 * CmdPal's UI layer, they'll be used as-is or mapped to the closest
 * CmdPal theme color.
 */

export const Color = {
  Blue: '#007AFF',
  Green: '#28CD41',
  Magenta: '#FF2D55',
  Orange: '#FF9500',
  Purple: '#AF52DE',
  Red: '#FF3B30',
  Yellow: '#FFCC00',

  // Primary colors that Raycast supports
  PrimaryText: '#000000',
  SecondaryText: '#8E8E93',

} as const;

export type ColorEnum = typeof Color;
export type ColorKey = keyof ColorEnum;

/**
 * Raycast's Color.Dynamic — adapts to light/dark appearance.
 * For the spike, we return the light value.
 */
export function ColorDynamic(light: string, dark: string): string {
  // TODO: When CmdPal provides theme context, switch based on appearance
  void dark;
  return light;
}

/**
 * Resolve a Raycast color reference to a hex string.
 * Handles Color enum values, hex strings, and Color.Dynamic objects.
 */
export function resolveColor(color: unknown): string | undefined {
  if (typeof color === 'string') return color;
  if (color && typeof color === 'object') {
    if ('light' in color && 'dark' in color) {
      return (color as { light: string }).light;
    }
    if ('adjustContrast' in color || 'value' in color) {
      return (color as { value?: string }).value;
    }
  }
  return undefined;
}
