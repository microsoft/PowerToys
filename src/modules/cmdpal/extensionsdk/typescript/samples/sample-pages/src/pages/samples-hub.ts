// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  ListPage,
  ListItem,
  IListItem,
  ICommand,
  IconInfo,
  tag,
} from '@cmdpal/sdk';

/**
 * Hub page listing all sample pages in this extension.
 * Uses page instances directly as commands — CmdPal's navigation checks
 * `command is IPage` and navigates directly (no GoToPage needed).
 */
export class SamplesHubPage extends ListPage {
  id = 'samples-hub';
  name = 'TypeScript SDK Samples';
  placeholderText = 'Choose a sample...';

  private _pages: { title: string; subtitle: string; icon: string; command: ICommand; tag: string }[] = [];

  addPage(title: string, subtitle: string, icon: string, command: ICommand, tag: string): void {
    this._pages.push({ title, subtitle, icon, command, tag });
  }

  getItems(): IListItem[] {
    return this._pages.map((p) =>
      new ListItem({
        title: p.title,
        subtitle: p.subtitle,
        icon: IconInfo.fromGlyph(p.icon),
        command: p.command,
        tags: [tag(p.tag)],
      }),
    );
  }
}
