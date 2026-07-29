// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ListItemBase, ListPageBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { GridProperties, IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

let galleryPageCounter = 0;

/**
 * A gallery/grid page rendered with a caller-supplied layout. Mirrors the C#
 * `SampleGalleryListPage`.
 *
 * The C# sample decorates its items with bundled image assets. This sample ships
 * no binary assets, so Segoe Fluent glyphs stand in for the images.
 */
export class SampleGalleryListPage extends ListPageBase {
  readonly id: string;
  readonly name = 'Sample Gallery List Page';
  readonly title = 'Sample Gallery List Page';

  constructor(gridProperties: GridProperties) {
    super();
    this.id = `sample-gallery-${galleryPageCounter++}`;
    this.gridProperties = gridProperties;
  }

  override getItems(): IListItem[] {
    const glyphs = ['\uE753', '\uE8B9', '\uE909', '\uE7F4', '\uE774', '\uE8B9', '\uE909'];
    const titles = [
      'Sample Title',
      'Another Title',
      'More Titles',
      'Stop With The Titles',
      'Another Title',
      'More Titles',
      'Stop With The Titles',
    ];
    return titles.map(
      (title, index) =>
        new ListItemBase({
          command: new NoOpCommand(`gallery-item-${index}`),
          title,
          subtitle: "I don't do anything",
          icon: icon(glyphs[index] ?? '\uE753'),
        }),
    );
  }
}

/**
 * An index of grid and gallery layout variants. Mirrors the C#
 * `SampleGridsListPage`.
 */
export class SampleGridsListPage extends ListPageBase {
  readonly id = 'sample-grids-list-page';
  readonly name = 'Grid and gallery lists';
  readonly title = 'Grid and gallery lists';

  override icon = icon('\uE7C5');

  override getItems(): IListItem[] {
    return [
      new ListItemBase({
        command: new SampleGalleryListPage({ type: 'gallery', showTitle: true, showSubtitle: true }),
        title: 'Gallery list page (title and subtitle)',
        subtitle: 'A sample gallery list page with images',
        icon: icon('\uE909'),
      }),
      new ListItemBase({
        command: new SampleGalleryListPage({ type: 'gallery', showTitle: true, showSubtitle: false }),
        title: 'Gallery list page (title, no subtitle)',
        subtitle: 'A sample gallery list page with images',
        icon: icon('\uE909'),
      }),
      new ListItemBase({
        command: new SampleGalleryListPage({ type: 'gallery', showTitle: false, showSubtitle: false }),
        title: 'Gallery list page (no title, no subtitle)',
        subtitle: 'A sample gallery list page with images',
        icon: icon('\uE909'),
      }),
      new ListItemBase({
        command: new SampleGalleryListPage({ type: 'small' }),
        title: 'Small grid list page',
        subtitle: 'A sample grid list page with text items',
        icon: icon('\uE8B9'),
      }),
      new ListItemBase({
        command: new SampleGalleryListPage({ type: 'medium', showTitle: true }),
        title: 'Medium grid (with title)',
        subtitle: 'A sample grid list page with text items',
        icon: icon('\uE8B9'),
      }),
      new ListItemBase({
        command: new SampleGalleryListPage({ type: 'medium', showTitle: false }),
        title: 'Medium grid (hidden title)',
        subtitle: 'A sample grid list page with text items',
        icon: icon('\uE8B9'),
      }),
    ];
  }
}
