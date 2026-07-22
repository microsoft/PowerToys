// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Core type definitions for the Command Palette extension SDK.
 *
 * These types mirror the Command Palette host contracts and the JSON-RPC wire
 * protocol described in the specification documents `02-typescript-sdk.md` and
 * `03-jsonrpc-protocol.md`.
 */

// === Enumerations (string unions) ===

/** Tells the host what to do after a command is invoked. */
export type CommandResultKind =
  'dismiss' | 'goHome' | 'goBack' | 'hide' | 'keepOpen' | 'goToPage' | 'showToast' | 'confirm';

/** How a `goToPage` result should affect the navigation stack. */
export type NavigationMode = 'push' | 'goBack' | 'goHome';

/** Severity of a log or status message. */
export type MessageState = 'info' | 'success' | 'warning' | 'error';

/** Scope a status message applies to. */
export type StatusContext = 'page' | 'extension';

/** Discriminator for the {@link Content} union. */
export type ContentType = 'markdown' | 'form' | 'tree' | 'plainText' | 'image';

/** Layout used by a list page rendered as a grid. */
export type GridLayoutType = 'small' | 'medium' | 'gallery';

/** Font family used by plain text content. */
export type FontFamily = 'userInterface' | 'monospace';

/** Identifies the kind of page a command navigates to, if any. */
export type PageType = 'listPage' | 'dynamicListPage' | 'contentPage';

// === Icons ===

/**
 * A single icon source. Provide either a {@link IconData.icon} reference (glyph,
 * path, or URI) or inline {@link IconData.data} image bytes. Prefer the helpers
 * in `helpers.ts` ({@link iconFromGlyph}, {@link iconFromBase64},
 * {@link iconFromFile}, {@link iconFromUrl}) to build these values.
 */
export interface IconData {
  /** Font glyph character, file path, or URI. */
  icon?: string;
  /** Base64-encoded image bytes or a data URI. */
  data?: string | null;
}

/**
 * A theme-aware icon. Supply a {@link IconInfo.light} and/or
 * {@link IconInfo.dark} variant so the palette can pick the one that matches the
 * active theme. The icon helpers return the same {@link IconData} for both.
 */
export interface IconInfo {
  /** Icon used with the light theme. */
  light?: IconData;
  /** Icon used with the dark theme. */
  dark?: IconData;
}

// === Colors ===

/** A 32-bit RGBA color. Each channel is an integer from 0 to 255. */
export interface Color {
  /** Red channel, 0 to 255. */
  r: number;
  /** Green channel, 0 to 255. */
  g: number;
  /** Blue channel, 0 to 255. */
  b: number;
  /** Alpha channel, 0 to 255. Defaults to 255 (opaque). */
  a: number;
}

/**
 * A color that may be unset. When {@link OptionalColor.hasValue} is `false` the
 * host uses its default color and {@link OptionalColor.color} is ignored.
 */
export interface OptionalColor {
  /** Whether {@link OptionalColor.color} holds a color to apply. */
  hasValue: boolean;
  /** The color to apply when {@link OptionalColor.hasValue} is `true`. */
  color?: Color;
}

// === Tags ===

/** A small colored label shown on a list item, for example a status or badge. */
export interface Tag {
  /** Optional icon rendered before the tag text. */
  icon?: IconInfo | null;
  /** Text shown inside the tag. */
  text: string;
  /** Text color. When unset the host chooses a color based on the theme. */
  foreground?: OptionalColor | null;
  /** Fill color. When unset the host chooses a color based on the theme. */
  background?: OptionalColor | null;
  /** Tooltip shown when the user hovers over the tag. */
  toolTip?: string;
}

// === Key chords ===

/** A keyboard shortcut described by its modifiers and target key. */
export interface KeyChord {
  /** Modifier bitmask: Ctrl = 1, Alt = 2, Shift = 4, Win = 8. */
  modifiers: number;
  /** Virtual key code. */
  vkey: number;
  /** Hardware scan code. */
  scanCode: number;
}

// === Commands ===

/** The base contract for every command and page. */
export interface ICommand {
  /** Unique identifier. */
  id: string;
  /** Display name. */
  name: string;
  /** Optional icon. */
  icon?: IconInfo | null;
}

/** A command that can be executed. */
export interface IInvokableCommand extends ICommand {
  /**
   * Runs the command's action.
   *
   * @returns A {@link CommandResult} telling the host what to do next, either
   * synchronously or as a promise.
   */
  invoke(): Promise<CommandResult> | CommandResult;
}

