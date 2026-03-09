// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Test specifications: edge cases and stress tests for the reconciler
 * and translator pipeline.
 *
 * These tests cover unusual inputs, boundary conditions, and performance
 * baselines to ensure the Raycast compat layer is robust.
 *
 * STATUS: Specification-only. Implementation pending.
 */

import type { VNode, ReconcilerRenderer } from './types';

// ---------------------------------------------------------------------------
// Translator import — real implementation.
// ---------------------------------------------------------------------------
import { translateVNode } from '../src/translator';

// ---------------------------------------------------------------------------
// Reconciler stub — returns a minimal mock so translator-only tests in
// mixed describe blocks don't crash from beforeEach. Reconciler-specific
// tests will fail on assertions (as expected — they're still specs).
// ---------------------------------------------------------------------------

function createReconciler(): ReconcilerRenderer {
  const emptyVNode: VNode = { type: '__stub__', props: {}, children: [] };
  return {
    render: (_element: React.ReactElement): VNode => emptyVNode,
    unmount: (): void => {},
  };
}

function el(
  type: string,
  props: Record<string, unknown> = {},
  ...children: unknown[]
): React.ReactElement {
  return { type, props: { ...props, children }, key: null } as unknown as React.ReactElement;
}

function vnode(
  type: string,
  props: Record<string, unknown> = {},
  children: VNode[] = [],
): VNode {
  return { type, props, children };
}

// ============================================================================
// 1. COMPONENTS WITH NO CHILDREN
// ============================================================================

describe('Edge cases — components with no children', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('List with zero items produces valid VNode with empty children', () => {
    const root = renderer.render(el('List', { navigationTitle: 'Empty' }));

    expect(root.type).toBe('List');
    expect(root.children).toEqual([]);
  });

  test('translator handles List with zero items gracefully', () => {
    const tree = vnode('List', { navigationTitle: 'Empty' });
    const page = translateVNode(tree) as any;

    expect(page).toBeDefined();
    const items = page.getItems ? page.getItems() : [];
    expect(items).toHaveLength(0);
  });

  test('ActionPanel with zero actions produces empty VNode children', () => {
    const root = renderer.render(el('ActionPanel'));
    expect(root.children).toEqual([]);
  });

  test('Detail with no markdown child still produces valid VNode', () => {
    const root = renderer.render(el('Detail', { navigationTitle: 'Blank' }));

    expect(root.type).toBe('Detail');
    expect(root.children).toHaveLength(0);
  });

  test('translator handles Detail with no markdown', () => {
    const tree = vnode('Detail', { navigationTitle: 'Blank' });
    const page = translateVNode(tree) as any;

    expect(page).toBeDefined();
    if (page.getContent) {
      const contents = page.getContent();
      // Should have zero or one content (empty markdown as fallback)
      expect(contents.length).toBeLessThanOrEqual(1);
    }
  });
});

// ============================================================================
// 2. DEEPLY NESTED COMPONENTS
// ============================================================================

describe('Edge cases — deeply nested components', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('3-level nesting: List > Section > Item with ActionPanel', () => {
    const tree = el(
      'List',
      {},
      el(
        'List.Section',
        { title: 'Group' },
        el(
          'List.Item',
          { title: 'Deep' },
          el(
            'ActionPanel',
            {},
            el('Action', { title: 'Act' }),
          ),
        ),
      ),
    );

    const root = renderer.render(tree);

    expect(root.type).toBe('List');
    const section = root.children[0];
    expect(section.type).toBe('List.Section');
    const item = section.children[0];
    expect(item.type).toBe('List.Item');
    expect(item.children[0].type).toBe('ActionPanel');
    expect(item.children[0].children[0].type).toBe('Action');
  });

  test('translator handles 3-level nesting end-to-end', () => {
    const tree = vnode('List', {}, [
      vnode('List.Section', { title: 'Group' }, [
        vnode('List.Item', { title: 'Deep' }, [
          vnode('ActionPanel', {}, [
            vnode('Action', { title: 'Act' }),
          ]),
        ]),
      ]),
    ]);

    const page = translateVNode(tree) as any;
    const items = page.getItems ? page.getItems() : [];

    expect(items.length).toBeGreaterThanOrEqual(1);
    expect(items[0].title).toBe('Deep');
    // Action should be in moreCommands
    if (items[0].moreCommands) {
      expect(items[0].moreCommands.length).toBeGreaterThanOrEqual(1);
    }
  });

  test('Detail with nested metadata containing tag lists', () => {
    const tree = vnode('Detail', {}, [
      vnode('Detail.Markdown', { content: '# Doc' }),
      vnode('Detail.Metadata', {}, [
        vnode('Detail.Metadata.TagList', { title: 'Status' }, [
          vnode('Detail.Metadata.TagList.Item', { text: 'active', color: 'green' }),
          vnode('Detail.Metadata.TagList.Item', { text: 'beta', color: 'orange' }),
        ]),
      ]),
    ]);

    // Should not throw
    expect(() => translateVNode(tree)).not.toThrow();
  });
});

