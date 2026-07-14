/**
 * Helper functions for creating icon data from various sources.
 */
import type { IconInfo } from './types';
/**
 * Creates an IconInfo from a base64-encoded image string.
 * The base64 string should be the raw base64 data (without data URI prefix).
 */
export declare function iconFromBase64(base64Data: string): IconInfo;
/**
 * Creates an IconInfo from a font glyph character (e.g., '\uE91B' for Segoe MDL2/Fluent Icons).
 */
export declare function iconFromGlyph(glyph: string): IconInfo;
/**
 * Creates an IconInfo by reading a local file and encoding it as base64.
 * Supports common image formats: PNG, JPEG, BMP, GIF, ICO.
 */
export declare function iconFromFile(filePath: string): Promise<IconInfo>;
/**
 * Creates an IconInfo by fetching an image from a URL and encoding it as base64.
 * Uses Node.js built-in fetch (Node 18+).
 */
export declare function iconFromUrl(url: string): Promise<IconInfo>;
//# sourceMappingURL=helpers.d.ts.map