/** Extra values carried by a {@link CommandResult}, keyed by name. */
export interface CommandResultArgs {
  [key: string]: unknown;
}

/** Arguments for a `goToPage` {@link CommandResult}. */
export interface GoToPageArgs extends CommandResultArgs {
  /** Identifier of the page to navigate to. */
  pageId: string;
  /** How the target page is placed on the navigation stack. Defaults to `push`. */
  navigationMode?: NavigationMode;
}

/** Arguments for a `showToast` {@link CommandResult}. */
export interface ToastArgs extends CommandResultArgs {
  /** Text shown in the toast. */
  message: string;
  /** What the host should do after the toast is dismissed. */
  result?: CommandResult;
}

/** Arguments for a `confirm` {@link CommandResult}. */
export interface ConfirmationArgs extends CommandResultArgs {
  /** Title of the confirmation dialog. */
  title: string;
  /** Body text explaining what the user is confirming. */
  description: string;
  /** Command run when the user accepts the dialog. */
  primaryCommand?: ICommand;
  /** Render the primary action with a destructive (critical) style. */
  isPrimaryCommandCritical?: boolean;
}

/** Returned from `invoke()` to tell the host what to do next. */
export interface CommandResult {
  /** The action the host should take. */
  kind: CommandResultKind;
  /**
   * Extra data for the action, such as {@link GoToPageArgs},
   * {@link ToastArgs}, or {@link ConfirmationArgs}.
   */
  args?: CommandResultArgs;
}

// === Command items ===

/** An action shown in a right-click or overflow menu. */
export interface ContextItem {
  /** The command run when the action is chosen. */
  command: ICommand;
  /** Nested right-click / overflow menu actions shown as a sub-menu. */
  moreCommands?: ContextItem[];
  /** Icon shown next to the action. */
  icon?: IconInfo | null;
  /** Label shown for the action. */
  title: string;
  /** Secondary text shown below the title. */
  subtitle?: string;
  /** Render with a destructive (critical) style. */
  isCritical?: boolean;
  /** Keyboard shortcut hint shown to the user. */
  requestedShortcut?: KeyChord;
}

/** A selectable item shown in lists. */
export interface ICommandItem {
  /** The command run when the item is activated. */
  command: ICommand;
  /** Right-click / overflow menu actions. */
  moreCommands?: ContextItem[];
  /** Icon shown next to the item. Falls back to the command's icon when unset. */
  icon?: IconInfo | null;
  /** Primary text shown for the item. */
  title: string;
  /** Secondary text shown below the title. */
  subtitle?: string;
}

/** An extended list item with additional metadata. */
export interface IListItem extends ICommandItem {
  /** Colored labels shown on the item. */
  tags?: Tag[];
  /** Rich content shown in the details panel when the item is selected. */
  details?: Details;
  /** Section header text used to visually group items. */
  section?: string;
  /** Text placed in the search box when the item is selected. */
  textToSuggest?: string;
}

/** Receives the user's search query in real time. */
export interface IFallbackHandler {
  /**
   * Called whenever the search query changes.
   *
   * @param query The current text in the search box.
   */
  updateQuery(query: string): void | Promise<void>;
}

/** A command that receives the user's search query in real time. */
export interface IFallbackCommandItem extends ICommandItem {
  /** Handler notified as the user types so the item can update itself. */
  fallbackHandler?: IFallbackHandler;
  /** Dynamic title that updates as the user types. */
  displayTitle?: string;
}

// === Details panel ===

/** A details row value that renders a set of {@link Tag}s. */
export interface DetailsTags {
  /** Discriminator identifying this as a tags value. */
  type: 'tags';
  /** Tags shown in the row. */
  tags: Tag[];
}

/** A details row value that renders a clickable hyperlink. */
export interface DetailsLink {
  /** Discriminator identifying this as a link value. */
  type: 'link';
  /** Target URL opened when the link is clicked. */
  link: string;
  /** Text shown for the link. */
  text: string;
}

/** A details row value that renders a set of commands. */
export interface DetailsCommands {
  /** Discriminator identifying this as a commands value. */
  type: 'commands';
  /** Commands offered in the row. */
  commands: ICommand[];
}

/** A details row value that renders a horizontal divider. */
export interface DetailsSeparator {
  /** Discriminator identifying this as a separator value. */
  type: 'separator';
}

/** Discriminated union of the value shown for a details metadata row. */
export type DetailsData = DetailsTags | DetailsLink | DetailsCommands | DetailsSeparator;

/** A single labeled row in the {@link Details.metadata} section. */
export interface DetailsElement {
  /** Label shown to the left. */
  key: string;
  /** Value shown to the right. */
  data: DetailsData;
}

