// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { afterEach, describe, expect, it } from 'vitest';
import { CommandProviderBase } from '../src/base/CommandProviderBase.js';
import { setNotificationSink } from '../src/runtime/notifications.js';

class TestCommandProvider extends CommandProviderBase {
  readonly id = 'test-provider';
  readonly displayName = 'Test Provider';

  topLevelCommands() {
    return [];
  }

  refresh(): void {
    this.notifyItemsChanged();
  }
}

describe('CommandProviderBase', () => {
  afterEach(() => {
    setNotificationSink(null);
  });

  it('notifies the host when provider items change', () => {
    const sent: Array<{ method: string; params: unknown }> = [];
    setNotificationSink((method, params) => sent.push({ method, params }));

    new TestCommandProvider().refresh();

    expect(sent).toEqual([
      {
        method: 'provider/itemsChanged',
        params: { totalItems: -1 },
      },
    ]);
  });
});
