// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ListItemBase, ListPageBase } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from './util.js';
import { SampleListPage } from './pages/listPage.js';
import { SampleToastsPage } from './pages/toastsPage.js';
import { SampleListPageWithDetails } from './pages/detailsPage.js';
import { SampleLiveDetailsPage } from './pages/liveDetailsPage.js';
import { SectionsIndexPage } from './pages/sectionsPages.js';
import { SampleUpdatingItemsPage } from './pages/updatingItemsPage.js';
import { SampleDynamicListPage } from './pages/dynamicListPage.js';
import { SampleGridsListPage } from './pages/gridsPages.js';
import { OnLoadPage } from './pages/onLoadPage.js';
import { SampleIconPage } from './pages/iconPage.js';
import { SlowListPage } from './pages/slowListPage.js';
import { SampleSuggestionsPage } from './pages/suggestionsPage.js';
import {
  SampleContentPage,
  SampleImageContentPage,
  SamplePlainTextContentPage,
  SampleTreeContentPage,
} from './pages/contentPages.js';
import { SampleCommentsPage } from './pages/commentsPage.js';
import {
  SampleMarkdownDetails,
  SampleMarkdownImagesPage,
  SampleMarkdownManyBodies,
  SampleMarkdownPage,
} from './pages/markdownPages.js';
import { SampleSettingsPage } from './pages/settingsPage.js';
import { SampleDataTransferPage } from './pages/dataTransferPage.js';

/**
 * The top-level index of every sample, mirroring the C# `SamplesListPage`.
 *
 * The following C# entries are intentionally not mirrored because they rely on
 * capabilities the JS protocol does not yet expose (see README.md):
 *  - Parameter pages (SimpleParameterTest, ButtonParameterTest, MixedParamTestPage).
 *  - Create note sample (CreateNoteParametersPage), which needs list parameters.
 *  - Evil samples (EvilSamplesPage) and Issue-specific samples, which reproduce
 *    host ABI edge cases from inside the C# process.
 */
export class SamplesListPage extends ListPageBase {
  readonly id = 'js-samples-list-page';
  readonly name = 'Samples';
  readonly title = 'Samples';

  override icon = icon('\ue946');

  override getItems(): IListItem[] {
    return [
      new ListItemBase({
        command: new SampleListPage(),
        title: 'List Page Sample Command',
        subtitle: 'Display a list of items',
      }),
      new ListItemBase({
        command: new SampleToastsPage(),
        title: 'Toast Notification Samples',
        subtitle: 'Demonstrates CommandResult.ShowToast and lets you send custom toasts',
      }),
      new ListItemBase({
        command: new SampleListPageWithDetails(),
        title: 'List Page With Details',
        subtitle: 'A list of items, each with additional details to display',
      }),
      new ListItemBase({
        command: new SampleLiveDetailsPage(),
        title: 'Live Updating Details',
        subtitle: 'Details pane updates in real time without reselecting',
      }),
      new ListItemBase({
        command: new SectionsIndexPage(),
        title: 'List Pages With Sections',
        subtitle: 'A list of items, with sections header',
      }),
      new ListItemBase({
        command: new SampleUpdatingItemsPage(),
        title: 'List page with items that change',
        subtitle: 'The items on the list update themselves in real time',
      }),
      new ListItemBase({
        command: new SampleDynamicListPage(),
        title: 'Dynamic List Page Command',
        subtitle: 'Changes the list of items in response to the typed query',
      }),
      new ListItemBase({
        command: new SampleGridsListPage(),
        title: 'Grid views and galleries',
        subtitle: 'Displays items as a gallery',
      }),
      new ListItemBase({
        command: new OnLoadPage(),
        title: 'Demo of OnLoad/OnUnload',
        subtitle: 'Changes the list of items every time the page is opened / closed',
      }),
      new ListItemBase({
        command: new SampleIconPage(),
        title: 'Sample Icon Page',
        subtitle: 'A demo of using icons in various ways',
      }),
      new ListItemBase({
        command: new SlowListPage(),
        title: 'Slow loading list page',
        subtitle: 'A demo of a list page that takes a while to load',
      }),
      new ListItemBase({
        command: new SampleSuggestionsPage(),
        title: 'Sample Prefix Suggestions',
        subtitle: "A demo of using 'nested' pages to provide 'suggestions' as the user types",
      }),
      new ListItemBase({
        command: new SampleContentPage(),
        title: 'Sample content page',
        subtitle: 'Display mixed forms, markdown, and other types of content',
      }),
      new ListItemBase({
        command: new SamplePlainTextContentPage(),
        title: 'Sample plain text content page',
        subtitle: 'Display a page of plain text content',
      }),
      new ListItemBase({
        command: new SampleImageContentPage(),
        title: 'Sample image content page',
        subtitle: 'Display a page with an image',
      }),
      new ListItemBase({
        command: new SampleTreeContentPage(),
        title: 'Sample nested content',
        subtitle: 'Example of nesting a tree of content',
      }),
      new ListItemBase({
        command: new SampleCommentsPage(),
        title: 'Sample of nested comments',
        subtitle: 'Demo of using nested trees of content to create a comment thread-like experience',
        icon: icon('\uE90A'),
      }),
      new ListItemBase({
        command: new SampleMarkdownPage(),
        title: 'Markdown Page Sample Command',
        subtitle: 'Display a page of rendered markdown',
      }),
      new ListItemBase({
        command: new SampleMarkdownManyBodies(),
        title: 'Markdown with multiple blocks',
        subtitle: 'A page with multiple blocks of rendered markdown',
      }),
      new ListItemBase({
        command: new SampleMarkdownDetails(),
        title: 'Markdown with details',
        subtitle: 'A page with markdown and details',
      }),
      new ListItemBase({
        command: new SampleMarkdownImagesPage(),
        title: 'Markdown with images',
        subtitle: 'A page with rendered markdown and images',
        icon: icon('\uee71'),
      }),
      new ListItemBase({
        command: new SampleSettingsPage(),
        title: 'Sample settings page',
        subtitle: 'A demo of the settings helpers',
      }),
      new ListItemBase({
        command: new SampleDataTransferPage(),
        title: 'Clipboard and Drag-and-Drop Demo',
        subtitle: 'Demonstrates clipboard integration and drag-and-drop functionality',
      }),
    ];
  }
}
