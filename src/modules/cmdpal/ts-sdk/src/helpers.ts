/**
 * Helper functions for creating icon data from various sources.
 */

import * as fs from 'fs';
import * as path from 'path';
import type { IconInfo, IconData } from './types';

/**
 * Creates an IconInfo from a base64-encoded image string.
 * The base64 string should be the raw base64 data (without data URI prefix).
 */
export function iconFromBase64(base64Data: string): IconInfo {
  const iconData: IconData = { data: base64Data };
  return { light: iconData, dark: iconData };
}

/**
 * Creates an IconInfo from a font glyph character (e.g., '\uE91B' for Segoe MDL2/Fluent Icons).
 */
export function iconFromGlyph(glyph: string): IconInfo {
  const iconData: IconData = { icon: glyph };
  return { light: iconData, dark: iconData };
}

/**
 * Creates an IconInfo by reading a local file and encoding it as base64.
 * Supports common image formats: PNG, JPEG, BMP, GIF, ICO.
 */
export async function iconFromFile(filePath: string): Promise<IconInfo> {
  const absolutePath = path.resolve(filePath);
  const buffer = await fs.promises.readFile(absolutePath);
  const base64Data = buffer.toString('base64');
  return iconFromBase64(base64Data);
}

/**
 * Creates an IconInfo by fetching an image from a URL and encoding it as base64.
 * Uses Node.js built-in fetch (Node 18+).
 */
export async function iconFromUrl(url: string): Promise<IconInfo> {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`Failed to fetch icon from ${url}: ${response.status} ${response.statusText}`);
  }
  const arrayBuffer = await response.arrayBuffer();
  const base64Data = Buffer.from(arrayBuffer).toString('base64');
  return iconFromBase64(base64Data);
}
