# 02 - TypeScript SDK Reference

> **Package:** `@microsoft/cmdpal-sdk`
> **Version:** 0.1.0
> **Node.js:** ≥ 22.0.0
> **TypeScript:** ≥ 5.8

## Installation

```bash
npm install @microsoft/cmdpal-sdk@0.1.0
```

The shipped package is ESM (`"type": "module"`) and exposes `./dist/index.js` with type declarations from `./dist/index.d.ts`. Repository samples reference the SDK with `"@microsoft/cmdpal-sdk": "file:../../ts-sdk"`.


---

## Core Types

### Icon Types

```typescript
interface IconData {
  icon?: string;          // Font glyph character or file/URI path
  data?: string | null;   // Base64-encoded image data or data URI
}

interface IconInfo {
  light?: IconData;       // Icon for light theme
  dark?: IconData;        // Icon for dark theme
}
```

Icons can be provided as:
- **Font glyphs:** `{ icon: '\uE91B' }`, for Segoe Fluent Icons / MDL2 Assets
- **File paths:** `{ icon: 'C:\\path\\to\\icon.png' }`
- **Base64 data:** `{ data: 'iVBORw0KGgo...' }`, with raw base64-encoded image bytes
- **Data URIs:** `{ data: 'data:image/png;base64,iVBOR...' }`

### Color Types

```typescript
interface Color {
  r: number;  // 0-255
  g: number;
  b: number;
  a: number;  // 0-255 (default: 255)
}

interface OptionalColor {
  hasValue: boolean;
  color?: Color;
}
```

### Tags

```typescript
interface Tag {
  icon?: IconInfo | null;
  text: string;
  foreground?: OptionalColor | null;
  background?: OptionalColor | null;
  toolTip?: string;
}
```

### Key Chords

```typescript
interface KeyChord {
  modifiers: number;  // Bitmask: Ctrl=1, Alt=2, Shift=4, Win=8
  vkey: number;       // Virtual key code
  scanCode: number;
}
```

---

## Command Types

### ICommand

The base contract for all commands and pages.

```typescript
interface ICommand {
  id: string;               // Unique identifier
  name: string;             // Display name
  icon?: IconInfo | null;   // Optional icon
}
```

### IInvokableCommand

A command that can be executed.

```typescript
interface IInvokableCommand extends ICommand {
  invoke(): Promise<CommandResult> | CommandResult;
}
```

### CommandResult

Returned from `invoke()` to tell the host what to do next.

```typescript
type CommandResultKind =
  | 'dismiss'    // Close CmdPal
  | 'goHome'     // Navigate to home
  | 'goBack'     // Navigate back
  | 'hide'       // Hide CmdPal (keep state)
  | 'keepOpen'   // Stay on current page
  | 'goToPage'   // Navigate to a page
  | 'showToast'  // Show toast notification
  | 'confirm';   // Show confirmation dialog

interface CommandResult {
  kind: CommandResultKind;
  args?: CommandResultArgs;
}
```

### Result Args

```typescript
interface GoToPageArgs {
  pageId: string;
  navigationMode?: 'push' | 'goBack' | 'goHome';
}

interface ToastArgs {
  message: string;
  result?: CommandResult;  // What to do after toast is dismissed
}

interface ConfirmationArgs {
  title: string;
  description: string;
  primaryCommand?: ICommand;
  isPrimaryCommandCritical?: boolean;
}
```

### Helper: Creating Results

```typescript
// Navigate to a page
{ kind: 'goToPage', args: { pageId: 'my-page', navigationMode: 'push' } }

// Show a toast
{ kind: 'showToast', args: { message: 'Done!' } }

// Confirmation dialog
{ kind: 'confirm', args: { title: 'Delete?', description: 'This cannot be undone.' } }
```

---

## Item Types

### ICommandItem

A selectable item shown in lists.

```typescript
interface ICommandItem {
  command: ICommand;
  moreCommands?: ContextItem[];   // Right-click / overflow menu
  icon?: IconInfo | null;
  title: string;
  subtitle?: string;
}
```

### IListItem

Extended list item with metadata.

```typescript
interface IListItem extends ICommandItem {
  tags?: Tag[];
  details?: Details;
  section?: string;          // Section header text (creates visual grouping)
  textToSuggest?: string;    // Text to fill into search box on selection
}
```

### IFallbackCommandItem

A command that receives the user's search query in real-time.

```typescript
interface IFallbackCommandItem extends ICommandItem {
  fallbackHandler?: IFallbackHandler;
  displayTitle?: string;   // Dynamic title that updates as user types
}

interface IFallbackHandler {
  updateQuery(query: string): void | Promise<void>;
}
```

### ContextItem

An action in a right-click or overflow menu.

```typescript
interface ContextItem {
  command: ICommand;
  icon?: IconInfo | null;
  title: string;
  subtitle?: string;
  isCritical?: boolean;           // Show in red/destructive style
  requestedShortcut?: KeyChord;   // Keyboard shortcut hint
}
```

### Separator

A visual divider in lists.

