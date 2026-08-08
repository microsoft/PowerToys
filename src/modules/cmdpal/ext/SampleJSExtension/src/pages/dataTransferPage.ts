// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { CopyTextCommand, ListItemBase, ListPageBase } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

/**
 * A demo of clipboard integration. Mirrors the clipboard portion of the C#
 * `SampleDataTransferPage`. Each item exposes a copy-to-clipboard command.
 */
export class SampleDataTransferPage extends ListPageBase {
  readonly id = 'sample-data-transfer-page';
  readonly name = 'Open';
  readonly title = 'Clipboard Demo';

  override icon = icon('\uE8C8');

  override getItems(): IListItem[] {
    return [
      new ListItemBase({
        command: new CopyTextCommand('Text data in the Data Package', 'Copy text', 'Copied text'),
        title: 'Item with plain text',
        subtitle: 'Copy plain text to the clipboard',
      }),
      new ListItemBase({
        command: new CopyTextCommand(new Date().toLocaleString(), 'Copy timestamp', 'Copied timestamp'),
        title: 'Item with a lazily rendered plain text',
        subtitle: 'The value is captured when the page loads and copied when invoked',
      }),
    ];
  }
}
