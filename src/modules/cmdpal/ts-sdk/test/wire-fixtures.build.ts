// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Builds the canonical wire fixtures consumed by the phase-3 C# tests. Every
 * fixture is produced by the same serializer the runtime uses, so the fixtures
 * cannot drift from the code that emits them. A few fixtures (host status
 * payloads, provider metadata, and forward-compatibility cases) are shaped by
 * small deterministic builders that mirror the runtime and host bridge, since
 * those payloads are notification params rather than serializer output.
 *
 * Fixtures are serialized with {@link canonicalJson}: object keys are sorted
 * recursively, indentation is two spaces, newlines are LF, and the file ends
 * with a single trailing newline. This keeps the JSON stable across machines so
 * the regeneration test can assert byte-for-byte equality.
 */

import type {
  Color,
  Details,
  IconInfo,
  IListItem,
  IListPage,
  KeyChord,
  Tag,
} from '../src/types.js';
import {
  WireSerializer,
  type FormCollector,
  type FormSubmitHandler,
} from '../src/runtime/serialize.js';
import { serializeCommandResult } from '../src/runtime/commandResult.js';
import { iconFromBase64, iconFromGlyph } from '../src/helpers.js';

/** Numeric message-state map, mirroring the host bridge in `server.ts`. */
const MESSAGE_STATE = { info: 0, success: 1, warning: 2, error: 3 } as const;

function color(r: number, g: number, b: number, a = 255): Color {
  return { r, g, b, a };
}

/** A form collector that assigns deterministic ids, matching the runtime. */
function collector(): FormCollector {
  let counter = 0;
  const forms = new Map<string, FormSubmitHandler>();
  return {
    nextId(): string {
      const id = `form-${String(counter)}`;
      counter += 1;
      return id;
    },
    register(formId: string, handler: FormSubmitHandler): void {
      forms.set(formId, handler);
    },
  };
}

function iconLightDark(): IconInfo {
  return { light: { icon: '\uE706' }, dark: { icon: '\uE708' } };
}

function sampleTags(): Tag[] {
  return [
    {
      text: 'New',
      foreground: { hasValue: true, color: color(255, 255, 255) },
      background: { hasValue: true, color: color(0, 120, 215) },
      toolTip: 'Recently added',
    },
    { text: 'Beta', foreground: { hasValue: false }, background: { hasValue: false } },
  ];
}

function sampleDetails(): Details {
  return {
    heroImage: iconFromBase64('aGVsbG8='),
    title: 'Details title',
    body: '**Body** text',
    metadata: [
      { key: 'Tags', data: { type: 'tags', tags: sampleTags() } },
      { key: 'Link', data: { type: 'link', link: 'https://example.com', text: 'Open' } },
      {
        key: 'Actions',
        data: { type: 'commands', commands: [{ id: 'act-1', name: 'Act' }] },
      },
      { key: 'Divider', data: { type: 'separator' } },
    ],
  };
}

function sampleKeyChord(): KeyChord {
  return { modifiers: 1, vkey: 65, scanCode: 30 };
}

function listPage(): IListPage {
  return {
    id: 'page-list',
    name: 'List Page',
    title: 'List Page',
    accentColor: { hasValue: true, color: color(16, 124, 16) },
    placeholderText: 'Search...',
    showDetails: true,
    getItems(): IListItem[] {
      return [richListItem()];
    },
  };
}

function gridPage(): IListPage {
  return {
    id: 'page-grid',
    name: 'Grid Page',
    title: 'Grid Page',
    accentColor: { hasValue: true, color: color(120, 0, 120) },
    gridProperties: { type: 'medium', showTitle: true, showSubtitle: false },
    getItems(): IListItem[] {
      return [
        {
          command: { id: 'tile-1', name: 'Tile' },
          title: 'Tile One',
          icon: iconFromGlyph('\uE700'),
        },
      ];
    },
  };
}

function richListItem(): IListItem {
  return {
    command: { id: 'cmd-rich', name: 'Rich' },
    title: 'Rich item',
    subtitle: 'With everything',
    section: 'Section A',
    tags: sampleTags(),
    icon: iconLightDark(),
    details: sampleDetails(),
    moreCommands: [
      {
        command: { id: 'more-1', name: 'More' },
        title: 'More action',
        requestedShortcut: sampleKeyChord(),
        isCritical: true,
      },
    ],
  };
}

