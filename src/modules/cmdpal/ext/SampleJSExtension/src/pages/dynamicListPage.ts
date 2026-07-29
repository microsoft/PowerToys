// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { DynamicListPageBase, ListItemBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { Filters, IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

/**
 * A dynamic list page that rebuilds its items from the search text and offers
 * filters. Mirrors the C# `SampleDynamicListPage`.
 *
 * The host drives filters through the `listPage/setFilter` request, which the
 * runtime routes to a `setFilter` method when present.
 */
export class SampleDynamicListPage extends DynamicListPageBase {
  readonly id = 'sample-dynamic-list-page';
  readonly name = 'Dynamic List';
  readonly title = 'Dynamic List';

  override icon = icon('\uE721');

  override filters: Filters = {
    currentFilterId: 'all',
    filters: [
      { id: 'all', name: 'All' },
      { id: 'mod2', name: 'Every 2nd', icon: icon('2') },
      { id: 'mod3', name: 'Every 3rd (and long name)', icon: icon('3') },
    ],
  };

  override setSearchText(text: string): void {
    this.searchText = text;
    this.notifyItemsChanged();
  }

  setFilter(filterId: string): void {
    this.filters = { ...this.filters, currentFilterId: filterId };
    this.notifyItemsChanged();
  }

  override getItems(): IListItem[] {
    const chars = [...(this.searchText ?? '')];
    let items: IListItem[] = chars.map(
      (ch, index) =>
        new ListItemBase({ command: new NoOpCommand(`dyn-${index}`), title: ch }),
    );

    if (items.length === 0) {
      items = [
        new ListItemBase({ command: new NoOpCommand('dyn-empty'), title: 'Start typing in the search box' }),
      ];
    }

    switch (this.filters.currentFilterId) {
      case 'mod2':
        items = items.filter((_item, index) => (index + 1) % 2 === 0);
        break;
      case 'mod3':
        items = items.filter((_item, index) => (index + 1) % 3 === 0);
        break;
      default:
        break;
    }

    const first = items[0];
    if (first) {
      first.subtitle =
        'Notice how the number of items changes for this page when you type in the filter box';
    }

    return items;
  }
}
