// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ListPage,
  IListItem,
  IconInfo,
  NoOpCommand,
  SmallGridLayout,
  MediumGridLayout,
  GalleryGridLayout,
} from '@cmdpal/sdk';

// ---------------------------------------------------------------------------
// Shared gallery items — reused by each grid sub-page
// ---------------------------------------------------------------------------

function galleryItems(): IListItem[] {
  return [
    {
      title: '🌅 Sunrise',
      subtitle: 'A beautiful morning sky',
      icon: IconInfo.fromGlyph('\uE706'), // Sun
      command: new NoOpCommand(),
    },
    {
      title: '🏔️ Mountain',
      subtitle: 'Snow-capped peak',
      icon: IconInfo.fromGlyph('\uE909'), // Trackers
      command: new NoOpCommand(),
    },
    {
      title: '🌊 Ocean',
      subtitle: 'Deep blue waves',
      icon: IconInfo.fromGlyph('\uE81E'), // RainShowersDay
      command: new NoOpCommand(),
    },
    {
      title: '🌲 Forest',
      subtitle: 'Tall evergreen trees',
      icon: IconInfo.fromGlyph('\uE8BA'), // Leaf
      command: new NoOpCommand(),
    },
    {
      title: '🏜️ Desert',
      subtitle: 'Golden sand dunes',
      icon: IconInfo.fromGlyph('\uE753'), // Globe
      command: new NoOpCommand(),
    },
    {
      title: '🌌 Galaxy',
      subtitle: 'Spiral of stars',
      icon: IconInfo.fromGlyph('\uE7C4'), // FavoriteStar
      command: new NoOpCommand(),
    },
    {
      title: '🌸 Cherry Blossom',
      subtitle: 'Delicate pink petals',
      icon: IconInfo.fromGlyph('\uE734'), // Color
      command: new NoOpCommand(),
    },
    {
      title: '❄️ Snowflake',
      subtitle: 'Crystalline ice',
      icon: IconInfo.fromGlyph('\uE816'), // Snowflake
      command: new NoOpCommand(),
    },
    {
      title: '🌈 Rainbow',
      subtitle: 'After the rain',
      icon: IconInfo.fromGlyph('\uE790'), // Color
      command: new NoOpCommand(),
    },
    {
      title: '🌻 Sunflower',
      subtitle: 'Bright and cheerful',
      icon: IconInfo.fromGlyph('\uE712'), // Brightness
      command: new NoOpCommand(),
    },
    {
      title: '🍁 Maple Leaf',
      subtitle: 'Autumn foliage',
      icon: IconInfo.fromGlyph('\uE7C1'), // CalendarDay
      command: new NoOpCommand(),
    },
    {
      title: '⚡ Lightning',
      subtitle: 'Electric storm',
      icon: IconInfo.fromGlyph('\uE945'), // Lightning
      command: new NoOpCommand(),
    },
  ] as IListItem[];
}

// ---------------------------------------------------------------------------
// Sub-pages: each uses the same items with a different grid layout
// ---------------------------------------------------------------------------

class GalleryFullPage extends ListPage {
  id = 'grid-gallery-full';
  name = 'Gallery (title + subtitle)';
  gridProperties = new GalleryGridLayout(true, true);

  getItems(): IListItem[] {
    return galleryItems();
  }
}

class GalleryTitleOnlyPage extends ListPage {
  id = 'grid-gallery-title';
  name = 'Gallery (title only)';
  gridProperties = new GalleryGridLayout(true, false);

  getItems(): IListItem[] {
    return galleryItems();
  }
}

class GalleryNoTextPage extends ListPage {
  id = 'grid-gallery-notext';
  name = 'Gallery (no text)';
  gridProperties = new GalleryGridLayout(false, false);

  getItems(): IListItem[] {
    return galleryItems();
  }
}

class MediumGridPage extends ListPage {
  id = 'grid-medium';
  name = 'Medium Grid (with title)';
  gridProperties = new MediumGridLayout(true);

  getItems(): IListItem[] {
    return galleryItems();
  }
}

class MediumGridNoTitlePage extends ListPage {
  id = 'grid-medium-notitle';
  name = 'Medium Grid (no title)';
  gridProperties = new MediumGridLayout(false);

  getItems(): IListItem[] {
    return galleryItems();
  }
}

class SmallGridPage extends ListPage {
  id = 'grid-small';
  name = 'Small Grid';
  gridProperties = new SmallGridLayout();

  getItems(): IListItem[] {
    return galleryItems();
  }
}

// ---------------------------------------------------------------------------
// Main grid showcase page — lists the sub-pages as drilldown items
// ---------------------------------------------------------------------------

/**
 * Demonstrates grid and gallery layouts:
 * - Gallery layout (large tiles) with title + subtitle, title only, no text
 * - Medium grid with and without title
 * - Small grid (icon-only tiles)
 *
 * Similar to WinRT SampleGridsListPage.
 */
export class GridShowcasePage extends ListPage {
  id = 'grid-showcase';
  name = 'Grid & Gallery Layouts';
  icon = IconInfo.fromGlyph('\uE80A'); // ViewAll

  // Sub-pages registered for navigation
  readonly galleryFull = new GalleryFullPage();
  readonly galleryTitleOnly = new GalleryTitleOnlyPage();
  readonly galleryNoText = new GalleryNoTextPage();
  readonly mediumGrid = new MediumGridPage();
  readonly mediumGridNoTitle = new MediumGridNoTitlePage();
  readonly smallGrid = new SmallGridPage();

  getItems(): IListItem[] {
    return [
      {
        title: 'Gallery (title + subtitle)',
        subtitle: 'Large tiles showing icon, title, and subtitle',
        icon: IconInfo.fromGlyph('\uEB9F'),
        command: this.galleryFull,
      },
      {
        title: 'Gallery (title only)',
        subtitle: 'Large tiles showing icon and title',
        icon: IconInfo.fromGlyph('\uEB9F'),
        command: this.galleryTitleOnly,
      },
      {
        title: 'Gallery (no text)',
        subtitle: 'Large tiles showing only the icon',
        icon: IconInfo.fromGlyph('\uEB9F'),
        command: this.galleryNoText,
      },
      {
        title: 'Medium Grid (with title)',
        subtitle: 'Medium tiles with title below',
        icon: IconInfo.fromGlyph('\uE80A'),
        command: this.mediumGrid,
      },
      {
        title: 'Medium Grid (no title)',
        subtitle: 'Medium tiles, icon only',
        icon: IconInfo.fromGlyph('\uE80A'),
        command: this.mediumGridNoTitle,
      },
      {
        title: 'Small Grid',
        subtitle: 'Compact icon-only tiles',
        icon: IconInfo.fromGlyph('\uF0E2'),
        command: this.smallGrid,
      },
    ] as IListItem[];
  }
}
