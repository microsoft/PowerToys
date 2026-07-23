// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ListItemBase, ListPageBase, NoOpCommand, Separator } from '@microsoft/cmdpal-sdk';
import type { GridProperties, IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

let sectionPageCounter = 0;

/**
 * A list (or grid) page that groups items under headings. The host only shows a
 * heading for a `Separator` that carries a title, so each group is introduced by
 * a titled `Separator` rather than by tagging command items with a `section`
 * field (the host ignores `section` on command-bearing items). Mirrors the C#
 * `SampleListPageWithSections`, whose `Section` objects become titled separators
 * here.
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
      }),
      new Separator('This is another section list'),
      new ListItemBase({
        command: new NoOpCommand('sec2-a'),
        title: 'Another Title',
        subtitle: "I don't do anything",
      }),
      new ListItemBase({
        command: new NoOpCommand('sec2-b'),
        title: 'More Titles',
        subtitle: "I don't do anything",
      }),
      new ListItemBase({
        command: new NoOpCommand('sec2-c'),
        title: 'Stop With The Titles',
        subtitle: "I don't do anything",
      }),
      new Separator(),
      new ListItemBase({
        command: new NoOpCommand('sec-sep'),
        title: 'Separators also work',
        subtitle: "But I still don't do anything",
      }),
      new Separator("There's another"),
      new ListItemBase({
        command: new NoOpCommand('sec3-a'),
        title: 'Sample Title',
        subtitle: "I don't do anything",
      }),
      new ListItemBase({
        command: new NoOpCommand('sec3-b'),
        title: 'Another Title',
        subtitle: "I don't do anything",
      }),
      new ListItemBase({
        command: new NoOpCommand('sec3-c'),
        title: 'More Titles',
        subtitle: "I don't do anything",
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
