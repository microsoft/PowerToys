/**
 * VNode → CmdPal SDK translator.
 *
 * This module walks a captured VNode tree from the reconciler and produces
 * CmdPal-compatible data structures.
 *
 * Two APIs:
 *   translateTree(roots)   — original spike API, returns plain objects
 *   translateVNode(vnode)  — full translator, returns CmdPal SDK-compatible classes
 *
 * Mapping overview:
 *   Raycast VNode type    →  CmdPal concept
 *   ─────────────────────    ──────────────
 *   List                  →  DynamicListPage
 *   List.Item             →  IListItem
 *   List.Section          →  section grouping (IListItem.section)
 *   Detail                →  ContentPage + MarkdownContent
 *   Detail.Metadata.*     →  IDetails / IDetailsElement
 *   ActionPanel / Action  →  IContextItem / moreCommands
 *   Form                  →  ContentPage + FormContent
 */
import type { AnyVNode } from '../reconciler/vnode';
export { translateVNode } from './translate-vnode';
export type { IconData, IconInfo, Tag, ContextItem, DetailsElement, Details, } from './translate-vnode';
export { RaycastDynamicListPage, RaycastContentPage, RaycastListItem, RaycastMarkdownContent, } from './translate-vnode';
export interface TranslatedListItem {
    title: string;
    subtitle?: string;
    section?: string;
    icon?: unknown;
    tags?: Array<{
        text: string;
    }>;
    keywords?: string[];
    actions: TranslatedAction[];
}
export interface TranslatedAction {
    title: string;
    type: string;
    onAction?: (...args: unknown[]) => unknown;
    props: Record<string, unknown>;
}
export interface TranslatedListPage {
    type: 'list';
    isLoading: boolean;
    searchBarPlaceholder?: string;
    items: TranslatedListItem[];
    onSearchTextChange?: (text: string) => void;
}
export interface TranslatedDetailPage {
    type: 'detail';
    markdown: string;
    isLoading: boolean;
    metadata: Array<{
        key: string;
        value: unknown;
    }>;
}
export type TranslatedPage = TranslatedListPage | TranslatedDetailPage;
/**
 * Translate a VNode tree root into a CmdPal-compatible page description.
 */
export declare function translateTree(roots: AnyVNode[]): TranslatedPage | null;
//# sourceMappingURL=index.d.ts.map