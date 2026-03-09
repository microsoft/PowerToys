// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * VNode — a plain JS object representing a captured React element.
 *
 * The custom reconciler builds a tree of these instead of DOM nodes.
 * Raycast component types (List, List.Item, Detail, etc.) become VNode.type values.
 * React hooks (useState, useEffect) work normally — we only capture the output.
 */
export interface VNode {
  /** Raycast component type: "List", "List.Item", "Detail", "ActionPanel", etc. */
  type: string;
  /** Component props (title, subtitle, icon, isLoading, …) */
  props: Record<string, unknown>;
  /** Child VNodes (may include text nodes) */
  children: AnyVNode[];
}

/**
 * Text VNode — represents a raw text node in the React tree.
 */
export interface TextVNode {
  type: '#text';
  text: string;
}

export type AnyVNode = VNode | TextVNode;

/**
 * Container — the root of a reconciler tree.
 * Holds the top-level children and an optional commit callback.
 */
export interface Container {
  children: AnyVNode[];
  /** Called after each React commit phase so the bridge can react to updates. */
  onCommit?: () => void;
}

export function isTextVNode(node: AnyVNode): node is TextVNode {
  return node.type === '#text';
}

export function isElementVNode(node: AnyVNode): node is VNode {
  return node.type !== '#text';
}