/** Builds every wire fixture, keyed by its file name (without extension). */
export async function buildFixtures(): Promise<Record<string, unknown>> {
  const serializer = new WireSerializer();
  const forms = collector();

  const fixtures: Record<string, unknown> = {};

  // CommandResult kinds. The numeric Kind map and PascalCase args are the
  // locked wire contract and must stay byte-identical.
  fixtures['command-result-dismiss'] = serializeCommandResult({ kind: 'dismiss' });
  fixtures['command-result-goHome'] = serializeCommandResult({ kind: 'goHome' });
  fixtures['command-result-goBack'] = serializeCommandResult({ kind: 'goBack' });
  fixtures['command-result-hide'] = serializeCommandResult({ kind: 'hide' });
  fixtures['command-result-keepOpen'] = serializeCommandResult({ kind: 'keepOpen' });
  fixtures['command-result-null'] = serializeCommandResult(null);
  fixtures['command-result-goToPage'] = serializeCommandResult({
    kind: 'goToPage',
    args: { pageId: 'target-page', navigationMode: 'push' },
  });
  fixtures['command-result-showToast'] = serializeCommandResult({
    kind: 'showToast',
    args: { message: 'Saved' },
  });
  // Nested toast continuation (p1-10): the toast carries a follow-up result.
  fixtures['command-result-showToast-nested'] = serializeCommandResult({
    kind: 'showToast',
    args: { message: 'Saved', result: { kind: 'goHome' } },
  });
  fixtures['command-result-confirm'] = serializeCommandResult({
    kind: 'confirm',
    args: {
      title: 'Delete?',
      description: 'This cannot be undone.',
      isPrimaryCommandCritical: true,
      primaryCommand: { id: 'do-delete', name: 'Delete' },
    },
  });

  // Content types.
  fixtures['content-markdown'] = await serializer.content({ type: 'markdown', body: '# Title' });
  fixtures['content-plainText'] = await serializer.content({
    type: 'plainText',
    text: 'Hello',
    fontFamily: 'Consolas',
    wrapWords: true,
  });
  fixtures['content-image'] = await serializer.content({
    type: 'image',
    image: iconFromBase64('aW1n'),
    maxWidth: 320,
    maxHeight: 240,
  });
  fixtures['content-form'] = await serializer.content(
    {
      type: 'form',
      templateJson: '{"type":"AdaptiveCard"}',
      dataJson: '{}',
      submitForm: () => ({ kind: 'goHome' }),
    },
    forms,
  );
  fixtures['content-tree-nested-form'] = await serializer.content(
    {
      type: 'tree',
      rootContent: { type: 'markdown', body: 'root' },
      getChildren: () => [
        {
          type: 'form',
          formId: 'child-form',
          templateJson: '{"type":"AdaptiveCard"}',
          dataJson: '{}',
          submitForm: () => ({ kind: 'goBack' }),
        },
      ],
    },
    forms,
  );

  // Items.
  fixtures['command-item'] = serializer.commandItem({
    command: { id: 'ci-1', name: 'Item' },
    title: 'Command item',
    subtitle: 'Subtitle',
    icon: iconFromGlyph('\uE71D'),
  });
  fixtures['list-item'] = serializer.listItem(richListItem());
  fixtures['list-item-separator'] = serializer.listItem({
    _isSeparator: true,
    command: { id: 'sep', name: 'sep' },
    title: 'Group',
    section: 'Section',
  } as unknown as IListItem);
  fixtures['grid-item'] = serializer.listItem({
    command: { id: 'tile-1', name: 'Tile' },
    title: 'Tile One',
    icon: iconFromGlyph('\uE700'),
  });

  // Icons.
  fixtures['icon-glyph'] = iconFromGlyph('\uE71D');
  fixtures['icon-base64'] = iconFromBase64('iVBORw0KGgo=');
  fixtures['icon-data-uri'] = iconFromBase64('data:image/png;base64,iVBORw0KGgo=');
  fixtures['icon-light-dark'] = iconLightDark();
  fixtures['icon-path'] = {
    light: { icon: 'C:/icons/app.png' },
    dark: { icon: 'C:/icons/app.png' },
  };

  // Details, tags, colors, keychords.
  fixtures['details'] = serializer.details(sampleDetails());
  fixtures['tags'] = sampleTags();
  fixtures['keychord'] = sampleKeyChord();

  // Pages (list, grid, content) including accentColor.
  fixtures['page-list'] = serializer.command(listPage());
  fixtures['page-grid'] = serializer.command(gridPage());

  // Provider metadata (mirrors runtime.providerMetadata), including frozen.
  fixtures['provider-metadata-frozen'] = {
    id: 'ext.frozen',
    displayName: 'Frozen Extension',
    frozen: true,
    icon: iconFromGlyph('\uE700'),
  };
  fixtures['provider-metadata-unfrozen'] = {
    id: 'ext.live',
    displayName: 'Live Extension',
    frozen: false,
  };

  // Status payloads (mirror the host bridge in server.ts): show, update, hide.
  fixtures['status-show'] = {
    statusId: 'status-1',
    message: { Message: 'Working', State: MESSAGE_STATE.info },
    progress: { isIndeterminate: true },
    context: 'extension',
  };
  fixtures['status-update'] = {
    statusId: 'status-1',
    message: { Message: 'Almost done', State: MESSAGE_STATE.success },
    progress: { isIndeterminate: false, progressPercent: 80 },
    context: 'extension',
  };
  fixtures['status-hide'] = {
    statusId: 'status-1',
    message: { Message: 'Almost done', State: MESSAGE_STATE.success },
  };

  // Forward-compatibility cases the C# parser must tolerate: unknown fields are
  // ignored, a future Kind degrades safely, and a future MessageState is kept.
  fixtures['compat-omitted-optionals'] = serializer.command({ id: 'bare', name: 'Bare' });
  fixtures['compat-unknown-fields'] = {
    Kind: 1,
    UnknownField: 'ignored',
    Nested: { a: 1, b: [1, 2] },
  };
  fixtures['compat-future-command-result-kind'] = { Kind: 99 };
  fixtures['compat-future-message-state'] = { Message: 'Future', State: 99 };

  return fixtures;
}

/**
 * Serializes a value to canonical JSON: keys sorted recursively, two-space
 * indentation, LF newlines, and a single trailing newline.
 */
export function canonicalJson(value: unknown): string {
  return `${JSON.stringify(sortDeep(value), null, 2)}\n`;
}

function sortDeep(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((element) => sortDeep(element));
  }
  if (value !== null && typeof value === 'object') {
    const source = value as Record<string, unknown>;
    const sorted: Record<string, unknown> = {};
    for (const key of Object.keys(source).sort()) {
      sorted[key] = sortDeep(source[key]);
    }
    return sorted;
  }
  return value;
}
