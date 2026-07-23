// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { CopyTextCommand, ListItemBase, ListPageBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';
import { icon } from '../util.js';

/**
 * A demo of clipboard integration. Mirrors the C# `SampleDataTransferPage`.
 *
 * Not-yet-supported: the C# page attaches a `DataPackage` to each list item to
 * enable drag and drop (including delayed and image payloads). `IListItem` in
 * the JS protocol has no `DataPackage`, so drag and drop is omitted and the
 * text items expose a copy-to-clipboard command instead.
 */
export class SampleDataTransferPage extends ListPageBase {
  readonly id = 'sample-data-transfer-page';
  readonly name = 'Open';
  readonly title = 'Clipboard and Drag-and-Drop Demo';

  override icon = icon('\uE8C8');

  override getItems(): IListItem[] {
    return [
      new ListItemBase({
        command: new CopyTextCommand('Text data in the Data Package', 'Copy text', 'Copied text'),
        title: 'Item with plain text',
        subtitle: 'Copy plain text to the clipboard (drag and drop is not supported from JS)',
      }),
      new ListItemBase({
        command: new CopyTextCommand(new Date().toLocaleString(), 'Copy timestamp', 'Copied timestamp'),
        title: 'Item with a lazily rendered plain text',
        subtitle: 'The C# sample renders this lazily on drag; here it is copied when invoked',
      }),
      new ListItemBase({
        command: new NoOpCommand('data-transfer-image'),
        title: 'Item with an image',
        subtitle: 'The C# sample drags a bitmap and a file; image payloads are not supported from JS',
        icon: icon('\uEB9F'),
      }),
    ];
  }
}
