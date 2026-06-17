// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

import {
  CommandProviderBase,
  ListPageBase,
  DynamicListPageBase,
  ContentPageBase,
  InvokableCommandBase,
  CommandItemBase,
  ListItemBase,
  FallbackCommandItemBase,
  Separator,
  NoOpCommand,
  OpenUrlCommand,
  CopyTextCommand,
  ConfirmableCommand,
  Settings,
  ToggleSetting,
  TextSetting,
  ChoiceSetSetting,
  ExtensionHost,
  activate as sdkActivate,
  startJsonRpcServer,
  sendNotification,
  type ActivationContext,
  type Color,
  type CommandResult,
  type CommandResultArgs,
  type CommandResultKind,
  type Content,
  type ContentType,
  type ContextItem,
  type Details,
  type DetailsCommands,
  type DetailsElement,
  type DetailsLink,
  type DetailsSeparator,
  type DetailsTags,
  type Filter,
  type Filters,
  type FontFamily,
  type GoToPageArgs,
  type GridLayoutType,
  type GridProperties,
  type ICommand,
  type ICommandItem,
  type ICommandProvider,
  type ICommandSettings,
  type IContentPage,
  type IDynamicListPage,
  type IExtensionHost,
  type IFallbackCommandItem,
  type IInvokableCommand,
  type IListItem,
  type IListPage,
  type IPage,
  type IconData,
  type IconInfo,
  type KeyChord,
  type MarkdownContent,
  type MessageState,
  type OptionalColor,
  type PlainTextContent,
  type ProgressState,
  type StatusContext,
  type StatusMessage,
  type Tag,
  type ToastArgs,
  type TreeContent,
  type FormContent,
  type ImageContent,
  iconFromUrl,
  iconFromGlyph,
} from '@microsoft/cmdpal-sdk';

const EXTENSION_ID = 'sample-js-extension';
const EXTENSION_DISPLAY_NAME = 'Sample JS Extension';
const README_PAGE_ID = 'markdown-page';
const DEFAULT_NAVIGATION_MODE: GoToPageArgs['navigationMode'] = 'push';
const GALLERY_LAYOUT: GridLayoutType = 'gallery';
const MONOSPACE_FONT: FontFamily = 'monospace';
const SAMPLE_CONTENT_TYPE: MarkdownContent['type'] = 'markdown';
const SAMPLE_STATUS_CONTEXT: StatusContext = 'extension';

const PAGE_IDS = {
  main: 'main-index-page',
  staticList: 'static-list-page',
  detailsList: 'details-list-page',
  gridGallery: 'grid-gallery-page',
  filteredList: 'filtered-list-page',
  markdown: README_PAGE_ID,
  plainText: 'plain-text-page',
  image: 'image-page',
  tree: 'tree-page',
  form: 'form-page',
  multiContent: 'multi-content-page',
} as const;

function icon(glyph: string): IconInfo {
  const light: IconData = { icon: glyph };
  const dark: IconData = { icon: glyph };
  return { light, dark };
}

function rgba(r: number, g: number, b: number, a: number = 255): Color {
  return { r, g, b, a };
}

function optionalColor(color: Color): OptionalColor {
  return { hasValue: true, color };
}

// Colored grid icons using emoji characters rendered by the glyph pipeline
const gridIcons: Record<string, string> = {
  IMG: '\uE91B',   // Photo
  TREE: '\uE8FD',  // OrgChart / hierarchy
  MIX: '\uE81E',   // Page (document with mixed content)
  GRID: '\uE80A',  // GridView
};

function gridIcon(label: string): IconInfo {
  return icon(gridIcons[label] ?? '\uE8A5');
}

function makeTag(
  text: string,
  foreground?: OptionalColor,
  background?: OptionalColor,
  glyph?: string,
  toolTip?: string
): Tag {
  return {
    text,
    foreground,
    background,
    icon: glyph ? icon(glyph) : undefined,
    toolTip,
  };
}

function makeStatus(message: string, state: MessageState, progress?: ProgressState): StatusMessage {
  return { message, state, progress };
}

function publishStatus(message: string, state: MessageState, progress?: ProgressState): void {
  const status = makeStatus(message, state, progress);
  ExtensionHost.showStatus(status.message, status.state, status.progress);
  sendNotification('sample-js-extension/status', { context: SAMPLE_STATUS_CONTEXT, status });
}

function hidePublishedStatus(message: string): void {
  ExtensionHost.hideStatus(message);
}

function showToastResult(message: string, result?: CommandResult): CommandResult {
  const args: ToastArgs = { message, result };
  return { kind: 'showToast', args };
}

function goToPageResult(
  pageId: string,
  navigationMode: GoToPageArgs['navigationMode'] = DEFAULT_NAVIGATION_MODE
): CommandResult {
  const args: GoToPageArgs = { pageId, navigationMode };
  return { kind: 'goToPage', args };
}

function shortcut(modifiers: number, vkey: number, scanCode: number): KeyChord {
  return { modifiers, vkey, scanCode };
}

function contextAction(
  command: ICommand,
  title: string,
  glyph: string,
  subtitle?: string,
  requestedShortcut?: KeyChord,
  isCritical?: boolean
): ContextItem {
  return {
    command,
    title,
    subtitle,
    icon: icon(glyph),
    requestedShortcut,
    isCritical,
  };
}