// ============================================================================
// 3. PROPS WITH UNDEFINED / NULL VALUES
// ============================================================================

describe('Edge cases — props with undefined/null values', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('List.Item with undefined subtitle is captured without error', () => {
    const tree = el(
      'List',
      {},
      el('List.Item', { title: 'Test', subtitle: undefined }),
    );

    const root = renderer.render(tree);
    const item = root.children[0];

    expect(item.props.title).toBe('Test');
    // subtitle should be undefined or missing, not cause a crash
    expect(item.props.subtitle).toBeUndefined();
  });

  test('List.Item with null icon is captured without error', () => {
    const tree = el(
      'List',
      {},
      el('List.Item', { title: 'No Icon', icon: null }),
    );

    const root = renderer.render(tree);
    expect(root.children[0].props.icon).toBeNull();
  });

  test('translator handles null/undefined props gracefully', () => {
    const tree = vnode('List', {}, [
      vnode('List.Item', { title: 'Test', subtitle: undefined, icon: null }),
    ]);

    expect(() => translateVNode(tree)).not.toThrow();

    const page = translateVNode(tree) as any;
    const items = page.getItems ? page.getItems() : [];
    expect(items).toHaveLength(1);
    expect(items[0].title).toBe('Test');
  });

  test('Action with null onAction does not crash', () => {
    const tree = vnode('List', {}, [
      vnode('List.Item', { title: 'Item' }, [
        vnode('ActionPanel', {}, [
          vnode('Action', { title: 'Broken', onAction: null }),
        ]),
      ]),
    ]);

    expect(() => translateVNode(tree)).not.toThrow();
  });

  test('empty string props are preserved, not coerced to undefined', () => {
    const tree = el(
      'List',
      {},
      el('List.Item', { title: '', subtitle: '' }),
    );

    const root = renderer.render(tree);
    expect(root.children[0].props.title).toBe('');
    expect(root.children[0].props.subtitle).toBe('');
  });
});

// ============================================================================
// 4. CONDITIONAL RENDERING (null returns)
// ============================================================================

describe('Edge cases — conditional rendering', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('null child in children array is filtered out', () => {
    // Simulates: { condition && <List.Item ... /> } where condition is false
    const tree = el(
      'List',
      {},
      el('List.Item', { title: 'Visible' }),
      null as any,
      el('List.Item', { title: 'Also Visible' }),
    );

    const root = renderer.render(tree);

    // Null children should be filtered — only real items remain
    const realChildren = root.children.filter((c: VNode) => c.type !== '__null__');
    expect(realChildren).toHaveLength(2);
    expect(realChildren[0].props.title).toBe('Visible');
    expect(realChildren[1].props.title).toBe('Also Visible');
  });

  test('false / undefined children are ignored', () => {
    const tree = el(
      'List',
      {},
      false as any,
      undefined as any,
      el('List.Item', { title: 'Only One' }),
    );

    const root = renderer.render(tree);
    const realChildren = root.children.filter(
      (c: VNode) => c && c.type && c.type !== '__null__',
    );
    expect(realChildren).toHaveLength(1);
  });

  test('translator ignores null VNode children', () => {
    const tree = vnode('List', {}, [
      vnode('List.Item', { title: 'A' }),
      // A null child that might slip through reconciler edge cases
      null as any,
      vnode('List.Item', { title: 'B' }),
    ]);

    expect(() => translateVNode(tree)).not.toThrow();

    const page = translateVNode(tree) as any;
    const items = page.getItems ? page.getItems() : [];

    // Should have at least 2 valid items, null is skipped
    const validItems = items.filter((i: any) => i && i.title);
    expect(validItems.length).toBeGreaterThanOrEqual(2);
  });
});

// ============================================================================
// 5. LARGE LISTS — PERFORMANCE BASELINE
// ============================================================================

