// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  CopyTextCommand,
  iconFromBase64,
  iconFromUrl,
  ListItemBase,
  ListPageBase,
} from '@microsoft/cmdpal-sdk';
import type { Details, IconInfo, IListItem, Tag } from '@microsoft/cmdpal-sdk';
import { getHeroImage, glyphIcon, samplePngBase64 } from '../util.js';

type IconLoader = () => Promise<IconInfo>;

const firstPartyIconUrl =
  'https://raw.githubusercontent.com/microsoft/PowerToys/main/doc/images/icons/PowerToys%20icon/Vintage/Logo-HiRes.png';
const loadingFallback = glyphIcon('\uE774');

/*
 * Quick intro to Unicode in source code:
 * Every character has a code point (for example U+0041 = 'A').
 * Code points up to U+FFFF use \u1234 (four hex digits).
 * Code points above that use \u{XXXXX} in JavaScript source.
 * Some symbols, like many emojis, are built from multiple code points joined
 * together (for example a waving hand plus a skin tone modifier).
 *
 * Mirrors the C# `SampleIconPage`.
 */
const iconSamples: Array<[string, string, string]> = [
  ['\u{1F60D}', 'Standard emoji icon', 'Basic emoji character rendered as an icon'],
  ['\u{1F60D}\u{1F643}\u{1F622}', 'Multiple emojis', 'Use of multiple emojis for icon is not allowed'],
  ['\u{1F60E}', 'Unicode escape sequence emoji', 'Emoji defined using Unicode escape sequence notation'],
  ['\uE8D4', 'Segoe Fluent icon demonstration', "Segoe Fluent/MDL2 icon from system font\nWorks as an icon but won't display properly in button text"],
  ['\u2328', 'Extended pictographic symbol', 'Pictographic symbol representing a keyboard'],
  ['A', 'Simple text character as icon', 'Basic letter character used as an icon demonstration'],
  ['1', 'Simple text character as icon', 'Basic letter character used as an icon demonstration'],
  ['\u{32}\u{20E3}', 'Emoji without variation selector', "Emoji character doesn't have VS16 variation selector to render as text"],
  ['\u{33}\uFE0F\u{20E3}', 'Emoji with variation selector', 'Emoji character using a variation selector to specify emoji presentation'],
  ['#', 'Simple text character as icon', 'Basic letter character used as an icon demonstration'],
  ['\u0023\uFE0F\u20E3', 'Simple text character as icon', 'Basic letter character used as an icon demonstration'],
  ['WM', 'Invalid icon representation', 'String with multiple characters that does not correspond to a valid single icon'],
  ['\u{1F9D9}', 'Single code-point emoji example', 'Simple emoji character using a single Unicode code point'],
  ['\u{1F9D9}\u200D\u2642\uFE0F', 'Complex emoji with gender modifier', 'Composite emoji using Zero-Width Joiner (ZWJ) sequence for male variant'],
  ['\u{1F9D9}\u200D\u2640\uFE0F', 'Complex emoji with gender modifier', 'Composite emoji using Zero-Width Joiner (ZWJ) sequence for female variant'],
  ['\u{1F44B}', 'Basic hand gesture emoji', 'Standard emoji character representing a waving hand'],
  ['\u{1F44B}\u{1F3FB}', 'Emoji with light skin tone modifier', 'Emoji enhanced with Unicode skin tone modifier (light)'],
  ['\u{1F44B}\u{1F3FF}', 'Emoji with dark skin tone modifier', 'Emoji enhanced with Unicode skin tone modifier (dark)'],
  ['\u{1F1E8}\u{1F1FF}', 'Flag emoji using regional indicators', 'Emoji flag constructed from regional indicator symbols for Czechia'],
  ['\u0995\u09CD\u200D', 'Use of ZWJ in non-emoji context', 'Shows the half-form KA'],
  ['\u0995\u09CD', 'Use of ZWJ in non-emoji context', 'Shows full KA with an explicit virama mark'],
  ['\u{1F004}', 'Mahjong tile emoji (red dragon)', 'Mahjong tile red dragon emoji character using Unicode escape sequence'],
  ['\u{1F005}', 'Mahjong tile non-emoji (green dragon)', 'Mahjong tile character that is not classified as an emoji'],
  ['\u25B6', 'Play symbol (standalone)', 'Play symbol'],
  ['\u25B6\uFE0E', 'Play symbol + VS15 (request text)', 'Play symbol with variation specifier requesting rendering as text'],
  ['\u25B6\uFE0F', 'Play symbol + VS16 (request emoji)', 'Play symbol with variation specifier requesting rendering as emoji'],
  ['\u{23EF}\uFE0F', 'Play/Pause keycap emoji', "Play/Pause keycap emoji doesn't have plain text variant"],
  ['\u{23F8}\uFE0F', 'Pause keycap emoji', "Pause keycap emoji doesn't have plain text variant"],
  ['\u00A9', 'Copyright symbol (standalone)', 'Copyright symbol that is not classified as an emoji'],
  ['\u00A9\uFE0E', 'Copyright symbol + VS15 (request text)', 'Copyright symbol that is not classified as an emoji'],
  ['\u00A9\uFE0F', 'Copyright symbol + VS16 (request emoji)', 'Copyright symbol that is not classified as an emoji'],
  ['\u{1F3F3}\uFE0F', 'White Flag', 'White Flag'],
  ['\u{1F3F4}\u200D\u2620\uFE0F', 'Pirate Flag', 'Pirate Flag'],
];

