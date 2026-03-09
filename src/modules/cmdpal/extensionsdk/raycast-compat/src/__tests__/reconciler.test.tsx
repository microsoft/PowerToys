// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Tests for the custom React reconciler spike.
 *
 * Validates that:
 * 1. The reconciler captures React trees as VNode objects
 * 2. Marker components produce the correct VNode types
 * 3. Props flow through to VNode.props
 * 4. The translator maps VNode trees to CmdPal-compatible structures
 * 5. React state/hooks work correctly through the reconciler
 */

import React, { useState, useEffect } from 'react';
import {
  render,
  renderToVNodeTree,
  List,
  Detail,
  ActionPanel,
  Action,
  translateTree,
  isElementVNode,
} from '../index';
import type { VNode, AnyVNode } from '../index';

// ── Helper ────────────────────────────────────────────────────────────

function findVNode(nodes: AnyVNode[], type: string): VNode | undefined {
  for (const node of nodes) {
    if (isElementVNode(node) && node.type === type) return node;
    if (isElementVNode(node)) {
      const found = findVNode(node.children, type);
      if (found) return found;
    }
  }
  return undefined;
}

// ── Tests ─────────────────────────────────────────────────────────────

describe('Custom React Reconciler', () => {
  describe('VNode tree capture', () => {
    it('captures a simple List with List.Items', () => {
      const element = React.createElement(
        List,
        { isLoading: false },
        React.createElement(List.Item, { title: 'Hello', subtitle: 'World' }),
        React.createElement(List.Item, { title: 'Goodbye' }),
      );

      const tree = renderToVNodeTree(element);

      // Root should be a List
      expect(tree).toHaveLength(1);
      const root = tree[0] as VNode;
      expect(root.type).toBe('List');
      expect(root.props.isLoading).toBe(false);

      // Should have 2 List.Item children
      expect(root.children).toHaveLength(2);
      expect((root.children[0] as VNode).type).toBe('List.Item');
      expect((root.children[0] as VNode).props.title).toBe('Hello');
      expect((root.children[0] as VNode).props.subtitle).toBe('World');
      expect((root.children[1] as VNode).type).toBe('List.Item');
      expect((root.children[1] as VNode).props.title).toBe('Goodbye');
    });

    it('captures nested List.Section → List.Item', () => {
      const element = React.createElement(
        List,
        null,
        React.createElement(
          List.Section,
          { title: 'Recent' },
          React.createElement(List.Item, { title: 'Item A' }),
          React.createElement(List.Item, { title: 'Item B' }),
        ),
        React.createElement(
          List.Section,
          { title: 'All' },
          React.createElement(List.Item, { title: 'Item C' }),
        ),
      );

      const tree = renderToVNodeTree(element);
      const root = tree[0] as VNode;
      expect(root.type).toBe('List');
      expect(root.children).toHaveLength(2);

      const recent = root.children[0] as VNode;
      expect(recent.type).toBe('List.Section');
      expect(recent.props.title).toBe('Recent');
      expect(recent.children).toHaveLength(2);

      const all = root.children[1] as VNode;
      expect(all.type).toBe('List.Section');
      expect(all.props.title).toBe('All');
      expect(all.children).toHaveLength(1);
    });

    it('captures ActionPanel inside List.Item', () => {
      const onAction = jest.fn();
      const element = React.createElement(
        List,
        null,
        React.createElement(
          List.Item,
          { title: 'Test' },
          React.createElement(
            ActionPanel,
            null,
            React.createElement(Action, { title: 'Open', onAction }),
            React.createElement(Action.CopyToClipboard, { content: 'copied!' }),
          ),
        ),
      );

      const tree = renderToVNodeTree(element);
      const item = findVNode(tree, 'List.Item')!;
      expect(item).toBeDefined();
      expect(item.children).toHaveLength(1);

      const panel = item.children[0] as VNode;
      expect(panel.type).toBe('ActionPanel');
      expect(panel.children).toHaveLength(2);
      expect((panel.children[0] as VNode).type).toBe('Action');
      expect((panel.children[0] as VNode).props.title).toBe('Open');
      expect((panel.children[0] as VNode).props.onAction).toBe(onAction);
      expect((panel.children[1] as VNode).type).toBe('Action.CopyToClipboard');
      expect((panel.children[1] as VNode).props.content).toBe('copied!');
    });

    it('captures Detail with Metadata', () => {
      const element = React.createElement(
        Detail,
        { markdown: '# Hello World' },
        React.createElement(
          Detail.Metadata,
          null,
          React.createElement(Detail.Metadata.Label, {
            title: 'Author',
            text: 'Ash',
          }),
          React.createElement(Detail.Metadata.Separator, null),
          React.createElement(Detail.Metadata.Link, {
            title: 'Docs',
            target: 'https://example.com',
            text: 'View Docs',
          }),
        ),
      );

      const tree = renderToVNodeTree(element);
      const root = tree[0] as VNode;
      expect(root.type).toBe('Detail');
      expect(root.props.markdown).toBe('# Hello World');

      const metadata = root.children[0] as VNode;
      expect(metadata.type).toBe('Detail.Metadata');
      expect(metadata.children).toHaveLength(3);
      expect((metadata.children[0] as VNode).type).toBe('Detail.Metadata.Label');
      expect((metadata.children[0] as VNode).props.title).toBe('Author');
    });
  });

  describe('React hooks integration', () => {
    it('captures state-driven renders', () => {
      // A component that uses useState — verifies hooks work through our reconciler
      const StatefulList: React.FC = () => {
        const [items] = useState(['Alpha', 'Beta', 'Gamma']);
        return React.createElement(
          List,
          null,
          ...items.map((title) =>
            React.createElement(List.Item, { key: title, title }),
          ),
        );
      };

      const tree = renderToVNodeTree(React.createElement(StatefulList));
      const root = tree[0] as VNode;
      expect(root.type).toBe('List');
      expect(root.children).toHaveLength(3);
      expect((root.children[0] as VNode).props.title).toBe('Alpha');
      expect((root.children[1] as VNode).props.title).toBe('Beta');
      expect((root.children[2] as VNode).props.title).toBe('Gamma');
    });
  });

  describe('Commit callback (push-to-pull bridge)', () => {
    it('fires onCommit after React renders', () => {
      const onCommit = jest.fn();
      const element = React.createElement(
        List,
        null,
        React.createElement(List.Item, { title: 'Test' }),
      );

      const { container, unmount } = render(element, onCommit);

      // onCommit should have fired at least once from the initial render
      expect(onCommit).toHaveBeenCalled();
      expect(container.children).toHaveLength(1);
      expect((container.children[0] as VNode).type).toBe('List');

      unmount();
    });
  });

  describe('Translator: VNode → CmdPal types', () => {
    it('translates a List VNode tree to a TranslatedListPage', () => {
      const onSearch = jest.fn();
      const onAction = jest.fn();

      const element = React.createElement(
        List,
        { isLoading: false, searchBarPlaceholder: 'Search...', onSearchTextChange: onSearch },
        React.createElement(
          List.Section,
          { title: 'Results' },
          React.createElement(
            List.Item,
            {
              title: 'PowerToys',
              subtitle: 'Utilities',
              accessories: [{ text: 'v0.80' }],
            },
            React.createElement(
              ActionPanel,
              null,
              React.createElement(Action, { title: 'Open', onAction }),
            ),
          ),
          React.createElement(List.Item, { title: 'Terminal' }),
        ),
      );

      const tree = renderToVNodeTree(element);
      const page = translateTree(tree);

      expect(page).not.toBeNull();
      expect(page!.type).toBe('list');

      const listPage = page as {
        type: 'list';
        isLoading: boolean;
        searchBarPlaceholder?: string;
        items: Array<{
          title: string;
          subtitle?: string;
          section?: string;
          tags?: Array<{ text: string }>;
          actions: Array<{ title: string; type: string }>;
        }>;
        onSearchTextChange?: (text: string) => void;
      };

      expect(listPage.isLoading).toBe(false);
      expect(listPage.searchBarPlaceholder).toBe('Search...');
      expect(listPage.onSearchTextChange).toBe(onSearch);
      expect(listPage.items).toHaveLength(2);

      // First item — in "Results" section, with tags and actions
      const item0 = listPage.items[0];
      expect(item0.title).toBe('PowerToys');
      expect(item0.subtitle).toBe('Utilities');
      expect(item0.section).toBe('Results');
      expect(item0.tags).toEqual([{ text: 'v0.80' }]);
      expect(item0.actions).toHaveLength(1);
      expect(item0.actions[0].title).toBe('Open');
      expect(item0.actions[0].type).toBe('Action');

      // Second item — also in "Results" section
      const item1 = listPage.items[1];
      expect(item1.title).toBe('Terminal');
      expect(item1.section).toBe('Results');
      expect(item1.actions).toHaveLength(0);
    });

    it('translates a Detail VNode tree to a TranslatedDetailPage', () => {
      const element = React.createElement(
        Detail,
        { markdown: '# Readme\nSome content here' },
        React.createElement(
          Detail.Metadata,
          null,
          React.createElement(Detail.Metadata.Label, {
            title: 'Version',
            text: '1.0.0',
          }),
        ),
      );

      const tree = renderToVNodeTree(element);
      const page = translateTree(tree);

      expect(page).not.toBeNull();
      expect(page!.type).toBe('detail');

      const detailPage = page as {
        type: 'detail';
        markdown: string;
        isLoading: boolean;
        metadata: Array<{ key: string; value: unknown }>;
      };

      expect(detailPage.markdown).toBe('# Readme\nSome content here');
      expect(detailPage.isLoading).toBe(false);
      expect(detailPage.metadata).toHaveLength(1);
      expect(detailPage.metadata[0].key).toBe('Version');
      expect(detailPage.metadata[0].value).toBe('1.0.0');
    });

    it('returns null for unknown root types', () => {
      const tree = renderToVNodeTree(
        React.createElement('UnknownComponent', null),
      );
      const page = translateTree(tree);
      expect(page).toBeNull();
    });
  });
});