```typescript
import { Separator } from '@microsoft/cmdpal-sdk';

// Untitled separator (horizontal line)
new Separator()

// Section header separator
new Separator('Section Title')
```

---

## Details Panel

Rich metadata shown alongside a selected list item.

```typescript
interface Details {
  heroImage?: IconInfo | null;
  title?: string;
  body?: string;              // Markdown-formatted body text
  metadata?: DetailsElement[];
}

interface DetailsElement {
  key: string;                // Label shown to the left
  data: DetailsData;          // Value shown to the right
}

// Discriminated union of detail data types
type DetailsData =
  | DetailsTags        // { type: 'tags', tags: Tag[] }
  | DetailsLink        // { type: 'link', link: string, text: string }
  | DetailsCommands    // { type: 'commands', commands: ICommand[] }
  | DetailsSeparator;  // { type: 'separator' }
```

---

## Page Types

### IListPage

A page that shows a scrollable list of items.

```typescript
interface IListPage extends IPage {
  searchText?: string;
  placeholderText?: string;
  showDetails?: boolean;           // Show details panel
  filters?: Filters | null;       // Filter bar
  gridProperties?: GridProperties | null;  // Grid/gallery layout
  hasMoreItems?: boolean;          // Infinite scroll
  emptyContent?: ICommandItem | null;
  getItems(): IListItem[] | Promise<IListItem[]>;
  loadMore?(): void | Promise<void>;
}
```

### IDynamicListPage

A list page that receives search input in real-time.

```typescript
interface IDynamicListPage extends IListPage {
  setSearchText(text: string): void | Promise<void>;
}
```

### IContentPage

A page that displays rich content (markdown, forms, images, trees).

```typescript
interface IContentPage extends IPage {
  getContent(): Content[] | Promise<Content[]>;
  details?: Details | null;
  commands?: ContextItem[];
}
```

### Filters

```typescript
interface Filter {
  id: string;
  name: string;
  icon?: IconInfo | null;
}

interface Filters {
  currentFilterId: string;
  filters: Array<Filter | { separator: true }>;
}
```

### Grid Properties

```typescript
type GridLayoutType = 'small' | 'medium' | 'gallery';

interface GridProperties {
  type: GridLayoutType;
  showTitle?: boolean;
  showSubtitle?: boolean;
}
```

---

## Content Types

Content pages display an array of `Content` items:

```typescript
type ContentType = 'markdown' | 'form' | 'tree' | 'plainText' | 'image';

interface MarkdownContent {
  type: 'markdown';
  body: string;
}

interface FormContent {
  type: 'form';
  templateJson: string;   // Adaptive Card JSON template
  dataJson: string;        // Form data values JSON
  stateJson?: string;
  submitForm(inputs: string, data: string): CommandResult | Promise<CommandResult>;
}

interface ImageContent {
  type: 'image';
  image: IconInfo;        // Base64-encoded image data (use iconFromUrl/iconFromFile)
  maxWidth?: number;
  maxHeight?: number;
}

interface PlainTextContent {
  type: 'plainText';
  text: string;
  fontFamily?: 'userInterface' | 'monospace';
  wrapWords?: boolean;
}

interface TreeContent {
  type: 'tree';
  rootContent: Content;
  getChildren(): Content[] | Promise<Content[]>;
}
```

---

## Settings

```typescript
import { Settings, ToggleSetting, TextSetting, ChoiceSetSetting } from '@microsoft/cmdpal-sdk';

const settings = new Settings();

// Toggle (boolean)
settings.add(new ToggleSetting('darkMode', 'Dark Mode', true, 'Enable dark theme'));

// Text input
settings.add(new TextSetting('apiKey', 'API Key', '', 'Your API key'));

// Choice set (dropdown)
settings.add(new ChoiceSetSetting('language', 'Language', [
  { title: 'English', value: 'en' },
  { title: 'Spanish', value: 'es' },
], 'en'));

// Read values
const darkMode = settings.getSetting<ToggleSetting>('darkMode')?.value;

// Expose in provider
class MyProvider extends CommandProviderBase {
  settings = settings;
  // ... settings page auto-generated from settings definitions
}
```

---

## Base Classes

### CommandProviderBase

The entry point for every extension.

```typescript
class MyProvider extends CommandProviderBase implements ICommandProvider {
  readonly id = 'my-extension';
  readonly displayName = 'My Extension';
  readonly icon = iconFromGlyph('\uE8A5');

  topLevelCommands(): ICommandItem[] | Promise<ICommandItem[]> {
    return [ /* ... */ ];
  }

  fallbackCommands(): IFallbackCommandItem[] | Promise<IFallbackCommandItem[]> {
    return [];
  }

  getCommand(id: string): ICommand | null | Promise<ICommand | null> {
    return null;
  }

  settings?: ICommandSettings | null;

  initializeWithHost(host: IExtensionHost): void {
    // Store the host if this provider needs it.
  }

  dispose(): void {
    // Release resources before shutdown.
  }
}
```

### ListPageBase

