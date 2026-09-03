// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { ListItemBase, ListPageBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';
import { glyphIcon } from '../util.js';

/**
 * A page that adds one entry each time it opens. It mirrors the load side of the
 * C# `OnLoadPage`.
 *
 * The JS protocol has no explicit page load event yet, so the sample appends a
 * "Loaded" entry when the host asks for items during page open.
 */
export class OnLoadPage extends ListPageBase {
  readonly id = 'on-load-page';
  readonly name = 'Open';
  readonly title = 'OnLoad sample';

  override icon = glyphIcon('\uE8AB');
  override placeholderText = 'This page changes each time you load it';
  override emptyContent = new ListItemBase({
    command: new NoOpCommand('on-load-empty'),
    title: 'This page starts empty',
    subtitle: 'but go back and open it again',
    icon: glyphIcon('\uE8AB'),
  });

  private readonly items: IListItem[] = [];

  override getItems(): IListItem[] {
    const now = new Date().toLocaleTimeString();
    this.items.push(
      new ListItemBase({
        command: new NoOpCommand(`on-load-${this.items.length}`),
        title: `Loaded ${now}`,
        icon: glyphIcon('\uECCB'),
      }),
    );
    return [...this.items];
  }
}
