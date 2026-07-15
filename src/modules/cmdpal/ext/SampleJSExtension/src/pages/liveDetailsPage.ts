// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { DynamicListPageBase, ListItemBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

/**
 * A list page whose details pane updates once per second. Mirrors the intent of
 * the C# `SampleLiveDetailsPage`.
 *
 * Approximation: the JS `Details` type has no observable push. The C# page
 * relies on `Details` raising `INotifyPropertyChanged` so the pane refreshes
 * without reselecting. Here the page extends `DynamicListPageBase` and calls
 * `notifyItemsChanged()` on a timer, which asks the host to re-fetch the items
 * (and their rebuilt details). The details therefore refresh live, though
 * through a full item refresh rather than a targeted property change.
 */
export class SampleLiveDetailsPage extends DynamicListPageBase {
  readonly id = 'sample-live-details-page';
  readonly name = 'Live Updating Details';
  readonly title = 'Live Updating Details';

  override icon = icon('\uE916');
  override showDetails = true;

  private timer: NodeJS.Timeout | undefined;
  private counter = 0;

  override setSearchText(): void {
    // The live details demo does not filter on search text.
  }

  override getItems(): IListItem[] {
    this.timer ??= setInterval(() => {
      this.counter += 1;
      this.notifyItemsChanged();
    }, 1000);
    this.timer.unref?.();

    const now = new Date().toLocaleTimeString();
    const seconds = this.counter === 1 ? 'second' : 'seconds';

    return [
      new ListItemBase({
        command: new NoOpCommand('live-clock'),
        title: 'Live Clock',
        subtitle: 'Details pane shows current time, updating every second',
        details: { title: 'Current Time', body: now },
      }),
      new ListItemBase({
        command: new NoOpCommand('live-counter'),
        title: 'Counter',
        subtitle: 'Details pane increments a counter every second',
        details: { title: `Count: ${this.counter}`, body: `Elapsed: ${this.counter} ${seconds}` },
      }),
      new ListItemBase({
        command: new NoOpCommand('live-static'),
        title: 'Static Item',
        subtitle: "This item's details do not change",
        details: {
          title: 'Static Details',
          body: 'This item does not update. Select the items above to see live updates in the details pane.',
        },
      }),
    ];
  }
}
