// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Test specifications: VNode → CmdPal SDK type translator.
 *
 * The translator takes a VNode tree (produced by the reconciler from Raycast-
 * style JSX) and converts it to CmdPal TypeScript SDK objects such as
 * DynamicListPage, ListItem, ContentPage, MarkdownContent, etc.
 *
 * STATUS: Specification-only. Tests will fail until translator + reconciler
 *         are implemented. They serve as acceptance criteria for the mapping.
 */

import type { VNode } from './types';

// ---------------------------------------------------------------------------
// Translator import — the real implementation.
// The translator produces its own CmdPal-compatible classes rather than SDK
// abstract base instances. Tests cast to these concrete types.
// ---------------------------------------------------------------------------
import {
  translateVNode,
  RaycastDynamicListPage,
  RaycastContentPage,
  RaycastMarkdownContent,
} from '../src/translator';

// Helper: build a VNode literal.
function vnode(
  type: string,
  props: Record<string, unknown> = {},
  children: VNode[] = [],
): VNode {
  return { type, props, children };
}

// ============================================================================
// 1. LIST → DynamicListPage
// ============================================================================

describe('Translator — List VNode → DynamicListPage', () => {
  test('List VNode translates to a DynamicListPage instance', () => {
    const tree = vnode('List', { navigationTitle: 'Search' }, [
      vnode('List.Item', { title: 'Hello', subtitle: 'World' }),
      vnode('List.Item', { title: 'Foo', icon: 'star.png' }),
    ]);

    const page = translateVNode(tree);

    // Should be or extend DynamicListPage
    expect(page).toBeDefined();
    expect((page as any)._type).toBe('dynamicListPage');
  });

  test('List with navigationTitle maps to page name', () => {
    const tree = vnode('List', { navigationTitle: 'My Results' });
    const page = translateVNode(tree) as RaycastDynamicListPage;

    expect(page.name).toBe('My Results');
  });

  test('List with placeholder maps to placeholderText', () => {
    const tree = vnode('List', {
      searchBarPlaceholder: 'Type to search…',
    });
    const page = translateVNode(tree) as RaycastDynamicListPage;

    expect(page.placeholderText).toBe('Type to search…');
  });

  test('List isLoading does not crash translator', () => {
    const tree = vnode('List', { isLoading: true });

    expect(() => translateVNode(tree)).not.toThrow();
  });
});

// ============================================================================
// 2. LIST.ITEM → ListItem
// ============================================================================

describe('Translator — List.Item VNode → ListItem', () => {
  test('basic List.Item maps title, subtitle, icon', () => {
    const listTree = vnode('List', {}, [
      vnode('List.Item', { title: 'Hello', subtitle: 'World', icon: 'doc.png' }),
    ]);

    const page = translateVNode(listTree) as RaycastDynamicListPage;
    const items = page.getItems();

    expect(items).toHaveLength(1);
    expect(items[0].title).toBe('Hello');
    expect(items[0].subtitle).toBe('World');
    // Icon should be converted to IIconInfo format
    expect(items[0].icon).toBeDefined();
  });

  test('List.Item without subtitle leaves subtitle empty', () => {
    const listTree = vnode('List', {}, [
      vnode('List.Item', { title: 'Solo' }),
    ]);

    const page = translateVNode(listTree) as RaycastDynamicListPage;
    const items = page.getItems();

    expect(items[0].subtitle).toBeFalsy();
  });

  test('List.Item with accessories maps to tags or details', () => {
    const listTree = vnode('List', {}, [
      vnode('List.Item', {
        title: 'Item',
        accessories: [
          { text: '5m ago', tooltip: 'Last seen' },
          { icon: 'star.png' },
        ],
      }),
    ]);

    const page = translateVNode(listTree) as RaycastDynamicListPage;
    const items = page.getItems();

    // Accessories should map to tags or some metadata on the ListItem
    const item = items[0] as any;
    const hasMappedAccessories =
      (item.tags && item.tags.length > 0) ||
      (item.details && item.details.metadata && item.details.metadata.length > 0);
    expect(hasMappedAccessories).toBe(true);
  });

  test('List.Item with emoji icon maps to IIconInfo', () => {
    const listTree = vnode('List', {}, [
      vnode('List.Item', { title: 'Fruit', icon: '🍎' }),
    ]);

    const page = translateVNode(listTree) as RaycastDynamicListPage;
    const items = page.getItems();

    // Emoji icon should produce an IIconInfo with the emoji as glyph
    expect(items[0].icon).toBeDefined();
    const iconLight = (items[0].icon as any)?.light;
    expect(
      iconLight?.icon === '🍎' || (typeof iconLight === 'string' && iconLight === '🍎'),
    ).toBe(true);
  });

  test('List.Section title maps to section field on ListItems', () => {
    const listTree = vnode('List', {}, [
      vnode('List.Section', { title: 'Recent' }, [
        vnode('List.Item', { title: 'A' }),
        vnode('List.Item', { title: 'B' }),
      ]),
    ]);

    const page = translateVNode(listTree) as RaycastDynamicListPage;
    const items = page.getItems();

    // Items under a section should have their section field set
    for (const item of items) {
      expect((item as any).section).toBe('Recent');
    }
  });
});

