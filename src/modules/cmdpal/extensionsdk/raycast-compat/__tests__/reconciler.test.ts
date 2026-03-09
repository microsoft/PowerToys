// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Test specifications: React reconciler → VNode capture.
 *
 * These tests define the expected behavior of the custom React reconciler
 * that Ash is building. The reconciler renders React component trees into
 * VNode objects (plain JS trees) that represent the Raycast-style component
 * hierarchy.
 *
 * STATUS: Specification-only. Tests will fail until the reconciler
 *         implementation is wired up. That is intentional — these serve
 *         as acceptance criteria.
 */

import type { VNode, ReconcilerRenderer } from './types';

// ---------------------------------------------------------------------------
// The actual reconciler + Raycast component stubs will be imported from
// Ash's implementation once it lands. For now we reference placeholder paths.
// Uncomment / adjust these once the modules exist:
//
//   import { createReconciler } from '../reconciler';
//   import { List, Detail, Form, ActionPanel, Action } from '../components';
// ---------------------------------------------------------------------------

// Placeholder factory — replace with real createReconciler() import.
function createReconciler(): ReconcilerRenderer {
  throw new Error(
    'Reconciler not yet implemented. Replace this stub with the real import.',
  );
}

// Placeholder React-like element helpers.
// Once Ash's Raycast component shims exist, replace these with real JSX.
function el(
  type: string,
  props: Record<string, unknown> = {},
  ...children: unknown[]
): React.ReactElement {
  return { type, props: { ...props, children }, key: null } as unknown as React.ReactElement;
}

// ============================================================================
// 1. EMPTY / TRIVIAL RENDERS
// ============================================================================

describe('Reconciler — empty and trivial renders', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('empty component renders an empty container VNode', () => {
    // A component that returns null should produce a container with no children.
    const NullComponent = () => null;
    const root = renderer.render(el(NullComponent.name));

    expect(root).toBeDefined();
    expect(root.children).toHaveLength(0);
  });

  test('fragment with no children produces empty container', () => {
    const root = renderer.render(el('Fragment'));

    expect(root).toBeDefined();
    expect(root.children).toHaveLength(0);
  });
});

// ============================================================================
// 2. LIST COMPONENT TREES
// ============================================================================

describe('Reconciler — List component VNode capture', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('single List with Items produces correct VNode tree', () => {
    // <List navigationTitle="Search">
    //   <List.Item title="Hello" subtitle="World" />
    //   <List.Item title="Foo" icon="star.png" />
    // </List>
    const tree = el(
      'List',
      { navigationTitle: 'Search' },
      el('List.Item', { title: 'Hello', subtitle: 'World' }),
      el('List.Item', { title: 'Foo', icon: 'star.png' }),
    );

    const root = renderer.render(tree);

    expect(root.type).toBe('List');
    expect(root.props.navigationTitle).toBe('Search');
    expect(root.children).toHaveLength(2);

    expect(root.children[0].type).toBe('List.Item');
    expect(root.children[0].props.title).toBe('Hello');
    expect(root.children[0].props.subtitle).toBe('World');

    expect(root.children[1].type).toBe('List.Item');
    expect(root.children[1].props.title).toBe('Foo');
    expect(root.children[1].props.icon).toBe('star.png');
  });

  test('List.Item with accessories array preserves accessory props', () => {
    const tree = el(
      'List',
      {},
      el('List.Item', {
        title: 'Result',
        accessories: [
          { text: '3 days ago', tooltip: 'Last modified' },
          { icon: 'checkmark.png' },
        ],
      }),
    );

    const root = renderer.render(tree);
    const item = root.children[0];

    expect(item.type).toBe('List.Item');
    expect(item.props.accessories).toEqual([
      { text: '3 days ago', tooltip: 'Last modified' },
      { icon: 'checkmark.png' },
    ]);
  });

  test('List with isLoading prop captured', () => {
    const tree = el('List', { isLoading: true, navigationTitle: 'Loading…' });
    const root = renderer.render(tree);

    expect(root.type).toBe('List');
    expect(root.props.isLoading).toBe(true);
  });

  test('List.Section groups children correctly', () => {
    const tree = el(
      'List',
      {},
      el(
        'List.Section',
        { title: 'Recent' },
        el('List.Item', { title: 'Item A' }),
        el('List.Item', { title: 'Item B' }),
      ),
      el(
        'List.Section',
        { title: 'Favorites' },
        el('List.Item', { title: 'Item C' }),
      ),
    );

    const root = renderer.render(tree);

    expect(root.children).toHaveLength(2);
    expect(root.children[0].type).toBe('List.Section');
    expect(root.children[0].props.title).toBe('Recent');
    expect(root.children[0].children).toHaveLength(2);
    expect(root.children[1].type).toBe('List.Section');
    expect(root.children[1].children).toHaveLength(1);
  });

  test('List.Item with keywords prop for search filtering', () => {
    const tree = el(
      'List',
      {},
      el('List.Item', { title: 'Visual Studio Code', keywords: ['vscode', 'editor'] }),
    );

    const root = renderer.render(tree);
    expect(root.children[0].props.keywords).toEqual(['vscode', 'editor']);
  });
});

