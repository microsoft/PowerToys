// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ListItemBase, ListPageBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

/**
 * A page that grows by one entry every time it is opened. Mirrors the intent of
 * the C# `OnLoadPage`.
 *
 * Approximation: the JS protocol exposes no page load/unload lifecycle events
 * (the C# page hooks the `ItemsChanged` add/remove accessors). Here a "Loaded"
 * entry is appended each time the host fetches the items, which happens on open.
 */
export class OnLoadPage extends ListPageBase {
  readonly id = 'on-load-page';
  readonly name = 'Open';
  readonly title = 'Load/Unload sample';

  override icon = icon('\uE8AB');
  override placeholderText = 'This page changes each time you load it';
  override emptyContent = new ListItemBase({
    command: new NoOpCommand('on-load-empty'),
    title: 'This page starts empty',
    subtitle: 'but go back and open it again',
    icon: icon('\uE8AB'),
  });

  private readonly items: IListItem[] = [];

  override getItems(): IListItem[] {
    const now = new Date().toLocaleTimeString();
    this.items.push(
      new ListItemBase({
        command: new NoOpCommand(`on-load-${this.items.length}`),
        title: `Loaded ${now}`,
        icon: icon('\uECCB'),
      }),
    );
    return [...this.items];
  }
}