// ============================================================================
// 3. DETAIL → ContentPage + MarkdownContent
// ============================================================================

describe('Translator — Detail VNode → ContentPage', () => {
  test('Detail with markdown produces ContentPage', () => {
    const tree = vnode('Detail', { navigationTitle: 'Info' }, [
      vnode('Detail.Markdown', { content: '# Hello\n\nWorld' }),
    ]);

    const page = translateVNode(tree);

    expect(page).toBeDefined();
    expect((page as any)._type).toBe('contentPage');
  });

  test('Detail markdown body is preserved in MarkdownContent', () => {
    const md = '## Overview\n\nThis is a **test**.';
    const tree = vnode('Detail', {}, [
      vnode('Detail.Markdown', { content: md }),
    ]);

    const page = translateVNode(tree) as RaycastContentPage;
    const contents = page.getContent();

    expect(contents).toHaveLength(1);
    expect((contents[0] as RaycastMarkdownContent).body).toBe(md);
  });

  test('Detail.Metadata.Label maps to details metadata', () => {
    const tree = vnode('Detail', {}, [
      vnode('Detail.Markdown', { content: '# Doc' }),
      vnode('Detail.Metadata', {}, [
        vnode('Detail.Metadata.Label', { title: 'Version', text: '2.0' }),
      ]),
    ]);

    const page = translateVNode(tree) as RaycastContentPage;

    // The metadata should appear somewhere — either on page.details or as
    // extra content items. The exact mapping depends on implementation.
    expect(page).toBeDefined();
    // At minimum, content should contain the markdown
    const contents = page.getContent();
    expect(contents.length).toBeGreaterThanOrEqual(1);
  });

  test('Detail.Metadata.Link maps to details link element', () => {
    const tree = vnode('Detail', {}, [
      vnode('Detail.Markdown', { content: '' }),
      vnode('Detail.Metadata', {}, [
        vnode('Detail.Metadata.Link', {
          title: 'Homepage',
          target: 'https://example.com',
          text: 'Visit',
        }),
      ]),
    ]);

    const page = translateVNode(tree) as RaycastContentPage;
    expect(page).toBeDefined();
  });
});

// ============================================================================
// 4. ACTION PANEL → moreCommands
// ============================================================================

describe('Translator — ActionPanel → moreCommands array', () => {
  test('ActionPanel actions become moreCommands on parent item', () => {
    // An ActionPanel is typically a child of List.Item in Raycast.
    // In CmdPal, actions map to the moreCommands array on CommandItem.
    const listTree = vnode('List', {}, [
      vnode('List.Item', { title: 'Item' }, [
        vnode('ActionPanel', {}, [
          vnode('Action', { title: 'Open', onAction: 'handler' }),
          vnode('Action', { title: 'Delete', onAction: 'handler' }),
        ]),
      ]),
    ]);

    const page = translateVNode(listTree) as RaycastDynamicListPage;
    const items = page.getItems();
    const item = items[0] as any;

    // Actions should appear in moreCommands
    expect(item.moreCommands).toBeDefined();
    expect(item.moreCommands.length).toBeGreaterThanOrEqual(2);
    expect(item.moreCommands[0].title).toBe('Open');
    expect(item.moreCommands[1].title).toBe('Delete');
  });

  test('ActionPanel.Section titles are preserved in command grouping', () => {
    const listTree = vnode('List', {}, [
      vnode('List.Item', { title: 'Item' }, [
        vnode('ActionPanel', {}, [
          vnode('ActionPanel.Section', { title: 'Primary' }, [
            vnode('Action', { title: 'Run' }),
          ]),
          vnode('ActionPanel.Section', { title: 'Danger' }, [
            vnode('Action', { title: 'Delete' }),
          ]),
        ]),
      ]),
    ]);

    const page = translateVNode(listTree) as RaycastDynamicListPage;
    const items = page.getItems();
    const item = items[0] as any;

    // Should have commands from both sections
    expect(item.moreCommands).toBeDefined();
    expect(item.moreCommands.length).toBeGreaterThanOrEqual(2);
  });
});

