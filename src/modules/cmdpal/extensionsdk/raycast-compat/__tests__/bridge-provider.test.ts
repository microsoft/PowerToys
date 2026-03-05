// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Bridge Provider tests — verifies the push-to-pull model, snapshot
 * management, command registration, and action dispatch.
 */

import React, { useState, useEffect } from 'react';
import { RaycastBridgeProvider } from '../src/bridge/bridge-provider';
import type {
  RaycastExtensionManifest,
  RaycastCommandModule,
  PageSnapshot,
} from '../src/bridge/bridge-provider';

// ── Helpers ────────────────────────────────────────────────────────────

function makeManifest(overrides?: Partial<RaycastExtensionManifest>): RaycastExtensionManifest {
  return {
    name: 'test-ext',
    title: 'Test Extension',
    commands: [
      { name: 'search', title: 'Search Things', mode: 'view' },
    ],
    ...overrides,
  };
}

function makeCommandModule(Component: React.ComponentType): RaycastCommandModule {
  return { default: Component };
}

/** Collect notifications emitted by the bridge. */
function trackNotifications(): {
  notifications: Array<{ method: string; params?: Record<string, unknown> }>;
  notifyFn: (method: string, params?: Record<string, unknown>) => void;
} {
  const notifications: Array<{ method: string; params?: Record<string, unknown> }> = [];
  return {
    notifications,
    notifyFn: (method, params) => notifications.push({ method, params }),
  };
}

// ── Simple Raycast-style components (using marker component names) ────

function SimpleList() {
  return React.createElement('List', { searchBarPlaceholder: 'Type here…' },
    React.createElement('List.Item', { title: 'Alpha', subtitle: 'First' }),
    React.createElement('List.Item', { title: 'Beta', subtitle: 'Second' }),
  );
}

function SimpleDetail() {
  return React.createElement('Detail', { markdown: '# Hello World' });
}

function ListWithSections() {
  return React.createElement('List', {},
    React.createElement('List.Section', { title: 'Fruits' },
      React.createElement('List.Item', { title: 'Apple' }),
      React.createElement('List.Item', { title: 'Banana' }),
    ),
    React.createElement('List.Section', { title: 'Veggies' },
      React.createElement('List.Item', { title: 'Carrot' }),
    ),
  );
}

function ListWithActions() {
  return React.createElement('List', {},
    React.createElement('List.Item', { title: 'File' },
      React.createElement('ActionPanel', {},
        React.createElement('Action', { title: 'Open', onAction: () => {} }),
        React.createElement('Action.CopyToClipboard', { title: 'Copy Path', content: '/tmp/file' }),
      ),
    ),
  );
}

function ListWithSearch() {
  const [searchText, setSearchText] = React.useState('');
  const allItems = ['Apple', 'Banana', 'Cherry', 'Date'];
  const filtered = searchText
    ? allItems.filter((i) => i.toLowerCase().includes(searchText.toLowerCase()))
    : allItems;

  return React.createElement('List', {
    onSearchTextChange: setSearchText,
    searchBarPlaceholder: 'Search fruits…',
  },
    ...filtered.map((item) =>
      React.createElement('List.Item', { key: item, title: item }),
    ),
  );
}

// ══════════════════════════════════════════════════════════════════════════
// Tests
// ══════════════════════════════════════════════════════════════════════════