function containsQuery(item: { title: string; subtitle?: string; section?: string }, query: string): boolean {
  const haystack = `${item.title} ${item.subtitle ?? ''} ${item.section ?? ''}`.toLowerCase();
  return haystack.includes(query.toLowerCase());
}

function parseJsonObject(json: string): Record<string, string> {
  if (!json) {
    return {};
  }

  try {
    const parsed = JSON.parse(json);
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

class CommandRegistry {
  private readonly commands = new Map<string, ICommand>();

  register<T extends ICommand>(command: T): T {
    this.commands.set(command.id, command);
    return command;
  }

  registerAll(commands: readonly ICommand[]): void {
    for (const command of commands) {
      this.register(command);
    }
  }

  get(id: string): ICommand | null {
    return this.commands.get(id) ?? null;
  }
}

// === Utility Commands ===

class ShowToastDemoCommand extends InvokableCommandBase {
  readonly id = 'show-toast-command';
  readonly name = 'Show Toast';
  readonly icon = icon('\uE7F4');

  constructor(private readonly settings: Settings) {
    super();
  }

  invoke(): CommandResult {
    const greeting = this.settings.getSetting<TextSetting>('greeting')?.value ?? 'Hello!';
    const darkMode = this.settings.getSetting<ToggleSetting>('darkMode')?.value ?? false;
    const theme = this.settings.getSetting<ChoiceSetSetting>('theme')?.value ?? 'default';
    const progress: ProgressState = { isIndeterminate: false, progressPercent: 100 };

    publishStatus('Running toast showcase…', 'info', progress);
    ExtensionHost.log(`Toast showcase invoked with theme=${theme}; darkMode=${darkMode}`, 'info');
    hidePublishedStatus('Running toast showcase…');

    return showToastResult(`${greeting} from ${EXTENSION_DISPLAY_NAME} (${theme}${darkMode ? ', dark mode' : ''})`, {
      kind: 'keepOpen',
      args: { source: 'show-toast-demo' } as CommandResultArgs,
    });
  }
}

class GoToPageCommand extends InvokableCommandBase {
  readonly icon: IconInfo;

  constructor(
    readonly id: string,
    readonly name: string,
    private readonly pageId: string,
    glyph: string,
    private readonly navigationMode: GoToPageArgs['navigationMode'] = DEFAULT_NAVIGATION_MODE
  ) {
    super();
    this.icon = icon(glyph);
  }

  invoke(): CommandResult {
    return goToPageResult(this.pageId, this.navigationMode);
  }
}

class FixedResultCommand extends InvokableCommandBase {
  readonly icon: IconInfo;

  constructor(
    readonly id: string,
    readonly name: string,
    private readonly kind: CommandResultKind,
    glyph: string,
    private readonly args?: CommandResultArgs,
    private readonly logState: MessageState = 'info'
  ) {
    super();
    this.icon = icon(glyph);
  }

  invoke(): CommandResult {
    ExtensionHost.log(`${this.name} invoked`, this.logState);
    return { kind: this.kind, args: this.args };
  }
}

class QueryAwareFallbackCommand extends InvokableCommandBase {
  readonly id = 'fallback-query-command';
  readonly name = 'Fallback Query';
  readonly icon = icon('\uE721');

  private query = '';

  setQuery(query: string): void {
    this.query = query.trim();
  }

  invoke(): CommandResult {
    const message = this.query
      ? `Fallback command received query: “${this.query}”`
      : 'Fallback command invoked without any query text.';

    ExtensionHost.log(message, 'info');
    return showToastResult(message, { kind: 'keepOpen' });
  }
}

// === Demo Pages ===
//   - Main Index Page
//   - Static List Page
//   - Details List Page
//   - Grid Gallery Page
//   - Filtered List Page
//   - Markdown Content Page
//   - Plain Text Content Page
//   - Image Content Page
//   - Tree Content Page
//   - Form Content Page
//   - Multi-Content Page

class StaticListPage extends ListPageBase implements IListPage {
  readonly id = PAGE_IDS.staticList;
  readonly name = 'Static List';
  readonly title = 'Static List Page';
  readonly icon = icon('\uE8FD');
  readonly placeholderText = 'Static pages do not respond to search input';
  readonly showDetails = true;
  readonly accentColor = optionalColor(rgba(15, 108, 189));

  private readonly items: IListItem[];

  constructor(
    private readonly moreCommands: readonly ContextItem[],
    private readonly noOp: ICommand,
    private readonly readmePage: ICommand,
    private readonly goHomeCommand: ICommand
  ) {
    super();

    this.items = [
      new Separator('Pinned items'),
      new ListItemBase({
        command: this.readmePage,
        title: 'Open the README-style markdown page',
        subtitle: 'Static items can navigate directly to a page command',
        icon: icon('\uE8A5'),
        section: 'Pages',
        tags: [
          makeTag('Static', optionalColor(rgba(0, 120, 212))),
          makeTag('Page', optionalColor(rgba(255, 255, 255)), optionalColor(rgba(0, 120, 212))),
        ],
        details: {
          title: 'Fixed list item',
          body: 'This page returns a fixed `IListItem[]` and uses sections, tags, icons, separators, and context actions.',
        },
        moreCommands: [...this.moreCommands],
      }),
      new Separator('Built-in helper commands'),
      new ListItemBase({
        command: this.noOp,
        title: 'No-op item',
        subtitle: 'Uses `NoOpCommand` to keep the list open without side effects',
        icon: icon('\uE9CE'),
        section: 'Helpers',
        tags: [makeTag('NoOp', optionalColor(rgba(96, 94, 92)), optionalColor(rgba(243, 242, 241)))],
        textToSuggest: 'no-op helper',
        details: {
          title: 'NoOpCommand sample',
          body: 'Use `NoOpCommand` for placeholders, dividers, or list items whose value is purely descriptive.',
        },
        moreCommands: [contextAction(this.goHomeCommand, 'Go home', '\uE80F', 'Return to the top level')],
      }),
      new ListItemBase({
        command: this.goHomeCommand,
        title: 'Go home from a static page',
        subtitle: 'Returns a `goHome` command result',
        icon: icon('\uE80F'),
        section: 'Commands',
        tags: [makeTag('Result', optionalColor(rgba(0, 153, 188)))],
        details: {
          title: 'Command result demo',
          body: 'This item demonstrates `CommandResultKind = goHome` from a regular list item.',
        },
      }),
    ];
  }

  getItems(): IListItem[] {
    return this.items;
  }
}

class DetailsListPage extends DynamicListPageBase implements IDynamicListPage {
  readonly id = PAGE_IDS.detailsList;
  readonly name = 'Details List';
  readonly title = 'Details List Page';
  readonly icon = icon('\uE8A0');
  readonly placeholderText = 'Search rich detail cards…';
  readonly showDetails = true;

  private query = '';
  private readonly items: IListItem[];

  constructor(
    private readonly openRepoCommand: ICommand,
    private readonly copyCommand: ICommand,
    private readonly showToastCommand: ICommand,
    private readonly hideCommand: ICommand,
    private readonly imageIcon: IconInfo
  ) {
    super();

    const statusTags: DetailsTags = {
      type: 'tags',
      tags: [
        makeTag('Active', optionalColor(rgba(255, 255, 255)), optionalColor(rgba(16, 124, 16))),
        makeTag('SDK', optionalColor(rgba(0, 120, 212)), undefined, '\uE943', 'Command Palette SDK'),
      ],
    };

    const linkData: DetailsLink = {
      type: 'link',
      link: 'https://github.com/microsoft/PowerToys',
      text: 'PowerToys repository',
    };

    const separatorData: DetailsSeparator = { type: 'separator' };
    const commandData: DetailsCommands = {
      type: 'commands',
      commands: [this.showToastCommand, this.copyCommand, this.hideCommand],
    };

    const metadata: DetailsElement[] = [
      { key: 'Status', data: statusTags },
      { key: 'Link', data: linkData },
      { key: '', data: separatorData },
      { key: 'Actions', data: commandData },
    ];

    const detailPayload: Details = {
      heroImage: this.imageIcon,
      title: 'Rich details panel',
      body: 'Details bodies support **Markdown**, hero images, and structured metadata containing tags, links, commands, and separators.',
      metadata,
    };

    this.items = [
      new ListItemBase({
        command: this.openRepoCommand,
        title: 'Rich details for repository links',
        subtitle: 'Shows every `Details` metadata shape in one place',
        icon: icon('\uE943'),
        section: 'Metadata',
        tags: [
          makeTag('Details', optionalColor(rgba(0, 120, 212))),
          makeTag('Hero', optionalColor(rgba(255, 255, 255)), optionalColor(rgba(136, 23, 152))),
        ],
        details: detailPayload,
        moreCommands: [
          contextAction(this.copyCommand, 'Copy repo URL', '\uE8C8', 'Copy the sample URL', shortcut(2, 67, 46)),
          contextAction(this.hideCommand, 'Hide palette', '\uE8BB', 'Return to the desktop'),
        ],
      }),
      new ListItemBase({
        command: this.showToastCommand,
        title: 'Action-oriented detail commands',
        subtitle: 'The details panel exposes contextual command buttons',
        icon: icon('\uE7F4'),
        section: 'Commands',
        tags: [makeTag('Context', optionalColor(rgba(16, 124, 16)))],
        details: {
          title: 'Metadata commands',
          body: 'Use the command strip inside details to surface contextual actions for the selected item.',
          metadata: [{ key: 'Commands', data: commandData }],
        },
      }),
      new ListItemBase({
        command: this.hideCommand,
        title: 'Link-heavy metadata item',
        subtitle: 'Combines links, separators, and tags in the side pane',
        icon: icon('\uE8A7'),
        section: 'Links',
        tags: [makeTag('Link', optionalColor(rgba(0, 120, 212))), makeTag('Separator', optionalColor(rgba(96, 94, 92)))],
        details: {
          title: 'Link metadata',
          body: 'A details panel can mix simple text, links, tags, separators, and actionable commands.',
          metadata: [
            { key: 'Docs', data: linkData },
            { key: '', data: separatorData },
            { key: 'Status', data: statusTags },
          ],
        },
      }),
    ];
  }

  setSearchText(text: string): void {
    this.query = text.trim().toLowerCase();
    this.notifyItemsChanged();
  }

  getItems(): IListItem[] {
    if (!this.query) {
      return this.items;
    }

    return this.items.filter((item) => containsQuery(item, this.query));
  }
}

class GridGalleryPage extends ListPageBase implements IListPage {
  readonly id = PAGE_IDS.gridGallery;
  readonly name = 'Grid Gallery';
  readonly title = 'Grid & Gallery Page';
  readonly icon = icon('\uECA5');
  readonly gridProperties: GridProperties = { type: GALLERY_LAYOUT, showTitle: true, showSubtitle: true };

  private readonly items: IListItem[];

  constructor(private readonly imagePage: ICommand, private readonly treePage: ICommand, private readonly multiPage: ICommand) {
    super();

    this.items = [
      new ListItemBase({
        command: this.imagePage,
        title: 'Image content',
        subtitle: 'Open the image showcase page',
        icon: gridIcon('IMG'),
      }),
      new ListItemBase({
        command: this.treePage,
        title: 'Tree content',
        subtitle: 'Nested content threads',
        icon: gridIcon('TREE'),
      }),
      new ListItemBase({
        command: this.multiPage,
        title: 'Mixed content',
        subtitle: 'Markdown + form + image',
        icon: gridIcon('MIX'),
      }),
      new ListItemBase({
        command: this.imagePage,
        title: 'Medium grid compatible',
        subtitle: 'Gallery pages can also use medium layout',
        icon: gridIcon('GRID'),
      }),
    ];
  }

  getItems(): IListItem[] {
    return this.items;
  }
}

type FilteredSample = {
  id: string;
  title: string;
  subtitle: string;
  section: string;
  filter: 'recent' | 'favorite';
  command: ICommand;
  icon: IconInfo;
};

class FilteredListPage extends DynamicListPageBase implements IDynamicListPage {
  readonly id = PAGE_IDS.filteredList;
  readonly name = 'Filtered List';
  readonly title = 'Filtered List Page';
  readonly icon = icon('\uE71C');
  readonly placeholderText = 'Filter by group and search within the current selection…';
  readonly showDetails = true;

  filters: Filters = {
    currentFilterId: 'all',
    filters: [
      { id: 'all', name: 'All', icon: icon('\uE8EF') },
      { id: 'recent', name: 'Recent', icon: icon('\uE823') },
      { id: 'favorite', name: 'Favorites', icon: icon('\uE735') },
    ],
  };

  private query = '';
  private readonly allItems: FilteredSample[];

  constructor(private readonly moreCommands: readonly ContextItem[], readmePage: ICommand, formPage: ICommand, treePage: ICommand) {
    super();

    this.allItems = [
      {
        id: 'recent-readme',
        title: 'README walkthrough',
        subtitle: 'Rich markdown content and details metadata',
        section: 'Recent',
        filter: 'recent',
        command: readmePage,
        icon: icon('\uE8A5'),
      },
      {
        id: 'recent-form',
        title: 'Form submission sample',
        subtitle: 'Adaptive Card form with submit handling',
        section: 'Recent',
        filter: 'recent',
        command: formPage,
        icon: icon('\uE70F'),
      },
      {
        id: 'favorite-tree',
        title: 'Tree comment thread',
        subtitle: 'Nested markdown content with child nodes',
        section: 'Favorites',
        filter: 'favorite',
        command: treePage,
        icon: icon('\uE8FD'),
      },
    ];
  }

  setSearchText(text: string): void {
    this.query = text.trim().toLowerCase();
    this.notifyItemsChanged();
  }

  setFilter(filterId: string): void {
    const available = this.filters.filters.filter((filter): filter is Filter => 'id' in filter);
    const nextFilterId = available.some((filter) => filter.id === filterId) ? filterId : 'all';

    this.filters = {
      ...this.filters,
      currentFilterId: nextFilterId,
    };

    this.notifyItemsChanged();
  }

  getItems(): IListItem[] {
    return this.allItems
      .filter((item) => this.filters.currentFilterId === 'all' || item.filter === this.filters.currentFilterId)
      .filter((item) => !this.query || containsQuery(item, this.query))
      .map(
        (item) =>
          new ListItemBase({
            command: item.command,
            title: item.title,
            subtitle: item.subtitle,
            icon: item.icon,
            section: item.section,
            tags: [
              makeTag(item.filter === 'recent' ? 'Recent' : 'Favorite', optionalColor(rgba(0, 120, 212))),
              makeTag(this.filters.currentFilterId, optionalColor(rgba(96, 94, 92))),
            ],
            moreCommands: [...this.moreCommands],
            details: {
              title: `${item.title} (${item.id})`,
              body: `Current filter: **${this.filters.currentFilterId}**\n\nSearch text: \`${this.query || '(none)'}\`.`,
            },
          })
      );
  }
}

class MarkdownContentPage extends ContentPageBase implements IContentPage {
  readonly id = PAGE_IDS.markdown;
  readonly name = 'Markdown';
  readonly title = 'Markdown Content Page';
  readonly icon = icon('\uE8A5');
  readonly details: Details;
  readonly commands: ContextItem[];

  constructor(openRepoCommand: ICommand, copyCommand: ICommand, goHomeCommand: ICommand) {
    super();

    this.details = {
      title: 'README details',
      body: 'This content page mirrors a README-style experience with commands and metadata.',
      metadata: [
        {
          key: 'Capabilities',
          data: {
            type: 'tags',
            tags: [makeTag('Markdown', optionalColor(rgba(0, 120, 212))), makeTag('Commands', optionalColor(rgba(16, 124, 16)))],
          } as DetailsTags,
        },
      ],
    };

    this.commands = [
      contextAction(openRepoCommand, 'Open repository', '\uE8A7', 'Show the PowerToys repo'),
      contextAction(copyCommand, 'Copy README heading', '\uE8C8', 'Copy sample text', shortcut(2, 67, 46)),
      contextAction(goHomeCommand, 'Go home', '\uE80F', 'Return to the main index'),
    ];
  }

  getContent(): Content[] {
    const overview: MarkdownContent = {
      type: SAMPLE_CONTENT_TYPE,
      body: `# ${EXTENSION_DISPLAY_NAME}\n\nThis page demonstrates **MarkdownContent** in the TypeScript SDK.\n\n## Highlights\n\n- Searchable list pages\n- Grid and gallery layouts\n- Filters and dynamic updates\n- Images, trees, plain text, and forms\n- Settings, fallback commands, and confirmation dialogs\n\n> Every sample in this extension is reachable from the main index page.`,
    };

    return [overview];
  }
}

class PlainTextContentPage extends ContentPageBase implements IContentPage {
  readonly id = PAGE_IDS.plainText;
  readonly name = 'Plain Text';
  readonly title = 'Plain Text Content Page';
  readonly icon = icon('\uE8A5');

  getContent(): Content[] {
    const content: PlainTextContent = {
      type: 'plainText',
      fontFamily: MONOSPACE_FONT,
      wrapWords: true,
      text: [
        '// Sample SDK viewer',
        'const provider = new SampleJSProvider();',
        'provider.topLevelCommands();',
        '',
        'This plain text page behaves like a tiny code viewer.',
        'Use it to demonstrate monospace rendering and word wrapping.',
      ].join('\n'),
    };

    return [content];
  }
}

class ImageContentPage extends ContentPageBase implements IContentPage {
  readonly id = PAGE_IDS.image;
  readonly name = 'Image';
  readonly title = 'Image Content Page';
  readonly icon = icon('\uE91B');

  private _cachedIcon: IconInfo | null = null;
  private _loading = false;

  private async ensureImageLoaded(): Promise<IconInfo> {
    if (this._cachedIcon) return this._cachedIcon;
    if (!this._loading) {
      this._loading = true;
      try {
        this._cachedIcon = await iconFromUrl(
          'https://raw.githubusercontent.com/microsoft/PowerToys/main/doc/images/overview/PT_hero_image.png'
        );
      } catch {
        // Fallback to glyph if fetch fails
        this._cachedIcon = icon('\uE91B');
      }
      this._loading = false;
    }
    return this._cachedIcon ?? icon('\uE91B');
  }

  async getContent(): Promise<Content[]> {
    const imageIcon = await this.ensureImageLoaded();

    const fullSize: ImageContent = {
      type: 'image',
      image: imageIcon,
      maxWidth: 600,
      maxHeight: 400,
    };

    const constrained: ImageContent = {
      type: 'image',
      image: imageIcon,
      maxWidth: 200,
      maxHeight: 200,
    };

    return [
      { type: 'markdown', body: '## Full-size image' } as MarkdownContent,
      fullSize,
      { type: 'markdown', body: '## Constrained image (200×200)' } as MarkdownContent,
      constrained,
    ];
  }
}

function makeTreeNode(body: string, children: Content[]): TreeContent {
  return {
    type: 'tree',
    rootContent: { type: 'markdown', body } as MarkdownContent,
    getChildren: () => children,
  };
}

class TreeContentPage extends ContentPageBase implements IContentPage {
  readonly id = PAGE_IDS.tree;
  readonly name = 'Tree';
  readonly title = 'Tree Content Page';
  readonly icon = icon('\uE8FD');

  getContent(): Content[] {
    const discussion = makeTreeNode('### @maintainer\nHow should a comprehensive sample page be organized?', [
      makeTreeNode('**@reviewer**\nStart with a searchable index and branch into focused demos.', [
        { type: 'markdown', body: '- Static list page\n- Dynamic list page\n- Rich details panel' } as MarkdownContent,
      ]),
      makeTreeNode('**@designer**\nGroup content samples separately so forms and images are easy to compare.', [
        { type: 'markdown', body: 'Nested trees work well for comment-thread style UIs.' } as MarkdownContent,
      ]),
    ]);

    return [
      {
        type: 'markdown',
        body: '## Nested content\nThe items below simulate a comment thread by mixing `TreeContent` and `MarkdownContent`.',
      } as MarkdownContent,
      discussion,
    ];
  }
}

class FormContentPage extends ContentPageBase implements IContentPage {
  readonly id = PAGE_IDS.form;
  readonly name = 'Form';
  readonly title = 'Form Content Page';
  readonly icon = icon('\uE70F');
  readonly details: Details = {
    title: 'Adaptive Card form',
    body: 'Submit this sample card to see how `FormContent.submitForm(inputs, data)` works.',
  };

  constructor(private readonly settings: Settings) {
    super();
  }

  getContent(): Content[] {
    const greeting = this.settings.getSetting<TextSetting>('greeting')?.value ?? 'Hello!';
    const templateJson = JSON.stringify({
      type: 'AdaptiveCard',
      version: '1.5',
      body: [
        { type: 'TextBlock', size: 'Medium', weight: 'Bolder', text: 'SDK Form Demo' },
        { type: 'Input.Text', id: 'name', label: 'Name', placeholder: 'Enter your name' },
        {
          type: 'Input.ChoiceSet',
          id: 'favoritePage',
          label: 'Favorite page',
          choices: [
            { title: 'Markdown', value: PAGE_IDS.markdown },
            { title: 'Form', value: PAGE_IDS.form },
            { title: 'Tree', value: PAGE_IDS.tree },
          ],
        },
        { type: 'Input.Toggle', id: 'notify', title: 'Send a host notification', valueOn: 'true', valueOff: 'false' },
        { type: 'ActionSet', actions: [{ type: 'Action.Submit', title: 'Submit' }] },
      ],
    });

    const dataJson = JSON.stringify({ greeting, submittedFrom: PAGE_IDS.form });

    const form: FormContent = {
      type: 'form',
      templateJson,
      dataJson,
      stateJson: JSON.stringify({ lastAction: 'idle' }),
      submitForm: (inputs: string, data: string): CommandResult => {
        const parsedInputs = parseJsonObject(inputs);
        const parsedData = parseJsonObject(data);
        const person = parsedInputs.name || 'friend';
        const favoritePage = parsedInputs.favoritePage || 'unknown';

        if (parsedInputs.notify === 'true') {
          sendNotification('sample-js-extension/formSubmitted', {
            context: 'page' as StatusContext,
            inputs: parsedInputs,
            data: parsedData,
          });
        }

        ExtensionHost.log(`Form submitted for ${person} (${favoritePage})`, 'success');
        return showToastResult(`${parsedData.greeting ?? greeting} ${person}! Favorite page: ${favoritePage}.`, {
          kind: 'keepOpen',
          args: { source: PAGE_IDS.form } as CommandResultArgs,
        });
      },
    };

    return [form];
  }
}

class MultiContentPage extends ContentPageBase implements IContentPage {
  readonly id = PAGE_IDS.multiContent;
  readonly name = 'Mixed Content';
  readonly title = 'Multi-Content Page';
  readonly icon = icon('\uECA5');

  getContent(): Content[] {
    const markdown: MarkdownContent = {
      type: 'markdown',
      body: '## Multiple content blocks\nThis page mixes **markdown**, **plain text**, **images**, and a **form** in one experience.',
    };

    const plainText: PlainTextContent = {
      type: 'plainText',
      fontFamily: 'userInterface',
      wrapWords: true,
      text: 'This block sits between the markdown header and the image to show mixed content ordering.',
    };

    const image: MarkdownContent = {
      type: 'markdown',
      body: '![Mixed content image](https://raw.githubusercontent.com/microsoft/PowerToys/main/doc/images/overview/PT_hero_image.png)',
    };

    const form: FormContent = {
      type: 'form',
      templateJson: JSON.stringify({
        type: 'AdaptiveCard',
        version: '1.5',
        body: [
          { type: 'TextBlock', text: 'Quick feedback', weight: 'Bolder' },
          { type: 'Input.Text', id: 'feedback', isMultiline: true, placeholder: 'What did you like most?' },
          { type: 'ActionSet', actions: [{ type: 'Action.Submit', title: 'Send' }] },
        ],
      }),
      dataJson: JSON.stringify({ source: PAGE_IDS.multiContent }),
      submitForm: (inputs: string, data: string): CommandResult => {
        const parsedInputs = parseJsonObject(inputs);
        const parsedData = parseJsonObject(data);
        return showToastResult(`Feedback recorded from ${parsedData.source}: ${parsedInputs.feedback || 'No feedback entered.'}`);
      },
    };

    return [markdown, plainText, image, form];
  }
}

class MainIndexPage extends DynamicListPageBase implements IDynamicListPage {
  readonly id = PAGE_IDS.main;
  readonly name = 'Samples';
  readonly title = EXTENSION_DISPLAY_NAME;
  readonly icon = icon('\uE943');
  readonly placeholderText = 'Search pages, commands, settings, and fallback demos…';
  readonly showDetails = true;
  readonly emptyContent: ICommandItem;

  private query = '';
  private readonly items: IListItem[];

  constructor(readmePage: ICommand, sections: readonly IListItem[]) {
    super();
    this.items = [...sections];
    this.emptyContent = new CommandItemBase({
      command: readmePage,
      title: 'No matches found — open the README page',
      subtitle: 'The markdown page explains every available sample.',
      icon: icon('\uE8A5'),
    });
  }

  setSearchText(text: string): void {
    this.query = text.trim().toLowerCase();
    this.notifyItemsChanged();
  }

  getItems(): IListItem[] {
    if (!this.query) {
      return this.items;
    }

    return this.items.filter((item) => item instanceof Separator || containsQuery(item, this.query));
  }
}

// === Fallback Handler ===

class QueryAwareFallbackItem extends FallbackCommandItemBase {
  readonly title = 'Run fallback command';
  readonly subtitle = 'Updates its title live as the user types';
  readonly icon = icon('\uE721');
  readonly command: QueryAwareFallbackCommand;
  displayTitle = 'Try typing to update this fallback suggestion';

  constructor(command: QueryAwareFallbackCommand, private readonly readmePage: ICommand) {
    super();
    this.command = command;
    this.moreCommands = [contextAction(this.readmePage, 'Open markdown page', '\uE8A5', 'Jump to the README sample')];
  }

  updateQuery(query: string): void {
    const trimmed = query.trim();
    this.command.setQuery(trimmed);
    this.displayTitle = trimmed
      ? `Fallback: act on “${trimmed}”`
      : 'Fallback: type anything to customize this suggestion';
  }
}

// === Provider ===

class SampleJSProvider extends CommandProviderBase implements ICommandProvider {
  readonly id = EXTENSION_ID;
  readonly displayName = EXTENSION_DISPLAY_NAME;
  readonly icon = icon('\uE943');

  private readonly registry = new CommandRegistry();

  private readonly _settings = new Settings()
    .add(new ToggleSetting('darkMode', 'Dark Mode', false, 'Enable dark mode'))
    .add(new TextSetting('greeting', 'Greeting', 'Hello!', 'Custom greeting message'))
    .add(
      new ChoiceSetSetting(
        'theme',
        'Theme',
        [
          { title: 'Default', value: 'default' },
          { title: 'Compact', value: 'compact' },
          { title: 'Comfortable', value: 'comfortable' },
        ],
        'default',
        'UI density'
      )
    );

  readonly settings: ICommandSettings = { settingsPage: this._settings.settingsPage };

  private readonly showToastCommand = this.registry.register(new ShowToastDemoCommand(this._settings));
  private readonly goToReadmeCommand = this.registry.register(
    new GoToPageCommand('goto-readme-command', 'Go To README Page', PAGE_IDS.markdown, '\uE8A5')
  );
  private readonly goHomeCommand = this.registry.register(new FixedResultCommand('go-home-command', 'Go Home', 'goHome', '\uE80F'));
  private readonly hideCommand = this.registry.register(new FixedResultCommand('hide-command', 'Hide Palette', 'hide', '\uE8BB'));
  private readonly confirmPrimaryCommand = this.registry.register(
    new FixedResultCommand(
      'confirm-primary-command',
      'Delete sample',
      'showToast',
      '\uE74D',
      { message: 'Confirmed action executed.' },
      'warning'
    )
  );
  private readonly confirmCommand = this.registry.register(
    new ConfirmableCommand({
      id: 'confirm-command',
      name: 'Confirm Before Action',
      title: 'Delete the sample item?',
      description: 'This demonstrates the confirmation dialog flow before an action runs.',
      primaryCommand: this.confirmPrimaryCommand,
      isCritical: true,
      icon: icon('\uE74D'),
    })
  );
  private readonly noOpCommand = this.registry.register(new NoOpCommand('noop-command', 'No operation'));
  private readonly openRepoCommand = this.registry.register(new OpenUrlCommand('https://github.com/microsoft/PowerToys', 'Open PowerToys'));
  private readonly copyRepoCommand = this.registry.register(
    new CopyTextCommand('https://github.com/microsoft/PowerToys', 'Copy PowerToys URL', 'Repository URL copied.')
  );
  private readonly fallbackCommand = this.registry.register(new QueryAwareFallbackCommand());

  private readonly markdownPage = this.registry.register(
    new MarkdownContentPage(this.openRepoCommand, this.copyRepoCommand, this.goHomeCommand)
  );
  private readonly plainTextPage = this.registry.register(new PlainTextContentPage());
  private readonly imagePage = this.registry.register(new ImageContentPage());
  private readonly treePage = this.registry.register(new TreeContentPage());
  private readonly formPage = this.registry.register(new FormContentPage(this._settings));
  private readonly multiContentPage = this.registry.register(new MultiContentPage());
  private readonly staticListPage = this.registry.register(
    new StaticListPage(
      [
        contextAction(this.copyRepoCommand, 'Copy link', '\uE8C8', 'Copy the repository URL', shortcut(2, 67, 46)),
        contextAction(this.openRepoCommand, 'Open repository', '\uE8A7', 'View the PowerToys repo'),
      ],
      this.noOpCommand,
      this.markdownPage,
      this.goHomeCommand
    )
  );
  private readonly detailsListPage = this.registry.register(
    new DetailsListPage(this.openRepoCommand, this.copyRepoCommand, this.showToastCommand, this.hideCommand, icon('\uE8A5'))
  );
  private readonly gridGalleryPage = this.registry.register(new GridGalleryPage(this.imagePage, this.treePage, this.multiContentPage));
  private readonly filteredListPage = this.registry.register(
    new FilteredListPage(
      [
        contextAction(this.showToastCommand, 'Toast', '\uE7F4', 'Run the toast demo'),
        contextAction(this.goHomeCommand, 'Go home', '\uE80F', 'Return to the main index'),
      ],
      this.markdownPage,
      this.formPage,
      this.treePage
    )
  );
  private readonly mainIndexPage = this.registry.register(new MainIndexPage(this.markdownPage, this.createMainIndexItems()));
  private readonly fallbackItem = new QueryAwareFallbackItem(this.fallbackCommand, this.markdownPage);

  private readonly pages: IPage[] = [
    this.mainIndexPage,
    this.staticListPage,
    this.detailsListPage,
    this.gridGalleryPage,
    this.filteredListPage,
    this.markdownPage,
    this.plainTextPage,
    this.imagePage,
    this.treePage,
    this.formPage,
    this.multiContentPage,
    this.settings.settingsPage,
  ];

  private readonly listPages: IListPage[] = [
    this.mainIndexPage,
    this.staticListPage,
    this.detailsListPage,
    this.gridGalleryPage,
    this.filteredListPage,
  ];

  private readonly dynamicListPages: IDynamicListPage[] = [this.mainIndexPage, this.detailsListPage, this.filteredListPage];

  private readonly contentPages: IContentPage[] = [
    this.markdownPage,
    this.plainTextPage,
    this.imagePage,
    this.treePage,
    this.formPage,
    this.multiContentPage,
    this.settings.settingsPage,
  ];

  constructor() {
    super();
    this.registry.register(this.settings.settingsPage);
    this.registry.registerAll(this.pages);
    this.registry.registerAll(this.listPages);
    this.registry.registerAll(this.dynamicListPages);
    this.registry.registerAll(this.contentPages);
  }

  initializeWithHost(host: IExtensionHost): void {
    super.initializeWithHost(host);
    host.log(`${EXTENSION_DISPLAY_NAME} initialized.`, 'info');
  }

  topLevelCommands(): ICommandItem[] {
    return [
      new CommandItemBase({
        command: this.mainIndexPage,
        title: EXTENSION_DISPLAY_NAME,
        subtitle: 'Comprehensive showcase of the TypeScript Command Palette SDK',
        icon: this.icon,
      }),
    ];
  }

  fallbackCommands(): IFallbackCommandItem[] {
    return [this.fallbackItem];
  }

  getCommand(id: string): ICommand | null {
    return this.registry.get(id);
  }

  private createMainIndexItems(): IListItem[] {
    const commandItems: IListItem[] = [
      new Separator('Commands'),
      new ListItemBase({
        command: this.showToastCommand,
        title: 'ShowToast command',
        subtitle: 'Shows a toast and writes to the host status channel',
        icon: icon('\uE7F4'),
        section: 'Commands',
        details: {
          title: 'showToast',
          body: 'Demonstrates `CommandResultKind.showToast`, `ExtensionHost`, `sendNotification`, and settings-backed text.',
        },
      }),
      new ListItemBase({
        command: this.goToReadmeCommand,
        title: 'GoToPage command',
        subtitle: 'Navigates to the markdown README page',
        icon: icon('\uE8A5'),
        section: 'Commands',
        details: { title: 'goToPage', body: 'Uses `GoToPageArgs` with `navigationMode = push`.' },
      }),
      new ListItemBase({
        command: this.confirmCommand,
        title: 'Confirm command',
        subtitle: 'Prompts before invoking a critical action',
        icon: icon('\uE74D'),
        section: 'Commands',
        details: {
          title: 'confirm',
          body: 'This item wraps a primary action with `ConfirmableCommand`.',
          metadata: [
            {
              key: 'Primary action',
              data: { type: 'commands', commands: [this.confirmPrimaryCommand] } as DetailsCommands,
            },
          ],
        },
      }),
      new ListItemBase({
        command: this.goHomeCommand,
        title: 'GoHome command',
        subtitle: 'Returns to the top-level extension entry',
        icon: icon('\uE80F'),
        section: 'Commands',
      }),
      new ListItemBase({
        command: this.hideCommand,
        title: 'Hide command',
        subtitle: 'Closes the palette without dismissing the extension',
        icon: icon('\uE8BB'),
        section: 'Commands',
      }),
    ];

    const pageItems: IListItem[] = [
      new Separator('List Pages'),
      this.makePageItem(this.staticListPage, 'Fixed items with sections, icons, tags, and separators', 'Pages'),
      this.makePageItem(this.detailsListPage, 'Dynamic items with rich details metadata and hero images', 'Pages'),
      this.makePageItem(this.gridGalleryPage, 'Gallery grid layout with image-backed tiles', 'Pages'),
      this.makePageItem(this.filteredListPage, 'Dynamic filters plus text search', 'Pages'),
      new Separator('Content Pages'),
      this.makePageItem(this.markdownPage, 'README-style markdown rendering with contextual commands', 'Content'),
      this.makePageItem(this.plainTextPage, 'Monospace plain text viewer', 'Content'),
      this.makePageItem(this.imagePage, 'Image content with max dimensions', 'Content'),
      this.makePageItem(this.treePage, 'Nested tree content for comment-thread style UI', 'Content'),
      this.makePageItem(this.formPage, 'Adaptive Card form with submit handling', 'Content'),
      this.makePageItem(this.multiContentPage, 'Multiple content types on a single page', 'Content'),
      new Separator('Settings'),
      new ListItemBase({
        command: this.settings.settingsPage,
        title: 'Settings page',
        subtitle: 'Toggle, text, and choice settings powered by the helper classes',
        icon: icon('\uE713'),
        section: 'Settings',
        details: {
          title: 'Settings helpers',
          body: 'The provider exposes `Settings`, `ToggleSetting`, `TextSetting`, and `ChoiceSetSetting` via `settings.settingsPage`.',
        },
      }),
    ];

    return [...commandItems, ...pageItems];
  }

  private makePageItem(page: IPage, subtitle: string, section: string): IListItem {
    return new ListItemBase({
      command: page,
      title: page.title,
      subtitle,
      icon: page.icon ?? this.icon,
      section,
      tags: [makeTag(section, optionalColor(rgba(0, 120, 212)))],
      details: {
        title: page.title,
        body: subtitle,
      },
      moreCommands: [
        contextAction(this.showToastCommand, 'Toast', '\uE7F4', 'Run the toast demo'),
        contextAction(this.goHomeCommand, 'Go home', '\uE80F', 'Return to the sample index'),
      ],
    });
  }
}

// === Start Server ===

export function activate(context: ActivationContext): ICommandProvider {
  return sdkActivate(context, () => new SampleJSProvider()) as ICommandProvider;
}

startJsonRpcServer(() => new SampleJSProvider());
