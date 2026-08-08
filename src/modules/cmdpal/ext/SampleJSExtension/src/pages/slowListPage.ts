// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ListItemBase, ListPageBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

/**
 * A list page that takes a few seconds to produce its items. Mirrors the C#
 * `SlowListPage`, using an async `getItems` (the SDK awaits it) instead of a
 * blocking sleep.
 */
export class SlowListPage extends ListPageBase {
  readonly id = 'slow-list-page';
  readonly name = 'Slow List Page';
  readonly title = 'This page simulates a slow load';

  override icon = icon('\uEA79');

  override async getItems(): Promise<IListItem[]> {
    await new Promise((resolve) => setTimeout(resolve, 5000));
    return [
      new ListItemBase({
        command: new NoOpCommand('slow-1'),
        title: 'This is a basic item in the list',
        subtitle: "I don't do anything though",
      }),
      new ListItemBase({
        command: new NoOpCommand('slow-2'),
        title: 'This is another item in the list',
        subtitle: 'Still nothing',
      }),
    ];
  }
}
