// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * React Reconciler HostConfig for capturing Raycast component trees as VNodes.
 *
 * This does NOT render to any real surface (no DOM, no native views).
 * It captures the React element tree as plain JS objects (VNode) so the
 * translation layer can map them to CmdPal SDK types.
 *
 * Reference: https://github.com/facebook/react/tree/main/packages/react-reconciler
 */

import type { VNode, TextVNode, Container, AnyVNode } from './vnode';

type Type = string;
type Props = Record<string, unknown>;
type Instance = VNode;
type TextInstance = TextVNode;
type PublicInstance = VNode | TextVNode;
type HostContext = Record<string, never>;
type UpdatePayload = Props;
type NoTimeout = -1;

/**
 * Strip internal React props (key, ref, children) — they are managed by React,
 * not by our host instances.
 */
function sanitizeProps(rawProps: Props): Props {
  const { children, key, ref, ...rest } = rawProps as Props & {
    children?: unknown;
    key?: unknown;
    ref?: unknown;
  };
  return rest;
}

// The @types/react-reconciler package lags behind react-reconciler 0.32.
// We type the config as Record<string, unknown> and let the runtime validate.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const hostConfig: Record<string, any> = {
  // ── Feature flags ───────────────────────────────────────────────────
  supportsMutation: true,
  supportsPersistence: false,
  supportsHydration: false,
  isPrimaryRenderer: true,

  // ── Instance creation ───────────────────────────────────────────────

  createInstance(type: Type, props: Props): Instance {
    return {
      type,
      props: sanitizeProps(props),
      children: [],
    };
  },

  createTextInstance(text: string): TextInstance {
    return { type: '#text', text };
  },

  // ── Tree building (initial render) ─────────────────────────────────

  appendInitialChild(parent: Instance, child: Instance | TextInstance): void {
    parent.children.push(child);
  },

  finalizeInitialChildren(): boolean {
    // Return false — we don't need to do host-side work after children are set
    return false;
  },

  // ── Tree mutations (updates) ───────────────────────────────────────

  appendChild(parent: Instance, child: Instance | TextInstance): void {
    parent.children.push(child);
  },

  appendChildToContainer(container: Container, child: Instance | TextInstance): void {
    container.children.push(child);
  },

  removeChild(parent: Instance, child: Instance | TextInstance): void {
    const idx = parent.children.indexOf(child);
    if (idx !== -1) {
      parent.children.splice(idx, 1);
    }
  },

  removeChildFromContainer(container: Container, child: Instance | TextInstance): void {
    const idx = container.children.indexOf(child);
    if (idx !== -1) {
      container.children.splice(idx, 1);
    }
  },

  insertBefore(
    parent: Instance,
    child: Instance | TextInstance,
    beforeChild: Instance | TextInstance,
  ): void {
    const idx = parent.children.indexOf(beforeChild);
    if (idx !== -1) {
      parent.children.splice(idx, 0, child);
    } else {
      parent.children.push(child);
    }
  },

  insertInContainerBefore(
    container: Container,
    child: Instance | TextInstance,
    beforeChild: Instance | TextInstance,
  ): void {
    const idx = container.children.indexOf(beforeChild);
    if (idx !== -1) {
      container.children.splice(idx, 0, child);
    } else {
      container.children.push(child);
    }
  },

  // ── Prop updates ───────────────────────────────────────────────────

  prepareUpdate(
    _instance: Instance,
    _type: Type,
    _oldProps: Props,
    newProps: Props,
  ): UpdatePayload | null {
    // Always return the new props — let commitUpdate apply them.
    // A production version could diff here for efficiency.
    // Note: react-reconciler 0.32+ may not pass the return value to
    // commitUpdate anymore (API change in React 19). See commitUpdate below.
    return sanitizeProps(newProps);
  },

  // react-reconciler 0.32 (React 19) changed the commitUpdate signature:
  //   Old (≤0.31): commitUpdate(instance, updatePayload, type, oldProps, newProps)
  //   New (0.32+):  commitUpdate(instance, type, oldProps, newProps, internalHandle)
  // We accept the new signature and diff newProps directly.
  commitUpdate(
    instance: Instance,
    _type: Type,
    _oldProps: Props,
    newProps: Props,
  ): void {
    instance.props = sanitizeProps(newProps);
  },

  commitTextUpdate(_textInstance: TextInstance, _oldText: string, newText: string): void {
    _textInstance.text = newText;
  },

  // ── Container lifecycle ────────────────────────────────────────────

  prepareForCommit(_container: Container): Record<string, unknown> | null {
    return null;
  },

  resetAfterCommit(container: Container): void {
    // Notify the bridge that React committed a new tree snapshot.
    if (container.onCommit) {
      container.onCommit();
    }
  },

  clearContainer(container: Container): void {
    container.children.length = 0;
  },

  // ── Context ────────────────────────────────────────────────────────

  getRootHostContext(): HostContext {
    return {};
  },

  getChildHostContext(parentContext: HostContext): HostContext {
    return parentContext;
  },

  // ── Misc required methods ──────────────────────────────────────────

  shouldSetTextContent(): boolean {
    return false;
  },

  getPublicInstance(instance: Instance | TextInstance): PublicInstance {
    return instance;
  },

  preparePortalMount(): void {
    // no-op
  },

  scheduleTimeout: setTimeout,
  cancelTimeout: clearTimeout,
  noTimeout: -1 as NoTimeout,

  getCurrentEventPriority(): number {
    // DefaultEventPriority from react-reconciler constants
    return 0b0000000000000000000000000100000;
  },

  getInstanceFromNode(): null {
    return null;
  },

  beforeActiveInstanceBlur(): void {
    // no-op
  },

  afterActiveInstanceBlur(): void {
    // no-op
  },

  prepareScopeUpdate(): void {
    // no-op
  },

  getInstanceFromScope(): null {
    return null;
  },

  detachDeletedInstance(): void {
    // no-op
  },

  requestPostPaintCallback(): void {
    // no-op
  },

  maySuspendCommit(): boolean {
    return false;
  },

  preloadInstance(): boolean {
    return true;
  },

  startSuspendingCommit(): void {
    // no-op
  },

  suspendInstance(): void {
    // no-op
  },

  waitForCommitToBeReady(): null {
    return null;
  },

  NotPendingTransition: null as unknown,

  resetFormInstance(): void {
    // no-op
  },

  setCurrentUpdatePriority(): void {
    // no-op
  },

  getCurrentUpdatePriority(): number {
    return 0b0000000000000000000000000100000;
  },

  resolveUpdatePriority(): number {
    return 0b0000000000000000000000000100000;
  },

  shouldAttemptEagerTransition(): boolean {
    return false;
  },

  trackSchedulerEvent(): void {
    // no-op
  },
};