// ============================================================================
// 3. DETAIL COMPONENT TREES
// ============================================================================

describe('Reconciler — Detail component VNode capture', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('Detail with markdown content produces correct VNode', () => {
    const markdown = '# Hello\n\nSome **bold** text.';
    const tree = el(
      'Detail',
      { navigationTitle: 'Info' },
      el('Detail.Markdown', { content: markdown }),
    );

    const root = renderer.render(tree);

    expect(root.type).toBe('Detail');
    expect(root.props.navigationTitle).toBe('Info');
    expect(root.children).toHaveLength(1);
    expect(root.children[0].type).toBe('Detail.Markdown');
    expect(root.children[0].props.content).toBe(markdown);
  });

  test('Detail with metadata sidebar', () => {
    const tree = el(
      'Detail',
      {},
      el('Detail.Markdown', { content: '# Doc' }),
      el(
        'Detail.Metadata',
        {},
        el('Detail.Metadata.Label', { title: 'Version', text: '1.0.0' }),
        el('Detail.Metadata.Link', { title: 'Docs', target: 'https://example.com', text: 'Link' }),
        el('Detail.Metadata.Separator'),
        el('Detail.Metadata.TagList', { title: 'Tags' },
          el('Detail.Metadata.TagList.Item', { text: 'stable', color: 'green' }),
        ),
      ),
    );

    const root = renderer.render(tree);
    const metadata = root.children.find(c => c.type === 'Detail.Metadata');

    expect(metadata).toBeDefined();
    expect(metadata!.children.length).toBeGreaterThanOrEqual(3);
    expect(metadata!.children[0].type).toBe('Detail.Metadata.Label');
    expect(metadata!.children[0].props.text).toBe('1.0.0');
  });

  test('Detail with isLoading flag', () => {
    const tree = el('Detail', { isLoading: true });
    const root = renderer.render(tree);

    expect(root.type).toBe('Detail');
    expect(root.props.isLoading).toBe(true);
    expect(root.children).toHaveLength(0);
  });
});

// ============================================================================
// 4. FORM COMPONENT TREES
// ============================================================================

describe('Reconciler — Form component VNode capture', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('Form with text fields produces correct VNode', () => {
    const tree = el(
      'Form',
      { navigationTitle: 'Create Item' },
      el('Form.TextField', { id: 'name', title: 'Name', placeholder: 'Enter name' }),
      el('Form.TextArea', { id: 'desc', title: 'Description' }),
      el('Form.Checkbox', { id: 'active', title: 'Active', defaultValue: true }),
    );

    const root = renderer.render(tree);

    expect(root.type).toBe('Form');
    expect(root.children).toHaveLength(3);
    expect(root.children[0].type).toBe('Form.TextField');
    expect(root.children[0].props.id).toBe('name');
    expect(root.children[0].props.placeholder).toBe('Enter name');
    expect(root.children[2].type).toBe('Form.Checkbox');
    expect(root.children[2].props.defaultValue).toBe(true);
  });

  test('Form.Dropdown with items', () => {
    const tree = el(
      'Form',
      {},
      el(
        'Form.Dropdown',
        { id: 'priority', title: 'Priority', defaultValue: 'medium' },
        el('Form.Dropdown.Item', { value: 'low', title: 'Low' }),
        el('Form.Dropdown.Item', { value: 'medium', title: 'Medium' }),
        el('Form.Dropdown.Item', { value: 'high', title: 'High' }),
      ),
    );

    const root = renderer.render(tree);
    const dropdown = root.children[0];

    expect(dropdown.type).toBe('Form.Dropdown');
    expect(dropdown.props.defaultValue).toBe('medium');
    expect(dropdown.children).toHaveLength(3);
    expect(dropdown.children[1].props.title).toBe('Medium');
  });
});

// ============================================================================
// 5. ACTION PANEL TREES
// ============================================================================