function codePointTags(value: string): Tag[] {
  return [...value].map((ch) => {
    const cp = ch.codePointAt(0) ?? 0;
    const hex =
      cp <= 0xffff
        ? `\\u${cp.toString(16).toUpperCase().padStart(4, '0')}`
        : `\\U${cp.toString(16).toUpperCase().padStart(8, '0')}`;
    return { text: hex };
  });
}

function buildIconItem(glyph: string, title: string, description: string): IListItem {
  const iconInfo = glyphIcon(glyph);
  const details: Details = {
    heroImage: iconInfo,
    title,
    body: description,
    metadata: [
      {
        key: 'Unicode Code Points',
        data: { type: 'tags', tags: codePointTags(glyph) },
      },
    ],
  };

  return new ListItemBase({
    command: new CopyTextCommand(glyph, `Action with ${glyph}`),
    title,
    subtitle: description,
    icon: iconInfo,
    tags: [{ text: 'Tag', icon: iconInfo }],
    details,
  });
}

class MutableIconItem extends ListItemBase {
  updateIcon(icon: IconInfo): void {
    if (this.icon === icon) {
      return;
    }

    this.icon = icon;
    this.notifyPropChanged('icon');
  }
}

/** A demo of how many icon strings are interpreted. Mirrors `SampleIconPage`. */
export class SampleIconPage extends ListPageBase {
  readonly id = 'sample-icon-page';
  readonly name = 'Sample Icon Page';
  readonly title = 'Sample Icon Page';

  override icon = glyphIcon('\uE8BA');
  override showDetails = true;

  private readonly items = iconSamples.map(([glyph, title, description]) =>
    buildIconItem(glyph, title, description),
  );
  private readonly packagedFileItem = new MutableIconItem({
    command: new CopyTextCommand('assets/hero.png', 'Copy packaged file path'),
    title: 'Packaged file icon',
    subtitle: 'Loads the bundled PNG after this list is requested and sends its bytes, not a machine path',
    icon: loadingFallback,
  });
  private readonly urlItem = new MutableIconItem({
    command: new CopyTextCommand(firstPartyIconUrl, 'Copy first-party URL'),
    title: 'First-party URL icon',
    subtitle: 'Loads after this list is requested and keeps this glyph fallback when offline',
    icon: loadingFallback,
  });
  private readonly sourceItems = [
    this.packagedFileItem,
    this.urlItem,
    new ListItemBase({
      command: new CopyTextCommand(samplePngBase64, 'Copy inline base64'),
      title: 'Inline base64 icon',
      subtitle: 'iconFromBase64 sends image bytes without a file or network request',
      icon: iconFromBase64(samplePngBase64),
    }),
  ];
  private packagedFileIconLoad?: Promise<void>;
  private urlIconLoad?: Promise<void>;

  private loadIcon(loader: IconLoader, item: MutableIconItem): Promise<void> {
    return Promise.resolve()
      .then(loader)
      .catch(() => loadingFallback)
      .then((loadedIcon) => item.updateIcon(loadedIcon));
  }

  private startIconLoads(): void {
    this.packagedFileIconLoad ??= this.loadIcon(getHeroImage, this.packagedFileItem);
    this.urlIconLoad ??= this.loadIcon(() => iconFromUrl(firstPartyIconUrl), this.urlItem);
  }

  override getItems(): IListItem[] {
    this.startIconLoads();
    return [...this.sourceItems, ...this.items];
  }
}
