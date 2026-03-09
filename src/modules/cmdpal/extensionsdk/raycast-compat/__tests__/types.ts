// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Shared type definitions for reconciler and translator test specs.
 *
 * These mirror what Ash's reconciler will produce. Once the implementation
 * lands, these can be replaced by re-exports from the actual module.
 */

/** Virtual node produced by the React reconciler. */
export interface VNode {
  type: string;
  props: Record<string, unknown>;
  children: VNode[];
}

/**
 * Minimal contract the reconciler must expose.
 * The real implementation wraps react-reconciler's HostConfig.
 */
export interface ReconcilerRenderer {
  /** Render a React element tree and return the root VNode container. */
  render(element: React.ReactElement): VNode;

  /** Unmount the currently rendered tree. */
  unmount(): void;
}

/**
 * Translator function signature: converts a VNode tree into CmdPal SDK objects.
 * Returns the top-level SDK object (e.g., DynamicListPage, ContentPage).
 */
export type TranslateVNode = (vnode: VNode) => unknown;
