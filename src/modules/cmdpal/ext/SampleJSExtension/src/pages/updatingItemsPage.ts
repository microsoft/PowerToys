// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { DynamicListPageBase, ListItemBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

/**
 * A page whose three items retitle themselves every half second with the
 * current hour, minute, and second. Mirrors the C# `SampleUpdatingItemsPage`.
 *
 * Approximation: like the live-details sample, the JS protocol has no targeted
 * item property push, so this extends `DynamicListPageBase` and refreshes via
 * `notifyItemsChanged()` on a timer.
 */
export class SampleUpdatingItemsPage extends DynamicListPageBase {
  readonly id = 'sample-updating-items-page';
  readonly name = 'Open';
  readonly title = 'List page with items that change';

  override icon = icon('\uE72C');

  private timer: NodeJS.Timeout | undefined;

  override setSearchText(): void {
    // This page updates on a timer rather than on search input.
  }

  override getItems(): IListItem[] {
    this.timer ??= setInterval(() => {
      this.notifyItemsChanged();
    }, 500);
    this.timer.unref?.();

    const now = new Date();
    return [
      new ListItemBase({ command: new NoOpCommand('clock-hour'), title: `${now.getHours()}` }),
      new ListItemBase({ command: new NoOpCommand('clock-minute'), title: `${now.getMinutes()}` }),
      new ListItemBase({ command: new NoOpCommand('clock-second'), title: `${now.getSeconds()}` }),
    ];
  }
}