/** Rich metadata shown alongside a selected list item. */
export interface Details {
  /** Large image shown at the top of the details panel. */
  heroImage?: IconInfo | null;
  /** Heading shown above the body. */
  title?: string;
  /** Markdown-formatted body text. */
  body?: string;
  /** Labeled metadata rows shown below the body. */
  metadata?: DetailsElement[];
}

// === Filters ===

/** A single option in a list page's filter dropdown. */
export interface Filter {
  /** Identifier reported back through {@link Filters.currentFilterId}. */
  id: string;
  /** Label shown for the filter. */
  name: string;
  /** Optional icon shown next to the filter. */
  icon?: IconInfo | null;
}

/** A divider placed between {@link Filter}s in the dropdown. */
export interface FilterSeparator {
  /** Marks this entry as a separator rather than a selectable filter. */
  separator: true;
}

/** The set of filters offered by a list page and the current selection. */
export interface Filters {
  /** Identifier of the currently selected {@link Filter}. */
  currentFilterId: string;
  /** The available filters, optionally grouped with {@link FilterSeparator}s. */
  filters: Array<Filter | FilterSeparator>;
}

// === Grid ===

/** Controls how a list page is rendered as a grid of tiles. */
export interface GridProperties {
  /** Tile size and layout. */
  type: GridLayoutType;
  /** Whether each tile shows its item title. */
  showTitle?: boolean;
  /** Whether each tile shows its item subtitle. */
  showSubtitle?: boolean;
}

// === Pages ===

/** The base contract for every page. */
export interface IPage extends ICommand {
  /** Title shown at the top of the page. */
  title: string;
  /** Whether the page is currently loading; shows a progress indicator. */
  isLoading?: boolean;
  /** Accent color applied to the page. Uses the host default when unset. */
  accentColor?: OptionalColor | null;
}

/** A page that shows a scrollable list of items. */
export interface IListPage extends IPage {
  /** Current text in the search box. */
  searchText?: string;
  /** Placeholder shown in the search box while it is empty. */
  placeholderText?: string;
  /** Show the details panel next to the list. */
  showDetails?: boolean;
  /** Filter dropdown shown above the list, or `null` for none. */
  filters?: Filters | null;
  /** Grid layout settings, or `null` to render a plain list. */
  gridProperties?: GridProperties | null;
  /** Whether more items can be loaded (infinite scroll). */
  hasMoreItems?: boolean;
  /** Item shown when the list is empty, or `null` for none. */
  emptyContent?: ICommandItem | null;
  /**
   * Produces the items to display.
   *
   * @returns The current list items, synchronously or as a promise.
   */
  getItems(): IListItem[] | Promise<IListItem[]>;
  /**
   * Loads the next page of items when {@link IListPage.hasMoreItems} is `true`.
   * Called by the host as the user scrolls.
   */
  loadMore?(): void | Promise<void>;
}

/** A list page that receives search input in real time. */
export interface IDynamicListPage extends IListPage {
  /**
   * Called whenever the search text changes so the page can update its results.
   *
   * @param text The current text in the search box.
   */
  setSearchText(text: string): void | Promise<void>;
}

/** A page that displays rich content (markdown, forms, images, trees). */
export interface IContentPage extends IPage {
  /**
   * Produces the content blocks to render.
   *
   * @returns The page's {@link Content} blocks, synchronously or as a promise.
   */
  getContent(): Content[] | Promise<Content[]>;
  /** Rich metadata shown alongside the content, or `null` for none. */
  details?: Details | null;
  /** Overflow menu actions offered for the page. */
  commands?: ContextItem[];
}

// === Content ===

/** Content that renders a block of Markdown. */
export interface MarkdownContent {
  /** Discriminator identifying this as markdown content. */
  type: 'markdown';
  /** Markdown source to render. */
  body: string;
}

/** Content that renders an Adaptive Card form the user can submit. */
export interface FormContent {
  /** Discriminator identifying this as form content. */
  type: 'form';
  /** Adaptive Card JSON template. */
  templateJson: string;
  /** Form data values JSON. */
  dataJson: string;
  /** Adaptive Card runtime state JSON, if any. */
  stateJson?: string;
  /**
   * Handles a form submission.
   *
   * @param inputs JSON string of the submitted input values.
   * @param data JSON string of the form's bound data.
   * @returns A {@link CommandResult} describing what the host does next.
   */
  submitForm(inputs: string, data: string): CommandResult | Promise<CommandResult>;
}

