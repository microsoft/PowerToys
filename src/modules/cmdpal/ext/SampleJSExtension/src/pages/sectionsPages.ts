// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ListItemBase, ListPageBase, NoOpCommand, Separator } from '@microsoft/cmdpal-sdk';
import type { GridProperties, IListItem } from '@microsoft/cmdpal-sdk';
import { fileURLToPath } from 'node:url';
import { icon } from '../util.js';

let sectionPageCounter = 0;

/**
 * Absolute path to the image asset that ships with the sample. Grid and gallery
 * layouts show each item's icon prominently, so the section items below carry a
 * committed local image the same way the C# `SampleListPageWithSections`
 * assigns bundled images to every item. Resolving it from `import.meta.url`
 * renders without a network fetch. See detailsPage.ts for the same pattern.
 */
const sectionImagePath = fileURLToPath(new URL('../assets/hero.png', import.meta.url));

/**
 * A list (or grid) page that groups items under headings. The host only shows a
 * heading for a `Separator` that carries a title, so each group is introduced by
 * a titled `Separator` rather than by tagging command items with a `section`
 * field (the host ignores `section` on command-bearing items). Mirrors the C#
 * `SampleListPageWithSections`, whose `Section` objects become titled separators
 * here.
 *
 * The host renders a titled `Separator` as heading text with no divider line and
 * a plain `Separator` (no title) as a horizontal line with no text; that is the
 * built-in behavior shared with the C# section pages. To give each group a
 * visible divider, a plain `Separator` line is emitted between sections in
 * addition to the titled heading. Every item also carries a local image icon so
 * the grid and gallery variants show pictures rather than empty tiles.
 */
export class SampleListPageWithSections extends ListPageBase {
  readonly id: string;
  readonly name = 'Sample Gallery List Page';
  readonly title = 'Sample Gallery List Page';

  override icon = icon('\uE7C5');

  constructor(gridProperties?: GridProperties) {
    super();
    this.id = `sample-list-with-sections-${sectionPageCounter++}`;
    this.gridProperties = gridProperties ?? null;
  }

  override getItems(): IListItem[] {
    return [
      new Separator('This is a section list'),
      new ListItemBase({
        command: new NoOpCommand('sec1-a'),
        title: 'Sample Title',
        subtitle: "I don't do anything",
        icon: icon(sectionImagePath),
      }),
      new Separator(),
      new Separator('This is another section list'),
      new ListItemBase({
        command: new NoOpCommand('sec2-a'),
        title: 'Another Title',
        subtitle: "I don't do anything",
        icon: icon(sectionImagePath),
      }),
      new ListItemBase({
        command: new NoOpCommand('sec2-b'),
        title: 'More Titles',
        subtitle: "I don't do anything",
        icon: icon(sectionImagePath),
      }),
      new ListItemBase({
        command: new NoOpCommand('sec2-c'),
        title: 'Stop With The Titles',
        subtitle: "I don't do anything",
        icon: icon(sectionImagePath),
      }),
      new Separator(),
      new ListItemBase({
        command: new NoOpCommand('sec-sep'),
        title: 'Separators also work',
        subtitle: "But I still don't do anything",
        icon: icon(sectionImagePath),
      }),
      new Separator(),
      new Separator("There's another"),
      new ListItemBase({
        command: new NoOpCommand('sec3-a'),
        title: 'Sample Title',
        subtitle: "I don't do anything",
        icon: icon(sectionImagePath),
      }),
      new ListItemBase({
        command: new NoOpCommand('sec3-b'),
        title: 'Another Title',
        subtitle: "I don't do anything",
        icon: icon(sectionImagePath),
      }),
      new ListItemBase({
        command: new NoOpCommand('sec3-c'),
        title: 'More Titles',
        subtitle: "I don't do anything",
        icon: icon(sectionImagePath),
      }),
    ];
  }
}

/** An index of the section-list variants. Mirrors the C# `SectionsIndexPage`. */
export class SectionsIndexPage extends ListPageBase {
  readonly id = 'sections-index-page';
  readonly name = 'Sections Index Page';
  readonly title = 'Sections Index Page';

  override icon = icon('\uF168');

  override getItems(): IListItem[] {
    return [
      new ListItemBase({
        command: new SampleListPageWithSections(),
        title: 'A list page with sections',
      }),
      new ListItemBase({
        command: new SampleListPageWithSections({ type: 'small' }),
        title: 'A small grid page with sections',
      }),
      new ListItemBase({
        command: new SampleListPageWithSections({ type: 'medium', showTitle: true }),
        title: 'A medium grid page with sections',
      }),
      new ListItemBase({
        command: new SampleListPageWithSections({ type: 'gallery', showTitle: true, showSubtitle: true }),
        title: 'A Gallery grid page with sections',
      }),
      new ListItemBase({
        command: new SampleListPageWithSections({ type: 'gallery', showTitle: false, showSubtitle: false }),
        title: 'A Gallery grid page without labels with sections',
      }),
    ];
  }
}