describe('Reconciler — ActionPanel VNode capture', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('ActionPanel with basic actions', () => {
    const noop = () => {};
    const tree = el(
      'ActionPanel',
      {},
      el('Action', { title: 'Open', onAction: noop }),
      el('Action.CopyToClipboard', { content: 'Hello clipboard' }),
      el('Action.OpenInBrowser', { url: 'https://github.com' }),
    );

    const root = renderer.render(tree);

    expect(root.type).toBe('ActionPanel');
    expect(root.children).toHaveLength(3);
    expect(root.children[0].type).toBe('Action');
    expect(root.children[0].props.title).toBe('Open');
    expect(root.children[1].type).toBe('Action.CopyToClipboard');
    expect(root.children[1].props.content).toBe('Hello clipboard');
    expect(root.children[2].type).toBe('Action.OpenInBrowser');
    expect(root.children[2].props.url).toBe('https://github.com');
  });

  test('ActionPanel.Section groups actions', () => {
    const tree = el(
      'ActionPanel',
      {},
      el(
        'ActionPanel.Section',
        { title: 'Primary' },
        el('Action', { title: 'Run' }),
      ),
      el(
        'ActionPanel.Section',
        { title: 'Secondary' },
        el('Action', { title: 'Copy' }),
        el('Action', { title: 'Share' }),
      ),
    );

    const root = renderer.render(tree);

    expect(root.children).toHaveLength(2);
    expect(root.children[0].type).toBe('ActionPanel.Section');
    expect(root.children[0].children).toHaveLength(1);
    expect(root.children[1].children).toHaveLength(2);
  });

  test('Action with keyboard shortcut prop', () => {
    const tree = el(
      'ActionPanel',
      {},
      el('Action', {
        title: 'Refresh',
        shortcut: { modifiers: ['cmd'], key: 'r' },
      }),
    );

    const root = renderer.render(tree);
    const action = root.children[0];

    expect(action.props.shortcut).toEqual({ modifiers: ['cmd'], key: 'r' });
  });
});

// ============================================================================
// 6. DYNAMIC RE-RENDERS (STATE CHANGES)
// ============================================================================

describe('Reconciler — dynamic re-render updates VNode tree', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('re-rendering with new children updates the VNode', () => {
    // First render: 2 items
    const tree1 = el(
      'List',
      {},
      el('List.Item', { title: 'A' }),
      el('List.Item', { title: 'B' }),
    );
    const root1 = renderer.render(tree1);
    expect(root1.children).toHaveLength(2);

    // Second render: 3 items (simulate state update adding an item)
    const tree2 = el(
      'List',
      {},
      el('List.Item', { title: 'A' }),
      el('List.Item', { title: 'B' }),
      el('List.Item', { title: 'C' }),
    );
    const root2 = renderer.render(tree2);
    expect(root2.children).toHaveLength(3);
    expect(root2.children[2].props.title).toBe('C');
  });

  test('re-rendering with changed props updates existing VNodes', () => {
    const tree1 = el('List', {}, el('List.Item', { title: 'Old Title' }));
    renderer.render(tree1);

    const tree2 = el('List', {}, el('List.Item', { title: 'New Title' }));
    const root = renderer.render(tree2);

    expect(root.children[0].props.title).toBe('New Title');
  });

  test('removing a child updates children array', () => {
    const tree1 = el(
      'List',
      {},
      el('List.Item', { title: 'Keep' }),
      el('List.Item', { title: 'Remove' }),
    );
    renderer.render(tree1);

    const tree2 = el('List', {}, el('List.Item', { title: 'Keep' }));
    const root = renderer.render(tree2);

    expect(root.children).toHaveLength(1);
    expect(root.children[0].props.title).toBe('Keep');
  });

  test('reordering children updates the VNode order', () => {
    const tree1 = el(
      'List',
      {},
      el('List.Item', { key: 'a', title: 'A' }),
      el('List.Item', { key: 'b', title: 'B' }),
      el('List.Item', { key: 'c', title: 'C' }),
    );
    renderer.render(tree1);

    // Reorder: C, A, B
    const tree2 = el(
      'List',
      {},
      el('List.Item', { key: 'c', title: 'C' }),
      el('List.Item', { key: 'a', title: 'A' }),
      el('List.Item', { key: 'b', title: 'B' }),
    );
    const root = renderer.render(tree2);

    expect(root.children[0].props.title).toBe('C');
    expect(root.children[1].props.title).toBe('A');
    expect(root.children[2].props.title).toBe('B');
  });
});

// ============================================================================
// 7. TEXT INSTANCE HANDLING
// ============================================================================

describe('Reconciler — text content handling', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('createTextInstance produces a text VNode or is appended to parent props', () => {
    // When a component has raw text children, the reconciler should either:
    // (a) create a text VNode with type "__text__" or similar, or
    // (b) fold text content into the parent's props.
    // This test verifies at least one of those strategies works.
    const tree = el('Detail.Markdown', { content: '' }, 'Plain text content');
    const root = renderer.render(tree);

    // The exact representation depends on Ash's design. Check that the
    // text is captured somewhere in the tree:
    const hasText =
      root.props.textContent === 'Plain text content' ||
      root.children.some(
        (c: VNode) => c.type === '__text__' && c.props.text === 'Plain text content',
      );
    expect(hasText).toBe(true);
  });
});