/** Content that renders an image. */
export interface ImageContent {
  /** Discriminator identifying this as image content. */
  type: 'image';
  /** Base64-encoded image data. Build with `iconFromUrl` or `iconFromFile`. */
  image: IconInfo;
  /** Maximum rendered width in pixels. */
  maxWidth?: number;
  /** Maximum rendered height in pixels. */
  maxHeight?: number;
}

/** Content that renders unformatted text. */
export interface PlainTextContent {
  /** Discriminator identifying this as plain text content. */
  type: 'plainText';
  /** Text to render. */
  text: string;
  /** Font family used to render the text. Defaults to the UI font. */
  fontFamily?: FontFamily;
  /** Whether long lines wrap instead of scrolling horizontally. */
  wrapWords?: boolean;
}

/** Content that renders an expandable tree of nested content. */
export interface TreeContent {
  /** Discriminator identifying this as tree content. */
  type: 'tree';
  /** Content shown for the root node. */
  rootContent: Content;
  /**
   * Produces the child nodes of the tree.
   *
   * @returns The child {@link Content} nodes, synchronously or as a promise.
   */
  getChildren(): Content[] | Promise<Content[]>;
}

/** Discriminated union of everything a content page can display. */
export type Content = MarkdownContent | FormContent | ImageContent | PlainTextContent | TreeContent;

// === Settings ===

/** Settings surface for an extension, exposed as a content page. */
export interface ICommandSettings {
  /** Page that renders and persists the extension's settings. */
  settingsPage: IContentPage;
}

// === Progress and status ===

/** Progress information attached to a status message. */
export interface ProgressState {
  /** Whether progress is indeterminate (a spinner rather than a bar). */
  isIndeterminate: boolean;
  /** Completion percentage from 0 to 100, used when determinate. */
  progressPercent?: number;
}

/** A status message shown by the host, optionally with progress. */
export interface StatusMessage {
  /** Severity of the message. */
  state: MessageState;
  /** Progress information shown with the message, if any. */
  progress?: ProgressState;
  /** Text of the message. */
  message: string;
}

// === Extension host bridge ===

/** The surface an extension uses to talk back to the Command Palette host. */
export interface IExtensionHost {
  /**
   * Writes a message to the host log.
   *
   * @param message Text to log.
   * @param state Severity of the message. Defaults to `info`.
   */
  log(message: string, state?: MessageState): void;
  /**
   * Shows a status message in the palette status bar.
   *
   * @param message Text to display.
   * @param state Severity of the message. Defaults to `info`.
   * @param progress Optional progress shown alongside the message.
   */
  showStatus(message: string, state?: MessageState, progress?: ProgressState): void;
  /**
   * Hides a previously shown status message.
   *
   * @param messageId Identifier of the message to hide.
   */
  hideStatus(messageId: string): void;
  /**
   * Copies text to the system clipboard.
   *
   * @param text Text to place on the clipboard.
   */
  copyToClipboard(text: string): void;
}

// === Command provider ===

/** The entry point for every extension. */
export interface ICommandProvider {
  /** Unique identifier for the extension. */
  id: string;
  /** Human-readable name shown for the extension. */
  displayName: string;
  /** Icon shown for the extension. */
  icon?: IconInfo | null;
  /** When `true`, the palette caches commands and does not re-query them. */
  frozen?: boolean;
  /** Settings surface for the extension, or `null` when it has none. */
  settings?: ICommandSettings | null;

  /**
   * Produces the commands shown at the top level of the palette.
   *
   * @returns The top-level command items, synchronously or as a promise.
   */
  topLevelCommands(): ICommandItem[] | Promise<ICommandItem[]>;
  /**
   * Produces fallback commands that receive the search query as the user types.
   *
   * @returns The fallback command items, synchronously or as a promise.
   */
  fallbackCommands?(): IFallbackCommandItem[] | Promise<IFallbackCommandItem[]>;
  /**
   * Resolves a command by id, for commands not returned up front.
   *
   * @param id Identifier of the command to resolve.
   * @returns The command, or `null` when it is not found.
   */
  getCommand?(id: string): ICommand | null | Promise<ICommand | null>;
  /**
   * Receives the host bridge once, before any commands are requested.
   *
   * @param host Bridge used to talk back to the palette.
   */
  initializeWithHost?(host: IExtensionHost): void;
  /** Releases resources before the extension process exits. */
  dispose?(): void;
}

// === Activation ===

/** Details about the extension supplied by the host during activation. */
export interface ActivationContext {
  /** Unique identifier of the activated extension. */
  extensionId: string;
  /** Absolute path to the extension's installed directory. */
  extensionDirectory: string;
}