describe('Edge cases — large list performance', () => {
  test('reconciler handles 100+ items without timeout (< 500ms)', () => {
    const renderer = createReconciler();

    const items = Array.from({ length: 150 }, (_, i) =>
      el('List.Item', { title: `Item ${i}`, subtitle: `Description ${i}` }),
    );

    const tree = el('List', { navigationTitle: 'Big List' }, ...items);

    const start = performance.now();
    const root = renderer.render(tree);
    const elapsed = performance.now() - start;

    expect(root.children).toHaveLength(150);
    expect(elapsed).toBeLessThan(500); // 500ms budget

    renderer.unmount();
  });

  test('translator handles 100+ items without timeout (< 500ms)', () => {
    const children = Array.from({ length: 150 }, (_, i) =>
      vnode('List.Item', { title: `Item ${i}`, subtitle: `Desc ${i}` }),
    );

    const tree = vnode('List', { navigationTitle: 'Big List' }, children);

    const start = performance.now();
    const page = translateVNode(tree) as any;
    const elapsed = performance.now() - start;

    const items = page.getItems ? page.getItems() : [];
    expect(items).toHaveLength(150);
    expect(elapsed).toBeLessThan(500);
  });

  test('reconciler handles 500 items (stress test)', () => {
    const renderer = createReconciler();

    const items = Array.from({ length: 500 }, (_, i) =>
      el('List.Item', {
        title: `Stress Item ${i}`,
        subtitle: `Sub ${i}`,
        icon: i % 2 === 0 ? 'icon.png' : undefined,
      }),
    );

    const tree = el('List', {}, ...items);

    const start = performance.now();
    const root = renderer.render(tree);
    const elapsed = performance.now() - start;

    expect(root.children).toHaveLength(500);
    expect(elapsed).toBeLessThan(2000); // 2s budget for 500 items

    renderer.unmount();
  });
});

// ============================================================================
// 6. MIXED COMPONENT TYPES IN SAME TREE
// ============================================================================

describe('Edge cases — mixed component types', () => {
  test('List.Item with both ActionPanel and accessories', () => {
    const tree = vnode('List', {}, [
      vnode('List.Item', {
        title: 'Complex',
        accessories: [{ text: 'tag' }],
      }, [
        vnode('ActionPanel', {}, [
          vnode('Action', { title: 'Do' }),
        ]),
      ]),
    ]);

    expect(() => translateVNode(tree)).not.toThrow();

    const page = translateVNode(tree) as any;
    const items = page.getItems ? page.getItems() : [];
    expect(items).toHaveLength(1);
  });
});

// ============================================================================
// 7. UNMOUNT / CLEANUP
// ============================================================================

describe('Edge cases — unmount and cleanup', () => {
  test('unmount clears the VNode tree', () => {
    const renderer = createReconciler();

    renderer.render(
      el('List', {}, el('List.Item', { title: 'Before Unmount' })),
    );

    renderer.unmount();

    // After unmount, rendering again should start fresh
    const root = renderer.render(el('List', {}));
    expect(root.children).toHaveLength(0);
  });

  test('double unmount does not throw', () => {
    const renderer = createReconciler();

    renderer.render(el('List', {}));
    renderer.unmount();

    expect(() => renderer.unmount()).not.toThrow();
  });
});

// ============================================================================
// 8. PROP TYPES — boolean, number, object, array
// ============================================================================

describe('Edge cases — diverse prop types', () => {
  let renderer: ReconcilerRenderer;

  beforeEach(() => {
    renderer = createReconciler();
  });

  afterEach(() => {
    renderer.unmount();
  });

  test('boolean prop (isLoading) is captured as boolean, not string', () => {
    const root = renderer.render(el('List', { isLoading: true }));
    expect(root.props.isLoading).toBe(true);
    expect(typeof root.props.isLoading).toBe('boolean');
  });

  test('numeric props are preserved', () => {
    const root = renderer.render(
      el('List.Item', { title: 'Test', rank: 42 }),
    );
    expect(root.props.rank).toBe(42);
    expect(typeof root.props.rank).toBe('number');
  });

  test('object props (e.g., shortcut) are preserved by reference', () => {
    const shortcut = { modifiers: ['cmd'], key: 'k' };
    const root = renderer.render(
      el('Action', { title: 'Act', shortcut }),
    );
    expect(root.props.shortcut).toEqual(shortcut);
  });

  test('array props (e.g., keywords) are preserved', () => {
    const keywords = ['react', 'typescript', 'testing'];
    const root = renderer.render(
      el('List.Item', { title: 'Item', keywords }),
    );
    expect(root.props.keywords).toEqual(keywords);
  });
});