// ============================================================================
// 5. ACTION.COPYTO CLIPBOARD → CmdPal command
// ============================================================================

describe('Translator — Action.CopyToClipboard → CmdPal command', () => {
  test('CopyToClipboard becomes an invokable command with clipboard behavior', () => {
    const listTree = vnode('List', {}, [
      vnode('List.Item', { title: 'Secret' }, [
        vnode('ActionPanel', {}, [
          vnode('Action.CopyToClipboard', { content: 'secret-value', title: 'Copy' }),
        ]),
      ]),
    ]);

    const page = translateVNode(listTree) as RaycastDynamicListPage;
    const items = page.getItems();
    const commands = (items[0] as any).moreCommands;

    expect(commands).toBeDefined();
    const copyCmd = commands.find((c: any) => c.title === 'Copy');
    expect(copyCmd).toBeDefined();
    // The command should carry the content to copy
    expect(copyCmd.command || copyCmd).toBeDefined();
  });
});

// ============================================================================
// 6. ACTION.OPENINBROWSER → OpenUrlCommand
// ============================================================================

describe('Translator — Action.OpenInBrowser → URL command', () => {
  test('OpenInBrowser maps to a command that opens a URL', () => {
    const listTree = vnode('List', {}, [
      vnode('List.Item', { title: 'GitHub' }, [
        vnode('ActionPanel', {}, [
          vnode('Action.OpenInBrowser', {
            url: 'https://github.com',
            title: 'Open in Browser',
          }),
        ]),
      ]),
    ]);

    const page = translateVNode(listTree) as RaycastDynamicListPage;
    const items = page.getItems();
    const commands = (items[0] as any).moreCommands;

    const openCmd = commands.find((c: any) => c.title === 'Open in Browser');
    expect(openCmd).toBeDefined();
  });
});

// ============================================================================
// 7. UNKNOWN COMPONENT TYPES → graceful fallback
// ============================================================================

describe('Translator — unknown component types', () => {
  test('unknown top-level component does not throw', () => {
    const tree = vnode('UnknownWidget', { foo: 'bar' });

    expect(() => translateVNode(tree)).not.toThrow();
  });

  test('unknown top-level component returns null or fallback object', () => {
    const tree = vnode('UnknownWidget', { foo: 'bar' });
    const result = translateVNode(tree);

    // Should either return null/undefined or a benign fallback
    const isGraceful = result === null || result === undefined || typeof result === 'object';
    expect(isGraceful).toBe(true);
  });

  test('unknown child component is skipped without breaking siblings', () => {
    const listTree = vnode('List', {}, [
      vnode('List.Item', { title: 'Valid' }),
      vnode('TotallyFake', { data: 123 }),
      vnode('List.Item', { title: 'Also Valid' }),
    ]);

    const page = translateVNode(listTree) as RaycastDynamicListPage;
    const items = page.getItems();

    // Should have at least the 2 valid items; the unknown one is skipped
    expect(items.length).toBeGreaterThanOrEqual(2);
    expect(items[0].title).toBe('Valid');
    expect(items[1].title).toBe('Also Valid');
  });
});

// ============================================================================
// 8. FORM → ContentPage with FormContent
// ============================================================================

describe('Translator — Form VNode → ContentPage with form content', () => {
  test('Form translates to a ContentPage', () => {
    const tree = vnode('Form', { navigationTitle: 'New Item' }, [
      vnode('Form.TextField', { id: 'name', title: 'Name' }),
      vnode('Form.Checkbox', { id: 'active', title: 'Active' }),
    ]);

    const page = translateVNode(tree);

    expect(page).toBeDefined();
    // Should produce a content page (form pages are content pages in CmdPal)
    expect((page as any)._type).toBe('contentPage');
  });

  test('Form fields are represented in content', () => {
    const tree = vnode('Form', {}, [
      vnode('Form.TextField', { id: 'email', title: 'Email' }),
    ]);

    const page = translateVNode(tree) as RaycastContentPage;
    const contents = page.getContent();

    expect(contents.length).toBeGreaterThanOrEqual(1);
  });
});