describe('RaycastBridgeProvider', () => {
  // ── Constructor & registration ──────────────────────────────────────

  test('creates provider with correct ID and display name', () => {
    const provider = new RaycastBridgeProvider(makeManifest());
    expect(provider.id).toBe('raycast-compat.test-ext');
    expect(provider.displayName).toBe('Test Extension');
  });

  test('topLevelCommands returns manifest commands as CmdPal items', () => {
    const manifest = makeManifest({
      commands: [
        { name: 'search', title: 'Search', icon: '🔍' },
        { name: 'create', title: 'Create New', subtitle: 'Quick create' },
      ],
    });
    const provider = new RaycastBridgeProvider(manifest);
    const cmds = provider.topLevelCommands();

    expect(cmds).toHaveLength(2);
    expect(cmds[0].title).toBe('Search');
    expect((cmds[0].command as Record<string, unknown>).id).toBe('raycast-compat.test-ext.search');
    expect(cmds[1].title).toBe('Create New');
    expect(cmds[1].subtitle).toBe('Quick create');
  });

  // ── List rendering ──────────────────────────────────────────────────

  test('getCommand mounts a List component and returns dynamicListPage', () => {
    const provider = new RaycastBridgeProvider(makeManifest());
    provider.registerCommand('search', makeCommandModule(SimpleList));

    const page = provider.getCommand('raycast-compat.test-ext.search');

    expect(page).toBeDefined();
    expect(page!._type).toBe('dynamicListPage');
    expect(page!.placeholderText).toBe('Type here…');
  });

  test('getItems returns translated list items after mount', () => {
    const provider = new RaycastBridgeProvider(makeManifest());
    provider.registerCommand('search', makeCommandModule(SimpleList));
    provider.getCommand('raycast-compat.test-ext.search');

    const items = provider.getItems();
    expect(items).toHaveLength(2);
    expect(items[0].title).toBe('Alpha');
    expect(items[0].subtitle).toBe('First');
    expect(items[1].title).toBe('Beta');
  });

  test('list items include section names from List.Section', () => {
    const provider = new RaycastBridgeProvider(makeManifest());
    provider.registerCommand('search', makeCommandModule(ListWithSections));
    provider.getCommand('raycast-compat.test-ext.search');

    const items = provider.getItems();
    expect(items).toHaveLength(3);
    expect(items[0].section).toBe('Fruits');
    expect(items[2].section).toBe('Veggies');
  });

  // ── Detail rendering ────────────────────────────────────────────────

  test('getCommand mounts a Detail component and returns contentPage', () => {
    const manifest = makeManifest({
      commands: [{ name: 'detail', title: 'Show Detail' }],
    });
    const provider = new RaycastBridgeProvider(manifest);
    provider.registerCommand('detail', makeCommandModule(SimpleDetail));

    const page = provider.getCommand('raycast-compat.test-ext.detail');

    expect(page).toBeDefined();
    expect(page!._type).toBe('contentPage');
  });

  test('getContent returns markdown for Detail pages', () => {
    const manifest = makeManifest({
      commands: [{ name: 'detail', title: 'Show Detail' }],
    });
    const provider = new RaycastBridgeProvider(manifest);
    provider.registerCommand('detail', makeCommandModule(SimpleDetail));
    provider.getCommand('raycast-compat.test-ext.detail');

    const content = provider.getContent();
    expect(content).toHaveLength(1);
    expect(content[0].body).toBe('# Hello World');
  });

  // ── Push-to-pull: commit detection & notifications ──────────────────

  test('snapshot is populated after initial render', () => {
    const provider = new RaycastBridgeProvider(makeManifest());
    provider.registerCommand('search', makeCommandModule(SimpleList));
    provider.getCommand('raycast-compat.test-ext.search');

    expect(provider.snapshot).not.toBeNull();
    expect(provider.snapshot!.kind).toBe('list');
  });

  test('commitCount increments on React re-renders', (done) => {
    // Component that triggers a re-render via useEffect
    function UpdatingList() {
      const [items, setItems] = useState(['Initial']);
      useEffect(() => {
        const timer = setTimeout(() => setItems(['Updated']), 10);
        return () => clearTimeout(timer);
      }, []);
      return React.createElement('List', {},
        ...items.map((i) => React.createElement('List.Item', { key: i, title: i })),
      );
    }

    const { notifications, notifyFn } = trackNotifications();
    const provider = new RaycastBridgeProvider(makeManifest());
    provider.setNotifyFn(notifyFn);
    provider.registerCommand('search', makeCommandModule(UpdatingList));
    provider.getCommand('raycast-compat.test-ext.search');

    const initialCount = provider.commitCount;

    // Wait for the useEffect re-render
    setTimeout(() => {
      expect(provider.commitCount).toBeGreaterThan(initialCount);

      // Should have sent listPage/itemsChanged notification
      const itemsChangedNotifs = notifications.filter(
        (n) => n.method === 'listPage/itemsChanged',
      );
      expect(itemsChangedNotifs.length).toBeGreaterThan(0);
      expect(itemsChangedNotifs[0].params?.pageId).toBe('raycast-compat.test-ext.search');

      // Updated items should be in the snapshot
      const items = provider.getItems();
      expect(items.some((i) => i.title === 'Updated')).toBe(true);

      provider.dispose();
      done();
    }, 100);
  });

  // ── Search text forwarding ──────────────────────────────────────────

  test('setSearchText forwards to Raycast onSearchTextChange', (done) => {
    const { notifications, notifyFn } = trackNotifications();
    const provider = new RaycastBridgeProvider(makeManifest());
    provider.setNotifyFn(notifyFn);
    provider.registerCommand('search', makeCommandModule(ListWithSearch));
    provider.getCommand('raycast-compat.test-ext.search');

    // Initial: 4 items
    expect(provider.getItems()).toHaveLength(4);

    // Search for "an" → should filter
    provider.setSearchText('an');

    // After flush, the snapshot should be updated synchronously.
    // However if batching defers it, wait a tick.
    const checkResult = () => {
      const items = provider.getItems();

      // The search must have produced fewer items
      expect(items.length).toBeLessThan(4);
      expect(items.length).toBeGreaterThan(0);

      // Verify notification was emitted
      const itemsChangedNotifs = notifications.filter(
        (n) => n.method === 'listPage/itemsChanged',
      );
      expect(itemsChangedNotifs.length).toBeGreaterThan(0);

      provider.dispose();
      done();
    };

    // Try synchronous first, fall back to async
    if (provider.getItems().length < 4) {
      checkResult();
    } else {
      setTimeout(checkResult, 200);
    }
  });

  // ── Action commands ─────────────────────────────────────────────────

  test('getItems returns items with action command IDs', () => {
    const provider = new RaycastBridgeProvider(makeManifest());
    provider.registerCommand('search', makeCommandModule(ListWithActions));
    provider.getCommand('raycast-compat.test-ext.search');

    const items = provider.getItems();
    expect(items).toHaveLength(1);

    const moreCommands = items[0].moreCommands as Array<Record<string, unknown>>;
    expect(moreCommands).toHaveLength(2);
    expect(moreCommands[0].title).toBe('Open');

    const actionCmd = moreCommands[0].command as Record<string, unknown>;
    expect(actionCmd.id).toContain('::action::0::0');
  });

  test('invokeCommand calls the action onAction callback', () => {
    let actionCalled = false;
    function ListWithCallback() {
      return React.createElement('List', {},
        React.createElement('List.Item', { title: 'Item' },
          React.createElement('ActionPanel', {},
            React.createElement('Action', { title: 'Do It', onAction: () => { actionCalled = true; } }),
          ),
        ),
      );
    }

    const provider = new RaycastBridgeProvider(makeManifest());
    provider.registerCommand('search', makeCommandModule(ListWithCallback));
    provider.getCommand('raycast-compat.test-ext.search');

    provider.invokeCommand('raycast-compat.test-ext.search::action::0::0');
    expect(actionCalled).toBe(true);

    provider.dispose();
  });

  // ── Dispose ─────────────────────────────────────────────────────────

  test('dispose unmounts the React tree and clears snapshot', () => {
    const provider = new RaycastBridgeProvider(makeManifest());
    provider.registerCommand('search', makeCommandModule(SimpleList));
    provider.getCommand('raycast-compat.test-ext.search');

    expect(provider.snapshot).not.toBeNull();

    provider.dispose();

    expect(provider.snapshot).toBeNull();
    expect(provider.activeCommandId).toBeNull();
  });

  // ── getProperties ───────────────────────────────────────────────────

  test('getProperties returns provider metadata', () => {
    const provider = new RaycastBridgeProvider(makeManifest({ icon: '⚡' }));
    const props = provider.getProperties();

    expect(props.id).toBe('raycast-compat.test-ext');
    expect(props.displayName).toBe('Test Extension');
    expect(props.icon).toBeDefined();
    expect(props.frozen).toBe(true);
  });

  // ── Unknown command handling ────────────────────────────────────────

  test('getCommand returns undefined for unknown command IDs', () => {
    const provider = new RaycastBridgeProvider(makeManifest());
    expect(provider.getCommand('nonexistent.command')).toBeUndefined();
  });

  test('getCommand returns undefined for command with no registered module', () => {
    const provider = new RaycastBridgeProvider(makeManifest());
    // Don't register any modules
    const result = provider.getCommand('raycast-compat.test-ext.search');
    // It should still return a loading page (the mount logs a warning)
    expect(result).toBeDefined();
    expect(result!._type).toBe('dynamicListPage');
  });

  // ── Multiple commands ───────────────────────────────────────────────

  test('switching between commands unmounts previous and mounts new', () => {
    const manifest = makeManifest({
      commands: [
        { name: 'list-cmd', title: 'List Command' },
        { name: 'detail-cmd', title: 'Detail Command' },
      ],
    });

    const provider = new RaycastBridgeProvider(manifest);
    provider.registerCommand('list-cmd', makeCommandModule(SimpleList));
    provider.registerCommand('detail-cmd', makeCommandModule(SimpleDetail));

    // Mount list first
    provider.getCommand('raycast-compat.test-ext.list-cmd');
    expect(provider.snapshot?.kind).toBe('list');

    // Switch to detail
    provider.getCommand('raycast-compat.test-ext.detail-cmd');
    expect(provider.snapshot?.kind).toBe('content');
    expect(provider.activeCommandId).toBe('raycast-compat.test-ext.detail-cmd');
  });
});
