// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  IIconInfo,
  IIconData,
  ITag,
  IDetails,
  IDetailsElement,
  IDetailsData,
  IDetailsTags,
  IDetailsLink,
  IDetailsSeparator,
  IContextItem,
  IListItem,
  ISmallGridLayout,
  IMediumGridLayout,
  IGalleryGridLayout,
  OptionalColor,
  Color,
  ContentSize,
  DetailsDataType,
  ICommand,
} from '../generated/types';

// ---------------------------------------------------------------------------
// Color helpers
// ---------------------------------------------------------------------------

/**
 * Create a visible color from RGBA components.
 * @example color(59, 130, 246) // blue
 * @example color(255, 0, 0, 128) // semi-transparent red
 */
export function color(r: number, g: number, b: number, a: number = 255): OptionalColor {
  return { hasValue: true, color: { r, g, b, a } };
}

/** Sentinel for "no color" — property won't apply. */
export function noColor(): OptionalColor {
  return { hasValue: false, color: { r: 0, g: 0, b: 0, a: 0 } };
}

// ---------------------------------------------------------------------------
// Icon helpers
// ---------------------------------------------------------------------------

/** Convenience overloads for building IIconInfo. */
export const IconInfo = {
  /**
   * Create an icon from a Segoe MDL2 glyph or emoji.
   * Uses the same glyph for both light and dark themes.
   * @example IconInfo.fromGlyph('\uE8C8')
   * @example IconInfo.fromGlyph('🍓')
   */
  fromGlyph(glyph: string): IIconInfo {
    return { light: { icon: glyph }, dark: { icon: glyph } };
  },

  /**
   * Create an icon from an emoji character.
   * Alias for fromGlyph — emoji works the same as glyphs.
   * @example IconInfo.fromEmoji('🌅')
   */
  fromEmoji(emoji: string): IIconInfo {
    return { light: { icon: emoji }, dark: { icon: emoji } };
  },

  /**
   * Create an icon with separate light/dark glyphs.
   * @example IconInfo.fromGlyphs('\uE8C8', '\uE8C9')
   */
  fromGlyphs(lightGlyph: string, darkGlyph: string): IIconInfo {
    return { light: { icon: lightGlyph }, dark: { icon: darkGlyph } };
  },

  /** Empty icon (no visual). */
  empty(): IIconInfo {
    return { light: { icon: '' }, dark: { icon: '' } };
  },
};

// ---------------------------------------------------------------------------
// Tag helpers
// ---------------------------------------------------------------------------

export interface TagOptions {
  text: string;
  icon?: IIconInfo;
  toolTip?: string;
  foreground?: OptionalColor;
  background?: OptionalColor;
}

/**
 * Create a tag with optional styling.
 * @example tag({ text: 'Beta' })
 * @example tag({ text: 'v1.0', foreground: color(59, 130, 246) })
 */
export function tag(options: TagOptions | string): ITag {
  if (typeof options === 'string') {
    return {
      text: options,
      icon: IconInfo.empty(),
      toolTip: options,
      foreground: noColor(),
      background: noColor(),
    };
  }
  return {
    text: options.text,
    icon: options.icon ?? IconInfo.empty(),
    toolTip: options.toolTip ?? options.text,
    foreground: options.foreground ?? noColor(),
    background: options.background ?? noColor(),
  };
}

// ---------------------------------------------------------------------------
// Details helpers
// ---------------------------------------------------------------------------

export interface DetailsOptions {
  title?: string;
  body?: string;
  heroImage?: IIconInfo;
  metadata?: IDetailsElement[];
  size?: ContentSize;
}

/**
 * Build a details panel descriptor.
 * @example details({ title: 'About', body: 'Description text' })
 */
export function details(options: DetailsOptions): IDetails {
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
export function metadataTags(key: string, tags: ITag[], icon?: IIconInfo): IDetailsElement {
  const data: IDetailsTags = { tags };
  return { key, icon, data };
}

/** A metadata row with a link. */
export function metadataLink(key: string, text: string, url: string, icon?: IIconInfo): IDetailsElement {
  const data: IDetailsLink = { text, url };
  return { key, icon, data };
}

/** A metadata separator line. */
export function metadataSeparator(): IDetailsElement {
  const data: IDetailsSeparator = { _type: DetailsDataType.Separator };
  return { key: '', data };
}

// ---------------------------------------------------------------------------
// Context item helper
// ---------------------------------------------------------------------------

export interface ContextItemOptions {
  title: string;
  subtitle?: string;
  icon?: IIconInfo;
  command: ICommand;
  moreCommands?: IContextItem[];
  isCritical?: boolean;
}

/**
 * Build a context menu item.
 * @example contextItem({ title: 'Copy', icon: IconInfo.fromGlyph('\uE8C8'), command: myCopyCmd })
 */
export function contextItem(options: ContextItemOptions): IContextItem {
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
export function sectionHeader(title: string): IListItem {
  return {
    title,
    section: title,
    // Deliberately no `command` — tells the host this is a separator
  } as IListItem;
}

// ---------------------------------------------------------------------------
// Grid layout helpers
// ---------------------------------------------------------------------------

/** Small grid layout — icon-only tiles with no text. */
export class SmallGridLayout implements ISmallGridLayout {
  readonly layout = 'small';
  PropChanged?: (args: unknown) => void;
}

/** Medium grid layout — tiles with optional title text below. */
export class MediumGridLayout implements IMediumGridLayout {
  readonly layout = 'medium';
  showTitle: boolean;
  PropChanged?: (args: unknown) => void;

  constructor(showTitle: boolean = true) {
    this.showTitle = showTitle;
  }
}

/** Gallery grid layout — large tiles with optional title and subtitle. */
export class GalleryGridLayout implements IGalleryGridLayout {
  readonly layout = 'gallery';
  showTitle: boolean;
  showSubtitle: boolean;
  PropChanged?: (args: unknown) => void;

  constructor(showTitle: boolean = true, showSubtitle: boolean = true) {
    this.showTitle = showTitle;
    this.showSubtitle = showSubtitle;
  }
}
