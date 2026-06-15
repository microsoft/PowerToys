/**
 * Core type definitions for the Command Palette extension SDK.
 * Mirrors the WinRT IDL interfaces as TypeScript types.
 */
export type CommandResultKind = 'dismiss' | 'goHome' | 'goBack' | 'hide' | 'keepOpen' | 'goToPage' | 'showToast' | 'confirm';
export type NavigationMode = 'push' | 'goBack' | 'goHome';
export type MessageState = 'info' | 'success' | 'warning' | 'error';
export type StatusContext = 'page' | 'extension';
export type ContentType = 'markdown' | 'form' | 'tree' | 'plainText' | 'image';
export type GridLayoutType = 'small' | 'medium' | 'gallery';
export type FontFamily = 'userInterface' | 'monospace';
export interface IconData {
    icon?: string;
    data?: string | null;
}
export interface IconInfo {
    light?: IconData;
    dark?: IconData;
}
export interface Color {
    r: number;
    g: number;
    b: number;
    a: number;
}
export interface OptionalColor {
    hasValue: boolean;
    color?: Color;
}
export interface Tag {
    icon?: IconInfo | null;
    text: string;
    foreground?: OptionalColor | null;
    background?: OptionalColor | null;
    toolTip?: string;
}
export interface KeyChord {
    modifiers: number;
    vkey: number;
    scanCode: number;
}
export interface DetailsData {
    type: 'tags' | 'link' | 'commands' | 'separator';
}
export interface DetailsTags extends DetailsData {
    type: 'tags';
    tags: Tag[];
}
export interface DetailsLink extends DetailsData {
    type: 'link';
    link: string;
    text: string;
}
export interface DetailsCommands extends DetailsData {
    type: 'commands';
    commands: ICommand[];
}
export interface DetailsSeparator extends DetailsData {
    type: 'separator';
}
export interface DetailsElement {
    key: string;
    data: DetailsData;
}
export interface Details {
    heroImage?: IconInfo | null;
    title?: string;
    body?: string;
    metadata?: DetailsElement[];
}
export interface Filter {
    id: string;
    name: string;
    icon?: IconInfo | null;
}
export interface Filters {
    currentFilterId: string;
    filters: Array<Filter | {
        separator: true;
    }>;
}
export interface GridProperties {
    type: GridLayoutType;
    showTitle?: boolean;
    showSubtitle?: boolean;
}
export interface ICommand {
    id: string;
    name: string;
    icon?: IconInfo | null;
}
export interface IInvokableCommand extends ICommand {
    invoke(): Promise<CommandResult> | CommandResult;
}
export interface CommandResult {
    kind: CommandResultKind;
    args?: CommandResultArgs;
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
    result?: CommandResult;
}
export interface ConfirmationArgs extends CommandResultArgs {
    title: string;
    description: string;
    primaryCommand?: ICommand;
    isPrimaryCommandCritical?: boolean;
}
export interface ContextItem {
    command: ICommand;
    icon?: IconInfo | null;
    title: string;
    subtitle?: string;
    isCritical?: boolean;
    requestedShortcut?: KeyChord;
}
export interface ICommandItem {
    command: ICommand;
    moreCommands?: ContextItem[];
    icon?: IconInfo | null;
    title: string;
    subtitle?: string;
}
export interface IListItem extends ICommandItem {
    tags?: Tag[];
    details?: Details;
    section?: string;
    textToSuggest?: string;
}
export interface IFallbackCommandItem extends ICommandItem {
    fallbackHandler?: IFallbackHandler;
    displayTitle?: string;
}
export interface IPage extends ICommand {
    title: string;
    isLoading?: boolean;
    accentColor?: OptionalColor | null;
}
export interface IListPage extends IPage {
    searchText?: string;
    placeholderText?: string;
    showDetails?: boolean;
    filters?: Filters | null;
    gridProperties?: GridProperties | null;
    hasMoreItems?: boolean;
    emptyContent?: ICommandItem | null;
    getItems(): Promise<IListItem[]> | IListItem[];
    loadMore?(): Promise<void> | void;
}
export interface IDynamicListPage extends IListPage {
    setSearchText(text: string): void;
}
export interface Content {
    type: ContentType;
}
export interface MarkdownContent extends Content {
    type: 'markdown';
    body: string;
}
export interface FormContent extends Content {
    type: 'form';
    templateJson: string;
    dataJson: string;
    stateJson?: string;
    submitForm(inputs: string, data: string): Promise<CommandResult> | CommandResult;
}
export interface TreeContent extends Content {
    type: 'tree';
    rootContent: Content;
    getChildren(): Promise<Content[]> | Content[];
}
export interface PlainTextContent extends Content {
    type: 'plainText';
    text: string;
    fontFamily?: FontFamily;
    wrapWords?: boolean;
}
export interface ImageContent extends Content {
    type: 'image';
    image: IconInfo;
    maxWidth?: number;
    maxHeight?: number;
}
export interface IContentPage extends IPage {
    getContent(): Promise<Content[]> | Content[];
    details?: Details | null;
    commands?: ContextItem[];
}
export interface ICommandSettings {
    settingsPage: IContentPage;
}
export interface IFallbackHandler {
    updateQuery(query: string): void;
}
export interface ProgressState {
    isIndeterminate: boolean;
    progressPercent?: number;
}
export interface StatusMessage {
    state: MessageState;
    progress?: ProgressState;
    message: string;
}
export interface IExtensionHost {
    log(message: string, state?: MessageState): void;
    showStatus(message: string, state?: MessageState, progress?: ProgressState): void;
    hideStatus(messageId: string): void;
}
export interface ICommandProvider {
    id: string;
    displayName: string;
    icon?: IconInfo | null;
    frozen?: boolean;
    settings?: ICommandSettings | null;
    topLevelCommands(): Promise<ICommandItem[]> | ICommandItem[];
    fallbackCommands?(): Promise<IFallbackCommandItem[]> | IFallbackCommandItem[];
    getCommand?(id: string): Promise<ICommand | null> | ICommand | null;
    initializeWithHost?(host: IExtensionHost): void;
    dispose?(): void;
}
export interface ActivationContext {
    extensionId: string;
    extensionDirectory: string;
}
//# sourceMappingURL=types.d.ts.map