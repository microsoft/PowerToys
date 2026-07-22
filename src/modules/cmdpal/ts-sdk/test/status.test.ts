// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import { describe, expect, it } from 'vitest';
import { createHostBridge } from '../src/runtime/server.js';

interface Notification {
  method: string;
  params: Record<string, unknown>;
}

function createBridge(): { bridge: ReturnType<typeof createHostBridge>; sent: Notification[] } {
  const sent: Notification[] = [];
  const bridge = createHostBridge((method, params) => {
    sent.push({ method, params: (params ?? {}) as Record<string, unknown> });
  });
  return { bridge, sent };
}

describe('client-side status identity', () => {
  it('mints a statusId and carries both id and message text', () => {
    const { bridge, sent } = createBridge();

    const statusId = bridge.showStatus('Working', 'info', { current: 1, total: 4 });

    expect(statusId).toBe('status-1');
    expect(sent).toHaveLength(1);
    expect(sent[0]?.method).toBe('host/showStatus');
    expect(sent[0]?.params).toMatchObject({
      statusId: 'status-1',
      message: { Message: 'Working', State: 0 },
      progress: { current: 1, total: 4 },
    });
  });

  it('mints a distinct id for each shown status', () => {
    const { bridge } = createBridge();
    expect(bridge.showStatus('one')).toBe('status-1');
    expect(bridge.showStatus('two')).toBe('status-2');
  });

  it('updates an existing status by id without duplicating it', () => {
    const { bridge, sent } = createBridge();
    const statusId = bridge.showStatus('Working', 'info', { current: 1, total: 4 });

    bridge.updateStatus(statusId, 'Almost done', 'warning', { current: 3, total: 4 });

    const shows = sent.filter((n) => n.method === 'host/showStatus');
    expect(shows).toHaveLength(2);
    expect(shows[1]?.params).toMatchObject({
      statusId: 'status-1',
      message: { Message: 'Almost done', State: 2 },
      progress: { current: 3, total: 4 },
    });
  });

  it('hides a status by id and echoes the tracked message', () => {
    const { bridge, sent } = createBridge();
    const statusId = bridge.showStatus('Done', 'success');

    bridge.hideStatus(statusId);

    const hides = sent.filter((n) => n.method === 'host/hideStatus');
    expect(hides).toHaveLength(1);
    expect(hides[0]?.params).toMatchObject({
      statusId: 'status-1',
      message: { Message: 'Done', State: 1 },
    });
  });
});
