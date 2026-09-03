// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { CopyTextCommand, ListItemBase, ListPageBase, NoOpCommand } from '@microsoft/cmdpal-sdk';
import type { IListItem } from '@microsoft/cmdpal-sdk';
import { glyphIcon } from '../util.js';

/**
 * Clipboard sample based on the C# `SampleDataTransferPage`. Each item copies
 * its own value so you can test plain text transfer from the JS extension.
 */
export class SampleDataTransferPage extends ListPageBase {
  readonly id = 'sample-data-transfer-page';
  readonly name = 'Open';
  readonly title = 'Clipboard Demo';

  override icon = glyphIcon('\uE8C8');

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
        subtitle: 'The C# sample renders this lazily on drag; here it is copied when invoked',
      }),
      new ListItemBase({
        command: new NoOpCommand('data-transfer-image'),
        title: 'Item with an image',
        subtitle: 'The C# sample drags a bitmap and a file; image payloads are not supported from JS',
        icon: glyphIcon('\uEB9F'),
      }),
    ];
  }
}
