"use strict";
/**
 * Helper functions for creating icon data from various sources.
 */
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.iconFromBase64 = iconFromBase64;
exports.iconFromGlyph = iconFromGlyph;
exports.iconFromFile = iconFromFile;
exports.iconFromUrl = iconFromUrl;
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
/**
 * Creates an IconInfo from a base64-encoded image string.
 * The base64 string should be the raw base64 data (without data URI prefix).
 */
function iconFromBase64(base64Data) {
    const iconData = { data: base64Data };
    return { light: iconData, dark: iconData };
}
/**
 * Creates an IconInfo from a font glyph character (e.g., '\uE91B' for Segoe MDL2/Fluent Icons).
 */
function iconFromGlyph(glyph) {
    const iconData = { icon: glyph };
    return { light: iconData, dark: iconData };
}
/**
 * Creates an IconInfo by reading a local file and encoding it as base64.
 * Supports common image formats: PNG, JPEG, BMP, GIF, ICO.
 */
async function iconFromFile(filePath) {
    const absolutePath = path.resolve(filePath);
    const buffer = await fs.promises.readFile(absolutePath);
    const base64Data = buffer.toString('base64');
    return iconFromBase64(base64Data);
}
/**
 * Creates an IconInfo by fetching an image from a URL and encoding it as base64.
 * Uses Node.js built-in fetch (Node 18+).
 */
async function iconFromUrl(url) {
    const response = await fetch(url);
    if (!response.ok) {
        throw new Error(`Failed to fetch icon from ${url}: ${response.status} ${response.statusText}`);
    }
    const arrayBuffer = await response.arrayBuffer();
    const base64Data = Buffer.from(arrayBuffer).toString('base64');
    return iconFromBase64(base64Data);
}
//# sourceMappingURL=helpers.js.map