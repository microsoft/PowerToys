// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { CommandItemBase, CommandProviderBase, run } from '@microsoft/cmdpal-sdk';
import type { ICommandItem } from '@microsoft/cmdpal-sdk';
import { icon } from './util.js';
import { SamplesListPage } from './samplesListPage.js';

/**
 * The provider for the JavaScript sample extension. Mirrors the C#
 * `SamplePagesCommandsProvider`, exposing a single top-level "Sample Pages"
 * command that opens the {@link SamplesListPage} index.
 */
class SampleProvider extends CommandProviderBase {
  readonly id = 'SampleJSExtension';
  readonly displayName = 'Sample Pages Commands (JS)';

  override icon = icon('\uE82D');

  private readonly samplesPage = new SamplesListPage();

  override topLevelCommands(): ICommandItem[] {
    return [
      new CommandItemBase({
        command: this.samplesPage,
        title: 'Sample Pages (JS)',
        subtitle: 'View example commands',
        icon: icon('\uE82D'),
      }),
    ];
  }
}

run(() => new SampleProvider());
