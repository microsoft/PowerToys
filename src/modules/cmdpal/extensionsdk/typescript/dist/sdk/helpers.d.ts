import { IIconInfo, ITag, IDetails, IDetailsElement, IContextItem, IListItem, ISmallGridLayout, IMediumGridLayout, IGalleryGridLayout, OptionalColor, ContentSize, ICommand } from '../generated/types';
/**
 * Create a visible color from RGBA components.
 * @example color(59, 130, 246) // blue
 * @example color(255, 0, 0, 128) // semi-transparent red
 */
export declare function color(r: number, g: number, b: number, a?: number): OptionalColor;
/** Sentinel for "no color" — property won't apply. */
export declare function noColor(): OptionalColor;
/** Convenience overloads for building IIconInfo. */
export declare const IconInfo: {
    /**
     * Create an icon from a Segoe MDL2 glyph or emoji.
     * Uses the same glyph for both light and dark themes.
     * @example IconInfo.fromGlyph('\uE8C8')
     * @example IconInfo.fromGlyph('🍓')
     */
    fromGlyph(glyph: string): IIconInfo;
    /**
     * Create an icon from an emoji character.
     * Alias for fromGlyph — emoji works the same as glyphs.
     * @example IconInfo.fromEmoji('🌅')
     */
    fromEmoji(emoji: string): IIconInfo;
    /**
     * Create an icon with separate light/dark glyphs.
     * @example IconInfo.fromGlyphs('\uE8C8', '\uE8C9')
     */
    fromGlyphs(lightGlyph: string, darkGlyph: string): IIconInfo;
    /** Empty icon (no visual). */
    empty(): IIconInfo;
};
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
export declare function tag(options: TagOptions | string): ITag;
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
export declare function details(options: DetailsOptions): IDetails;
/** A metadata row with tag pills. */
export declare function metadataTags(key: string, tags: ITag[], icon?: IIconInfo): IDetailsElement;
/** A metadata row with a link. */
export declare function metadataLink(key: string, text: string, url: string, icon?: IIconInfo): IDetailsElement;
/** A metadata separator line. */
export declare function metadataSeparator(): IDetailsElement;
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
export declare function contextItem(options: ContextItemOptions): IContextItem;
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
export declare function sectionHeader(title: string): IListItem;
/** Small grid layout — icon-only tiles with no text. */
export declare class SmallGridLayout implements ISmallGridLayout {
    readonly layout = "small";
    PropChanged?: (args: unknown) => void;
}
/** Medium grid layout — tiles with optional title text below. */
export declare class MediumGridLayout implements IMediumGridLayout {
    readonly layout = "medium";
    showTitle: boolean;
    PropChanged?: (args: unknown) => void;
    constructor(showTitle?: boolean);
}
/** Gallery grid layout — large tiles with optional title and subtitle. */
export declare class GalleryGridLayout implements IGalleryGridLayout {
    readonly layout = "gallery";
    showTitle: boolean;
    showSubtitle: boolean;
    PropChanged?: (args: unknown) => void;
    constructor(showTitle?: boolean, showSubtitle?: boolean);
}
//# sourceMappingURL=helpers.d.ts.map