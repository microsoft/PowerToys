"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.GalleryGridLayout = exports.MediumGridLayout = exports.SmallGridLayout = exports.IconInfo = void 0;
exports.color = color;
exports.noColor = noColor;
exports.tag = tag;
exports.details = details;
exports.metadataTags = metadataTags;
exports.metadataLink = metadataLink;
exports.metadataSeparator = metadataSeparator;
exports.contextItem = contextItem;
exports.sectionHeader = sectionHeader;
const types_1 = require("../generated/types");
// ---------------------------------------------------------------------------
// Color helpers
// ---------------------------------------------------------------------------
/**
 * Create a visible color from RGBA components.
 * @example color(59, 130, 246) // blue
 * @example color(255, 0, 0, 128) // semi-transparent red
 */
function color(r, g, b, a = 255) {
    return { hasValue: true, color: { r, g, b, a } };
}
/** Sentinel for "no color" — property won't apply. */
function noColor() {
    return { hasValue: false, color: { r: 0, g: 0, b: 0, a: 0 } };
}
// ---------------------------------------------------------------------------
// Icon helpers
// ---------------------------------------------------------------------------
/** Convenience overloads for building IIconInfo. */
exports.IconInfo = {
    /**
     * Create an icon from a Segoe MDL2 glyph or emoji.
     * Uses the same glyph for both light and dark themes.
     * @example IconInfo.fromGlyph('\uE8C8')
     * @example IconInfo.fromGlyph('🍓')
     */
    fromGlyph(glyph) {
        return { light: { icon: glyph }, dark: { icon: glyph } };
    },
    /**
     * Create an icon from an emoji character.
     * Alias for fromGlyph — emoji works the same as glyphs.
     * @example IconInfo.fromEmoji('🌅')
     */
    fromEmoji(emoji) {
        return { light: { icon: emoji }, dark: { icon: emoji } };
    },
    /**
     * Create an icon with separate light/dark glyphs.
     * @example IconInfo.fromGlyphs('\uE8C8', '\uE8C9')
     */
    fromGlyphs(lightGlyph, darkGlyph) {
        return { light: { icon: lightGlyph }, dark: { icon: darkGlyph } };
    },
    /** Empty icon (no visual). */
    empty() {
        return { light: { icon: '' }, dark: { icon: '' } };
    },
};
/**
 * Create a tag with optional styling.
 * @example tag({ text: 'Beta' })
 * @example tag({ text: 'v1.0', foreground: color(59, 130, 246) })
 */
function tag(options) {
    if (typeof options === 'string') {
        return {
            text: options,
            icon: exports.IconInfo.empty(),
            toolTip: options,
            foreground: noColor(),
            background: noColor(),
        };
    }
    return {
        text: options.text,
        icon: options.icon ?? exports.IconInfo.empty(),
        toolTip: options.toolTip ?? options.text,
        foreground: options.foreground ?? noColor(),
        background: options.background ?? noColor(),
    };
}
/**
 * Build a details panel descriptor.
 * @example details({ title: 'About', body: 'Description text' })
 */
function details(options) {
    return {
        title: options.title,
        body: options.body,
        heroImage: options.heroImage,
        metadata: options.metadata,
        size: options.size,
    };
}
// ---------------------------------------------------------------------------
// Details metadata element helpers
// ---------------------------------------------------------------------------
/** A metadata row with tag pills. */
function metadataTags(key, tags, icon) {
    const data = { tags };
    return { key, icon, data };
}
/** A metadata row with a link. */
function metadataLink(key, text, url, icon) {
    const data = { text, url };
    return { key, icon, data };
}
/** A metadata separator line. */
function metadataSeparator() {
    const data = { _type: types_1.DetailsDataType.Separator };
    return { key: '', data };
}
/**
 * Build a context menu item.
 * @example contextItem({ title: 'Copy', icon: IconInfo.fromGlyph('\uE8C8'), command: myCopyCmd })
 */
function contextItem(options) {
    return {
        title: options.title,
        subtitle: options.subtitle,
        icon: options.icon,
        command: options.command,
        moreCommands: options.moreCommands,
        isCritical: options.isCritical,
    };
}
// ---------------------------------------------------------------------------
// Section header helper
// ---------------------------------------------------------------------------
/**
 * Create a section header separator item.
 * Items returned after this one (with the same `section` value) will appear
 * grouped under this header in the Command Palette UI.
 *
 * Internally this creates a list item with **no command**, which the host
 * recognises as a non-interactive separator row.
 *
 * @example
 * getItems() {
 *   return [
 *     sectionHeader('🚀 Getting Started'),
 *     new ListItem({ title: 'Install', ... }),
 *     sectionHeader('📚 Docs'),
 *     new ListItem({ title: 'API Reference', ... }),
 *   ];
 * }
 */
function sectionHeader(title) {
    return {
        title,
        section: title,
        // Deliberately no `command` — tells the host this is a separator
    };
}
// ---------------------------------------------------------------------------
// Grid layout helpers
// ---------------------------------------------------------------------------
/** Small grid layout — icon-only tiles with no text. */
class SmallGridLayout {
    constructor() {
        this.layout = 'small';
    }
}
exports.SmallGridLayout = SmallGridLayout;
/** Medium grid layout — tiles with optional title text below. */
class MediumGridLayout {
    constructor(showTitle = true) {
        this.layout = 'medium';
        this.showTitle = showTitle;
    }
}
exports.MediumGridLayout = MediumGridLayout;
/** Gallery grid layout — large tiles with optional title and subtitle. */
class GalleryGridLayout {
    constructor(showTitle = true, showSubtitle = true) {
        this.layout = 'gallery';
        this.showTitle = showTitle;
        this.showSubtitle = showSubtitle;
    }
}
exports.GalleryGridLayout = GalleryGridLayout;
//# sourceMappingURL=helpers.js.map