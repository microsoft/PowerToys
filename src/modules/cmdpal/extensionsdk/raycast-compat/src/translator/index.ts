// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

import type { VNode, AnyVNode } from '../reconciler/vnode';
import { isElementVNode } from '../reconciler/vnode';

// Re-export the full translator and its types
export { translateVNode } from './translate-vnode';
export type {
  IconData,
  IconInfo,
  Tag,
  ContextItem,
  DetailsElement,
  Details,
} from './translate-vnode';
export {
  RaycastDynamicListPage,
  RaycastContentPage,
  RaycastListItem,
  RaycastMarkdownContent,
} from './translate-vnode';

// ── CmdPal-compatible output types (subset for the spike) ─────────────

export interface TranslatedListItem {
  title: string;
  subtitle?: string;
  section?: string;
  icon?: unknown;
  tags?: Array<{ text: string }>;
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
  metadata: Array<{ key: string; value: unknown }>;
}

export type TranslatedPage = TranslatedListPage | TranslatedDetailPage;

// ── Translator ────────────────────────────────────────────────────────

/**
 * Translate a VNode tree root into a CmdPal-compatible page description.
 */
export function translateTree(roots: AnyVNode[]): TranslatedPage | null {
  const root = roots.find(isElementVNode);
  if (!root) return null;

  switch (root.type) {
    case 'List':
      return translateList(root);
    case 'Detail':
      return translateDetail(root);
    default:
      return null;
  }
}

function translateList(node: VNode): TranslatedListPage {
  const items: TranslatedListItem[] = [];

  for (const child of node.children) {
    if (!isElementVNode(child)) continue;

    if (child.type === 'List.Item') {
      items.push(translateListItem(child));
    } else if (child.type === 'List.Section') {
      const sectionTitle = (child.props.title as string) ?? '';
      for (const sectionChild of child.children) {
        if (isElementVNode(sectionChild) && sectionChild.type === 'List.Item') {
          items.push(translateListItem(sectionChild, sectionTitle));
        }
      }
    }
  }

  return {
    type: 'list',
    isLoading: (node.props.isLoading as boolean) ?? false,
    searchBarPlaceholder: node.props.searchBarPlaceholder as string | undefined,
    items,
    onSearchTextChange: node.props.onSearchTextChange as ((text: string) => void) | undefined,
  };
}

function translateListItem(node: VNode, section?: string): TranslatedListItem {
  const actions = extractActions(node);

  const accessories = node.props.accessories as Array<{ text?: string }> | undefined;
  const tags = accessories?.map((a) => ({ text: a.text ?? '' })).filter((t) => t.text);

  return {
    title: (node.props.title as string) ?? '',
    subtitle: node.props.subtitle as string | undefined,
    section,
    icon: node.props.icon,
    tags: tags?.length ? tags : undefined,
    keywords: node.props.keywords as string[] | undefined,
    actions,
  };
}

function extractActions(node: VNode): TranslatedAction[] {
  const actions: TranslatedAction[] = [];

  for (const child of node.children) {
    if (!isElementVNode(child)) continue;

    if (child.type === 'ActionPanel') {
      for (const actionChild of child.children) {
        if (!isElementVNode(actionChild)) continue;

        if (actionChild.type === 'ActionPanel.Section') {
          for (const sectionChild of actionChild.children) {
            if (isElementVNode(sectionChild)) {
              actions.push(translateAction(sectionChild));
            }
          }
        } else {
          actions.push(translateAction(actionChild));
        }
      }
    }
  }

  return actions;
}

function translateAction(node: VNode): TranslatedAction {
  return {
    title: (node.props.title as string) ?? node.type,
    type: node.type,
    onAction: node.props.onAction as ((...args: unknown[]) => unknown) | undefined,
    props: node.props,
  };
}

function translateDetail(node: VNode): TranslatedDetailPage {
  const metadata: Array<{ key: string; value: unknown }> = [];

  // Detail's markdown is in props.markdown
  const markdown = (node.props.markdown as string) ?? '';

  // Walk Detail.Metadata children for metadata entries
  for (const child of node.children) {
    if (!isElementVNode(child)) continue;
    if (child.type === 'Detail.Metadata') {
      for (const metaChild of child.children) {
        if (!isElementVNode(metaChild)) continue;
        if (metaChild.type === 'Detail.Metadata.Label') {
          metadata.push({
            key: (metaChild.props.title as string) ?? '',
            value: metaChild.props.text ?? metaChild.props.icon,
          });
        }
      }
    }
  }

  return {
    type: 'detail',
    markdown,
    isLoading: (node.props.isLoading as boolean) ?? false,
    metadata,
  };
}