```typescript
class MyListPage extends ListPageBase implements IListPage {
  readonly id = 'my-list';
  readonly name = 'My List';
  readonly title = 'My List Page';

  getItems(): IListItem[] {
    return [ /* ... */ ];
  }

  // Optional
  showDetails?: boolean;
  filters?: Filters | null;
  gridProperties?: GridProperties | null;
  placeholderText?: string;
  loadMore(): void | Promise<void>;
}
```

### DynamicListPageBase

```typescript
class MySearchPage extends DynamicListPageBase implements IDynamicListPage {
  readonly id = 'my-search';
  readonly name = 'Search';
  readonly title = 'Search Page';
  private query = '';

  setSearchText(text: string): void {
    this.query = text;
    this.notifyItemsChanged();  // Tell host to re-fetch items
  }

  getItems(): IListItem[] {
    return allItems.filter(item => item.title.includes(this.query));
  }
}
```

### ContentPageBase

```typescript
class MyContentPage extends ContentPageBase implements IContentPage {
  readonly id = 'my-content';
  readonly name = 'Content';
  readonly title = 'My Content Page';

  getContent(): Content[] {
    return [
      { type: 'markdown', body: '# Hello\n\nThis is **markdown** content.' },
    ];
  }
}
```

### InvokableCommandBase

```typescript
class MyCommand extends InvokableCommandBase implements IInvokableCommand {
  readonly id = 'my-command';
  readonly name = 'Do Something';

  invoke(): CommandResult {
    // Do work...
    return { kind: 'showToast', args: { message: 'Done!' } };
  }
}
```

---

## Built-In Commands

| Class | Purpose | `invoke()` returns |
|-------|---------|-------------------|
| `NoOpCommand` | Does nothing | `{ kind: 'keepOpen' }` |
| `OpenUrlCommand` | Opens a URL in the default browser | `{ kind: 'dismiss' }` |
| `CopyTextCommand` | Copies text to clipboard | Toast with copy confirmation |
| `ConfirmableCommand` | Shows a confirmation dialog | `{ kind: 'confirm', args: {...} }` |

---

## Icon Helpers

```typescript
import { iconFromGlyph, iconFromBase64, iconFromUrl, iconFromFile } from '@microsoft/cmdpal-sdk';

// Font glyph (Segoe Fluent Icons)
const icon = iconFromGlyph('\uE91B');

// Base64-encoded image data
const icon = iconFromBase64('iVBORw0KGgoAAAANSUhEUg...');

// Fetch image from URL (async, downloads and encodes as base64)
const icon = await iconFromUrl('https://example.com/icon.png');

// Read local file (async, reads and encodes as base64)
const icon = await iconFromFile('./assets/icon.png');
```

---

## Runtime API

### ExtensionHost

Static bridge for communicating with the CmdPal host.

```typescript
import { ExtensionHost } from '@microsoft/cmdpal-sdk';

// Logging
ExtensionHost.log('Something happened');
ExtensionHost.log('Error occurred', 'error');

// Status bar
ExtensionHost.showStatus('Loading...', 'info', { isIndeterminate: true });
ExtensionHost.hideStatus('loading-id');

// Clipboard
ExtensionHost.copyToClipboard('Hello, clipboard!');
```

### Activation

```typescript
import { activate, run, startJsonRpcServer, type ActivationContext, type ProviderFactory } from '@microsoft/cmdpal-sdk';

const factory: ProviderFactory = () => new MyProvider();

// Standard activation pattern
startJsonRpcServer(factory);

// Alias for startJsonRpcServer
run(factory);

// Activation helper
const provider = activate({ extensionId: 'my-extension', extensionDirectory: process.cwd() } satisfies ActivationContext, factory);
```

### Notifications

```typescript
import { sendNotification } from '@microsoft/cmdpal-sdk';

// Tell the host that a list page's items have changed
sendNotification('listPage/itemsChanged', { pageId: 'my-list' });

// Tell the host that a command's properties changed
sendNotification('command/propChanged', {
  commandId: 'my-fallback',
  properties: { displayTitle: 'Search: query text' },
});
```

---

## C# Toolkit Equivalence

| C# Toolkit | TypeScript SDK |
|------------|----------------|
| `ListPage` | `ListPageBase` |
| `DynamicListPage` | `DynamicListPageBase` |
| `ContentPage` | `ContentPageBase` |
| `InvokableCommand` | `InvokableCommandBase` |
| `CommandItem` | `CommandItemBase` |
| `ListItem` | `ListItemBase` |
| `FallbackCommandItem` | `FallbackCommandItemBase` |
| `Separator` | `Separator` |
| `NoOpCommand` | `NoOpCommand` |
| `OpenUrlCommand` | `OpenUrlCommand` |
| `CopyTextCommand` | `CopyTextCommand` |
| `ConfirmableCommand` | `ConfirmableCommand` |
| `Settings`/`SettingsPage` | `Settings` (auto-generates `IContentPage`) |
| `ToggleSetting` | `ToggleSetting` |
| `TextSetting` | `TextSetting` |
| `ChoiceSetSetting` | `ChoiceSetSetting` |
| `IconHelpers.FromRelativePath` | `iconFromFile` |
| `IconInfo.FromStream` | `iconFromBase64` / `iconFromUrl` |
