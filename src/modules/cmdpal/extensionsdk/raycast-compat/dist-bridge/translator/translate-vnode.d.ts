/**
 * VNode → CmdPal SDK type translator.
 *
 * Converts VNode trees captured by the React reconciler into CmdPal-
 * compatible SDK objects. Produces concrete classes that implement the
 * same interfaces as the CmdPal TypeScript SDK.
 *
 * Mapping:
 *   List                → RaycastDynamicListPage  (IDynamicListPage)
 *   List.Item           → RaycastListItem         (IListItem)
 *   List.Section        → section grouping on items
 *   Detail              → RaycastContentPage      (IContentPage)
 *   Detail.Markdown     → RaycastMarkdownContent  (IMarkdownContent)
 *   Detail.Metadata.*   → IDetails / IDetailsElement
 *   ActionPanel/Action  → IContextItem on moreCommands
 *   Form                → RaycastContentPage with form description
 *   Unknown             → null (graceful fallback)
 */
export interface IconData {
    icon?: string;
    data?: string;
}
export interface IconInfo {
    light?: IconData | string;
    dark?: IconData | string;
}
export interface Tag {
    text: string;
    icon?: IconInfo;
    toolTip?: string;
}
export interface ContextItem {
    title?: string;
    subtitle?: string;
    icon?: IconInfo;
    command?: unknown;
    moreCommands?: ContextItem[];
    isCritical?: boolean;
}
export interface DetailsElement {
    key: string;
    icon?: IconInfo;
    data?: unknown;
}
export interface Details {
    title?: string;
    body?: string;
    heroImage?: IconInfo;
    metadata?: DetailsElement[];
}
/**
 * ListItem produced by the translator.
 * Implements the IListItem shape from CmdPal SDK.
 */
export declare class RaycastListItem {
    title: string;
    subtitle: string;
    icon?: IconInfo;
    command?: unknown;
    moreCommands: ContextItem[];
    tags: Tag[];
    details?: Details;
    section: string;
    textToSuggest: string;
}
/**
 * DynamicListPage produced by translating a Raycast List VNode.
 * Implements the IDynamicListPage shape from CmdPal SDK.
 */
export declare class RaycastDynamicListPage {
    readonly _type = "dynamicListPage";
    id: string;
    name: string;
    icon?: IconInfo;
    placeholderText: string;
    searchText: string;
    showDetails: boolean;
    hasMoreItems: boolean;
    gridProperties?: {
        layout: string;
        showTitle: boolean;
        showSubtitle: boolean;
    };
    PropChanged?: (args: unknown) => void;
    ItemsChanged?: (args: unknown) => void;
    private _items;
    private _onSearchTextChange?;
    constructor(items?: RaycastListItem[], onSearchTextChange?: (text: string) => void);
    getItems(): RaycastListItem[];
    updateSearchText(oldSearch: string, newSearch: string): void;
    setSearchText(searchText: string): void;
    loadMore(): void;
}
/**
 * MarkdownContent produced by translating Raycast Detail markdown.
 * Implements the IMarkdownContent shape from CmdPal SDK.
 */
export declare class RaycastMarkdownContent {
    type: string;
    id: string;
    body: string;
    PropChanged?: (args: unknown) => void;
    constructor(body?: string);
}
/**
 * ContentPage produced by translating Raycast Detail or Form VNodes.
 * Implements the IContentPage shape from CmdPal SDK.
 */
export declare class RaycastContentPage {
    readonly _type = "contentPage";
    id: string;
    name: string;
    icon?: IconInfo;
    details?: Details;
    commands: ContextItem[];
    PropChanged?: (args: unknown) => void;
    ItemsChanged?: (args: unknown) => void;
    private _content;
    constructor(content?: RaycastMarkdownContent[]);
    getContent(): RaycastMarkdownContent[];
}
/**
 * Translate a single VNode into a CmdPal-compatible SDK object.
 *
 * @param vnode - The root VNode from the reconciler tree
 * @returns A CmdPal page object, or null for unknown/unsupported types
 */
export declare function translateVNode(vnode: {
    type: string;
    props: Record<string, unknown>;
    children: unknown[];
}): RaycastDynamicListPage | RaycastContentPage | null;
//# sourceMappingURL=translate-vnode.d.ts.map