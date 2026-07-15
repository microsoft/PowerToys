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

export interface IconData {
  /** Font glyph character, file path, or URI. */
  icon?: string;
  /** Base64-encoded image bytes or a data URI. */
  data?: string | null;
}

export interface IconInfo {
  /** Icon used with the light theme. */
  light?: IconData;
  /** Icon used with the dark theme. */
  dark?: IconData;
}

// === Colors ===

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

export interface OptionalColor {
  hasValue: boolean;
  color?: Color;
}

// === Tags ===

export interface Tag {
  icon?: IconInfo | null;
  text: string;
  foreground?: OptionalColor | null;
  background?: OptionalColor | null;
  toolTip?: string;
}

// === Key chords ===

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
  invoke(): Promise<CommandResult> | CommandResult;
}

export interface CommandResultArgs {
  [key: string]: unknown;
}

export interface GoToPageArgs extends CommandResultArgs {
  pageId: string;
  navigationMode?: NavigationMode;
}

export interface ToastArgs extends CommandResultArgs {
  message: string;
  /** What the host should do after the toast is dismissed. */
  result?: CommandResult;
}

export interface ConfirmationArgs extends CommandResultArgs {
  title: string;
  description: string;
  primaryCommand?: ICommand;
  isPrimaryCommandCritical?: boolean;
}

/** Returned from `invoke()` to tell the host what to do next. */
export interface CommandResult {
  kind: CommandResultKind;
  args?: CommandResultArgs;
}

// === Command items ===

/** An action shown in a right-click or overflow menu. */
export interface ContextItem {
  command: ICommand;
  /** Nested right-click / overflow menu actions shown as a sub-menu. */
  moreCommands?: ContextItem[];
  icon?: IconInfo | null;
  title: string;
  subtitle?: string;
  /** Render with a destructive (critical) style. */
  isCritical?: boolean;
  /** Keyboard shortcut hint shown to the user. */
  requestedShortcut?: KeyChord;
}

/** A selectable item shown in lists. */
export interface ICommandItem {
  command: ICommand;
  /** Right-click / overflow menu actions. */
  moreCommands?: ContextItem[];
  icon?: IconInfo | null;
  title: string;
  subtitle?: string;
}

/** An extended list item with additional metadata. */
export interface IListItem extends ICommandItem {
  tags?: Tag[];
  details?: Details;
  /** Section header text used to visually group items. */
  section?: string;
  /** Text placed in the search box when the item is selected. */
  textToSuggest?: string;
}

/** Receives the user's search query in real time. */
export interface IFallbackHandler {
  updateQuery(query: string): void | Promise<void>;
}

/** A command that receives the user's search query in real time. */
export interface IFallbackCommandItem extends ICommandItem {
  fallbackHandler?: IFallbackHandler;
  /** Dynamic title that updates as the user types. */
  displayTitle?: string;
}

// === Details panel ===

export interface DetailsTags {
  type: 'tags';
  tags: Tag[];
}

export interface DetailsLink {
  type: 'link';
  link: string;
  text: string;
}

export interface DetailsCommands {
  type: 'commands';
  commands: ICommand[];
}

export interface DetailsSeparator {
  type: 'separator';
}

/** Discriminated union of the value shown for a details metadata row. */
export type DetailsData = DetailsTags | DetailsLink | DetailsCommands | DetailsSeparator;

export interface DetailsElement {
  /** Label shown to the left. */
  key: string;
  /** Value shown to the right. */
  data: DetailsData;
}

/** Rich metadata shown alongside a selected list item. */
export interface Details {
  heroImage?: IconInfo | null;
  title?: string;
  /** Markdown-formatted body text. */
  body?: string;
  metadata?: DetailsElement[];
}

// === Filters ===

export interface Filter {
  id: string;
  name: string;
  icon?: IconInfo | null;
}

export interface FilterSeparator {
  separator: true;
}

export interface Filters {
  currentFilterId: string;
  filters: Array<Filter | FilterSeparator>;
}

// === Grid ===

export interface GridProperties {
  type: GridLayoutType;
  showTitle?: boolean;
  showSubtitle?: boolean;
}

// === Pages ===

/** The base contract for every page. */
export interface IPage extends ICommand {
  title: string;
  isLoading?: boolean;
  accentColor?: OptionalColor | null;
}

/** A page that shows a scrollable list of items. */
export interface IListPage extends IPage {
  searchText?: string;
  placeholderText?: string;
  /** Show the details panel next to the list. */
  showDetails?: boolean;
  filters?: Filters | null;
  gridProperties?: GridProperties | null;
  /** Whether more items can be loaded (infinite scroll). */
  hasMoreItems?: boolean;
  emptyContent?: ICommandItem | null;
  getItems(): IListItem[] | Promise<IListItem[]>;
  loadMore?(): void | Promise<void>;
}

/** A list page that receives search input in real time. */
export interface IDynamicListPage extends IListPage {
  setSearchText(text: string): void | Promise<void>;
}

/** A page that displays rich content (markdown, forms, images, trees). */
export interface IContentPage extends IPage {
  getContent(): Content[] | Promise<Content[]>;
  details?: Details | null;
  commands?: ContextItem[];
}

// === Content ===

export interface MarkdownContent {
  type: 'markdown';
  body: string;
}

export interface FormContent {
  type: 'form';
  /** Adaptive Card JSON template. */
  templateJson: string;
  /** Form data values JSON. */
  dataJson: string;
  stateJson?: string;
  submitForm(inputs: string, data: string): CommandResult | Promise<CommandResult>;
}

export interface ImageContent {
  type: 'image';
  /** Base64-encoded image data. Build with `iconFromUrl` or `iconFromFile`. */
  image: IconInfo;
  maxWidth?: number;
  maxHeight?: number;
}

export interface PlainTextContent {
  type: 'plainText';
  text: string;
  fontFamily?: FontFamily;
  wrapWords?: boolean;
}

export interface TreeContent {
  type: 'tree';
  rootContent: Content;
  getChildren(): Content[] | Promise<Content[]>;
}

/** Discriminated union of everything a content page can display. */
export type Content = MarkdownContent | FormContent | ImageContent | PlainTextContent | TreeContent;

// === Settings ===

export interface ICommandSettings {
  settingsPage: IContentPage;
}

// === Progress and status ===

export interface ProgressState {
  isIndeterminate: boolean;
  progressPercent?: number;
}

export interface StatusMessage {
  state: MessageState;
  progress?: ProgressState;
  message: string;
}

// === Extension host bridge ===

/** The surface an extension uses to talk back to the Command Palette host. */
export interface IExtensionHost {
  log(message: string, state?: MessageState): void;
  showStatus(message: string, state?: MessageState, progress?: ProgressState): void;
  hideStatus(messageId: string): void;
  copyToClipboard(text: string): void;
}

// === Command provider ===

/** The entry point for every extension. */
export interface ICommandProvider {
  id: string;
  displayName: string;
  icon?: IconInfo | null;
  frozen?: boolean;
  settings?: ICommandSettings | null;

  topLevelCommands(): ICommandItem[] | Promise<ICommandItem[]>;
  fallbackCommands?(): IFallbackCommandItem[] | Promise<IFallbackCommandItem[]>;
  getCommand?(id: string): ICommand | null | Promise<ICommand | null>;
  initializeWithHost?(host: IExtensionHost): void;
  dispose?(): void;
}

// === Activation ===

export interface ActivationContext {
  extensionId: string;
  extensionDirectory: string;
}
