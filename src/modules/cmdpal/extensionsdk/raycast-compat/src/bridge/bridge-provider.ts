// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * RaycastBridgeProvider — the main bridge between Raycast extensions and CmdPal.
 *
 * Lifecycle:
 *   1. Receives a Raycast command module (the default export React component)
 *   2. Renders it through the custom reconciler → VNode tree
 *   3. Translates VNodes → CmdPal SDK types via the translator
 *   4. Stores the latest snapshot for the pull model
 *   5. On React re-render (onCommit), re-translates and notifies CmdPal
 *
 * Push-to-pull conversion:
 *   Raycast extensions PUSH UI updates via React re-renders (setState, hooks).
 *   CmdPal PULLS via JSON-RPC: getItems(), getContent(), etc.
 *   The bridge detects commits, stores the latest translated snapshot, and
 *   emits itemsChanged / propChanged notifications so CmdPal re-fetches.
 */

import React from 'react';

import { render } from '../reconciler/render';
import { reconciler } from '../reconciler/reconciler';
import type { Container, VNode, AnyVNode } from '../reconciler/vnode';
import { isElementVNode } from '../reconciler/vnode';
import { translateVNode } from '../translator/translate-vnode';
import type { RaycastDynamicListPage, RaycastContentPage, RaycastListItem } from '../translator/translate-vnode';
import type { ContextItem, IconInfo } from '../translator/translate-vnode';

// ══════════════════════════════════════════════════════════════════════════
// Types
// ══════════════════════════════════════════════════════════════════════════

/** Shape of a Raycast extension's package.json command entry. */
export interface RaycastCommandManifest {
  name: string;
  title: string;
  subtitle?: string;
  description?: string;
  icon?: string;
  mode?: 'view' | 'no-view' | 'menu-bar';
}

/** Shape of a Raycast extension's package.json. */
export interface RaycastExtensionManifest {
  name: string;
  title: string;
  description?: string;
  icon?: string;
  commands: RaycastCommandManifest[];
}

/** A Raycast command module — the default export is a React component. */
export interface RaycastCommandModule {
  default: React.ComponentType<Record<string, unknown>>;
}

/**
 * Notification callback — the bridge provider calls this to tell CmdPal
 * that data has changed. Wired to JsonRpcTransport.sendNotification by
 * the entry point.
 */
export type NotifyFn = (method: string, params?: Record<string, unknown>) => void;

/** Snapshot of a translated page, tagged with its kind. */
export type PageSnapshot =
  | { kind: 'list'; page: RaycastDynamicListPage }
  | { kind: 'content'; page: RaycastContentPage }
  | null;

// ══════════════════════════════════════════════════════════════════════════
// Bridge Provider
// ══════════════════════════════════════════════════════════════════════════

/**
 * RaycastBridgeProvider wraps a single Raycast command as a CmdPal extension.
 *
 * It implements the "provider" side of the CmdPal JSON-RPC protocol:
 *   - topLevelCommands() → the extension's commands from its manifest
 *   - getCommand(id)     → a DynamicListPage or ContentPage for the command
 *   - getItems()         → latest translated list items
 *   - getContent()       → latest translated content
 *   - setSearchText()    → forwards to Raycast's onSearchTextChange
 *
 * The class does NOT extend CmdPal's abstract CommandProvider/DynamicListPage
 * directly — those classes have transport dependencies and protected members.
 * Instead, it produces plain objects with the same shapes that ExtensionServer
 * serializes over JSON-RPC.
 */
export class RaycastBridgeProvider {
  readonly id: string;
  readonly displayName: string;
  readonly icon?: IconInfo;

  private _manifest: RaycastExtensionManifest;
  private _commandModules: Map<string, RaycastCommandModule> = new Map();

  // Active render state (per mounted command)
  private _activeCommandId: string | null = null;
  private _container: Container | null = null;
  private _unmount: (() => void) | null = null;
  private _snapshot: PageSnapshot = null;
  private _commitCount = 0;

  // Notification hook — wired by the entry point to the JSON-RPC transport
  private _notify: NotifyFn = () => {};

  // Navigation stack for push transitions (List → Detail)
  private _navStack: Array<{
    commandId: string;
    snapshot: PageSnapshot;
  }> = [];

  constructor(manifest: RaycastExtensionManifest) {
    this._manifest = manifest;
    this.id = `raycast-compat.${manifest.name}`;
    this.displayName = manifest.title;

    if (manifest.icon) {
      this.icon = {
        light: { icon: manifest.icon },
        dark: { icon: manifest.icon },
      };
    }
  }

  // ── Configuration ────────────────────────────────────────────────────

  /** Register a command module (the loaded JS module with `default` export). */
  registerCommand(name: string, mod: RaycastCommandModule): void {
    this._commandModules.set(name, mod);
  }

  /** Set the notification callback (wired to JSON-RPC transport). */
  setNotifyFn(fn: NotifyFn): void {
    this._notify = fn;
  }

  // ── Provider protocol (called by ExtensionServer handlers) ──────────

  /** Returns properties for the `provider/getProperties` RPC. */
  getProperties(): Record<string, unknown> {
    return {
      id: this.id,
      displayName: this.displayName,
      icon: this.icon,
      frozen: true,
    };
  }

  /**
   * Returns top-level command items for `provider/getTopLevelCommands`.
   * Each Raycast manifest command becomes a CmdPal ICommandItem.
   */
  topLevelCommands(): Array<Record<string, unknown>> {
    return this._manifest.commands.map((cmd) => {
      const commandId = this._commandId(cmd.name);
      const icon: IconInfo | undefined = cmd.icon
        ? { light: { icon: cmd.icon }, dark: { icon: cmd.icon } }
        : this.icon;

      return {
        title: cmd.title,
        subtitle: cmd.subtitle ?? this._manifest.title,
        icon,
        command: {
          id: commandId,
          name: cmd.title,
          icon,
        },
      };
    });
  }

  /**
   * Gets a command by ID. For Raycast commands, this mounts the React
   * component and returns a page descriptor.
   * Called by `provider/getCommand` RPC.
   */
  getCommand(id: string): Record<string, unknown> | undefined {
    const cmdName = this._commandNameFromId(id);
    if (!cmdName) return undefined;

    const manifestCmd = this._manifest.commands.find((c) => c.name === cmdName);
    if (!manifestCmd) return undefined;

    // Mount the React command if not already active
    if (this._activeCommandId !== id) {
      this._mountCommand(cmdName, id);
    }

    return this._snapshotToPage(id, manifestCmd);
  }

  // ── Page protocol (called by list/content RPC handlers) ─────────────

  /** Returns the latest list items for `listPage/getItems`. */
  getItems(): Array<Record<string, unknown>> {
    if (this._snapshot?.kind !== 'list') return [];

    const items = this._snapshot.page.getItems();
    return items.map((item, idx) => this._listItemToWire(item, idx));
  }

  /** Returns latest content for `contentPage/getContent`. */
  getContent(): Array<Record<string, unknown>> {
    if (this._snapshot?.kind !== 'content') return [];
    return this._snapshot.page.getContent().map((c) => ({
      type: c.type ?? 'markdown',
      id: c.id || `content-${Date.now()}`,
      body: c.body,
    }));
  }

  /**
   * Forwards search text to the Raycast extension's onSearchTextChange.
   * Called by `listPage/setSearchText` RPC.
   *
   * We read the callback from the live VNode tree (not the translated
   * snapshot) because the snapshot's callback is a captured reference
   * from translation time. The VNode tree always holds the latest
   * closure from React's most recent render.
   *
   * After calling the callback, we flush React's work queue to ensure
   * the state update and re-render happen synchronously.
   */
  setSearchText(searchText: string): void {
    if (!this._container) return;

    const root = this._findRootVNode(this._container.children);
    if (!root || root.type !== 'List') return;

    const onSearchTextChange = root.props.onSearchTextChange as
      | ((text: string) => void)
      | undefined;

    if (typeof onSearchTextChange === 'function') {
      onSearchTextChange(searchText);

      // Flush React's update queue so the re-render happens synchronously.
      // Without this, the useState update is batched and deferred.
      const rec = reconciler as unknown as Record<string, unknown>;
      if (typeof rec.flushSyncWork === 'function') {
        (rec.flushSyncWork as () => void)();
      } else if (typeof rec.flushSync === 'function') {
        (rec.flushSync as (fn?: () => void) => void)();
      }
    }
  }

  /**
   * Invoke an action command.
   * For Raycast actions, the action's onAction callback is stored in the
   * VNode props. We look up the action by ID and call it.
   */
  invokeCommand(commandId: string): Record<string, unknown> {
    // Check if this is a list item action (format: {pageId}::action::{index}::{actionIndex})
    const actionMatch = commandId.match(/^(.+)::action::(\d+)::(\d+)$/);
    if (actionMatch) {
      const [, , itemIdxStr, actionIdxStr] = actionMatch;
      const itemIdx = parseInt(itemIdxStr, 10);
      const actionIdx = parseInt(actionIdxStr, 10);
      this._invokeItemAction(itemIdx, actionIdx);
      return { kind: 4 }; // KeepOpen
    }

    // Check if this is a navigation push from an Action.Push
    const pushMatch = commandId.match(/^(.+)::push::(\d+)::(\d+)$/);
    if (pushMatch) {
      const [, , itemIdxStr, actionIdxStr] = pushMatch;
      const itemIdx = parseInt(itemIdxStr, 10);
      const actionIdx = parseInt(actionIdxStr, 10);
      return this._handlePushAction(itemIdx, actionIdx);
    }

    // Default: it's a top-level command, mount it
    return { kind: 4 }; // KeepOpen
  }

  /** Clean up: unmount React tree, clear state. */
  dispose(): void {
    this._unmountActive();
    this._commandModules.clear();
    this._navStack = [];
  }

  // ── Snapshot accessors (for testing) ────────────────────────────────

  get snapshot(): PageSnapshot {
    return this._snapshot;
  }

  get commitCount(): number {
    return this._commitCount;
  }

  get activeCommandId(): string | null {
    return this._activeCommandId;
  }

  // ══════════════════════════════════════════════════════════════════════
  // Internal: React lifecycle
  // ══════════════════════════════════════════════════════════════════════

  /** Mount a Raycast command's React component in the reconciler. */
  private _mountCommand(cmdName: string, commandId: string): void {
    const mod = this._commandModules.get(cmdName);
    if (!mod?.default) {
      console.warn(`[Bridge] No module registered for command: ${cmdName}`);
      return;
    }

    // Unmount previous command if any
    this._unmountActive();
    this._activeCommandId = commandId;

    const CommandComponent = mod.default;
    const element = React.createElement(CommandComponent, {});

    const { container, unmount } = render(element, () => {
      this._onReactCommit();
    });

    this._container = container;
    this._unmount = unmount;

    // Process the initial tree (render is synchronous with LegacyRoot)
    this._processTree();
  }

  /** Unmount the active React command tree. */
  private _unmountActive(): void {
    if (this._unmount) {
      this._unmount();
      this._unmount = null;
    }
    this._container = null;
    this._snapshot = null;
    this._activeCommandId = null;
  }

  /**
   * Called by the reconciler's resetAfterCommit via the onCommit callback.
   * This is the heart of push-to-pull: React pushed a new tree, we capture
   * the snapshot and notify CmdPal to pull fresh data.
   */
  private _onReactCommit(): void {
    this._commitCount++;
    const prevKind = this._snapshot?.kind ?? null;

    this._processTree();

    // Determine which notifications to send
    if (this._snapshot?.kind === 'list' && this._activeCommandId) {
      this._notify('listPage/itemsChanged', {
        pageId: this._activeCommandId,
        totalItems: this._snapshot.page.getItems().length,
      });
    } else if (this._snapshot?.kind === 'content' && this._activeCommandId) {
      this._notify('page/propChanged', {
        pageId: this._activeCommandId,
        propertyName: 'content',
      });
    }

    // If the page kind changed (e.g., from loading→list), also notify prop change
    if (prevKind !== null && prevKind !== this._snapshot?.kind && this._activeCommandId) {
      this._notify('page/propChanged', {
        pageId: this._activeCommandId,
        propertyName: '_type',
      });
    }
  }

  /**
   * Walk the container's VNode tree, find the root component, translate it,
   * and store as the latest snapshot.
   */
  private _processTree(): void {
    if (!this._container) return;

    const root = this._findRootVNode(this._container.children);
    if (!root) {
      this._snapshot = null;
      return;
    }

    const translated = translateVNode(root);
    if (!translated) {
      this._snapshot = null;
      return;
    }

    if (translated._type === 'dynamicListPage') {
      this._snapshot = { kind: 'list', page: translated as RaycastDynamicListPage };
    } else if (translated._type === 'contentPage') {
      this._snapshot = { kind: 'content', page: translated as RaycastContentPage };
    } else {
      this._snapshot = null;
    }
  }

  /** Find the first element VNode in the tree (skip text nodes). */
  private _findRootVNode(children: AnyVNode[]): VNode | null {
    for (const child of children) {
      if (isElementVNode(child)) {
        return child;
      }
    }
    return null;
  }

  // ══════════════════════════════════════════════════════════════════════
  // Internal: ID helpers
  // ══════════════════════════════════════════════════════════════════════

  private _commandId(cmdName: string): string {
    return `${this.id}.${cmdName}`;
  }

  private _commandNameFromId(id: string): string | null {
    const prefix = `${this.id}.`;
    if (!id.startsWith(prefix)) return null;
    return id.slice(prefix.length);
  }

  // ══════════════════════════════════════════════════════════════════════
  // Internal: Snapshot → JSON-RPC wire format
  // ══════════════════════════════════════════════════════════════════════

  /**
   * Convert the current snapshot into a page descriptor that CmdPal's
   * ExtensionServer can serve over JSON-RPC.
   */
  private _snapshotToPage(
    commandId: string,
    manifestCmd: RaycastCommandManifest,
  ): Record<string, unknown> {
    if (this._snapshot?.kind === 'list') {
      const page = this._snapshot.page;
      return {
        _type: 'dynamicListPage',
        id: commandId,
        name: page.name || manifestCmd.title,
        icon: this.icon,
        placeholderText: page.placeholderText || `Search ${manifestCmd.title}…`,
        searchText: page.searchText,
        showDetails: page.showDetails,
        hasMoreItems: page.hasMoreItems,
      };
    }

    if (this._snapshot?.kind === 'content') {
      const page = this._snapshot.page;
      return {
        _type: 'contentPage',
        id: commandId,
        name: page.name || manifestCmd.title,
        icon: this.icon,
        details: page.details,
        commands: page.commands,
      };
    }

    // No snapshot yet (still loading) — return a loading list page
    return {
      _type: 'dynamicListPage',
      id: commandId,
      name: manifestCmd.title,
      icon: this.icon,
      placeholderText: `Loading ${manifestCmd.title}…`,
      searchText: '',
      showDetails: false,
      hasMoreItems: false,
    };
  }

  /**
   * Convert a translated RaycastListItem to the wire format that CmdPal
   * expects from getItems(), including action commands with IDs.
   */
  private _listItemToWire(
    item: RaycastListItem,
    itemIndex: number,
  ): Record<string, unknown> {
    const pageId = this._activeCommandId ?? '';

    // Convert moreCommands (Raycast Actions) to CmdPal IContextItems with IDs
    const moreCommands = (item.moreCommands ?? []).map(
      (action: ContextItem, actionIdx: number) => ({
        title: action.title,
        subtitle: action.subtitle,
        icon: action.icon,
        isCritical: action.isCritical,
        command: {
          id: `${pageId}::action::${itemIndex}::${actionIdx}`,
          name: action.title ?? 'Action',
          icon: action.icon,
        },
      }),
    );

    // Primary command: invoke the first action (or navigate to detail)
    const primaryCommandId =
      moreCommands.length > 0
        ? (moreCommands[0].command as Record<string, unknown>).id
        : `${pageId}::action::${itemIndex}::0`;

    return {
      title: item.title,
      subtitle: item.subtitle,
      icon: item.icon,
      tags: item.tags,
      details: item.details,
      section: item.section,
      textToSuggest: item.textToSuggest,
      moreCommands,
      command: {
        id: primaryCommandId,
        name: item.title,
        icon: item.icon,
      },
    };
  }

  // ══════════════════════════════════════════════════════════════════════
  // Internal: Action execution
  // ══════════════════════════════════════════════════════════════════════

  /**
   * Invoke an action on a list item by calling the original Raycast
   * Action's onAction callback from the VNode tree.
   */
  private _invokeItemAction(itemIndex: number, actionIndex: number): void {
    if (!this._container) return;

    const root = this._findRootVNode(this._container.children);
    if (!root || root.type !== 'List') return;

    // Collect all List.Item VNodes (including inside sections)
    const itemVNodes = this._collectListItems(root);
    const itemVNode = itemVNodes[itemIndex];
    if (!itemVNode) return;

    // Find the action at actionIndex
    const actions = this._collectActions(itemVNode);
    const action = actions[actionIndex];
    if (!action) return;

    // Call the onAction callback if present
    const onAction = action.props.onAction as ((...args: unknown[]) => void) | undefined;
    if (typeof onAction === 'function') {
      try {
        onAction();
      } catch (err) {
        console.error('[Bridge] Action error:', err);
      }
    }

    // Special handling for Action.Open and Action.OpenInBrowser
    if (action.type === 'Action.Open' || action.type === 'Action.OpenInBrowser') {
      const target = action.props.target as string | undefined;
      if (target) {
        import('../api-stubs/navigation').then((nav) => nav.open(target)).catch(() => {});
      }
    }

    // Special handling for Action.CopyToClipboard
    if (action.type === 'Action.CopyToClipboard') {
      const content = action.props.content as string | undefined;
      if (content) {
        import('../api-stubs/clipboard').then((clip) =>
          clip.Clipboard.copy(content),
        ).catch(() => {});
      }
    }
  }

  /** Handle Action.Push — save current state and render the pushed component. */
  private _handlePushAction(
    itemIndex: number,
    actionIndex: number,
  ): Record<string, unknown> {
    if (!this._container) return { kind: 4 }; // KeepOpen

    const root = this._findRootVNode(this._container.children);
    if (!root) return { kind: 4 };

    const itemVNodes = this._collectListItems(root);
    const itemVNode = itemVNodes[itemIndex];
    if (!itemVNode) return { kind: 4 };

    const actions = this._collectActions(itemVNode);
    const action = actions[actionIndex];
    if (!action || action.type !== 'Action.Push') return { kind: 4 };

    const target = action.props.target as React.ReactElement | undefined;
    if (!target) return { kind: 4 };

    // Push current state to nav stack
    if (this._activeCommandId) {
      this._navStack.push({
        commandId: this._activeCommandId,
        snapshot: this._snapshot,
      });
    }

    // TODO: Render the pushed component in a new container
    // For now, return KeepOpen — full navigation will land with the nav bridge
    return { kind: 4 };
  }

  /** Collect all List.Item VNodes from a List root (flattening sections). */
  private _collectListItems(listNode: VNode): VNode[] {
    const items: VNode[] = [];
    for (const child of listNode.children) {
      if (!isElementVNode(child)) continue;
      if (child.type === 'List.Item') {
        items.push(child);
      } else if (child.type === 'List.Section') {
        for (const sectionChild of child.children) {
          if (isElementVNode(sectionChild) && sectionChild.type === 'List.Item') {
            items.push(sectionChild);
          }
        }
      }
    }
    return items;
  }

  /** Collect all Action VNodes from a List.Item's ActionPanel. */
  private _collectActions(itemNode: VNode): VNode[] {
    const actions: VNode[] = [];
    for (const child of itemNode.children) {
      if (!isElementVNode(child) || child.type !== 'ActionPanel') continue;
      for (const actionChild of child.children) {
        if (!isElementVNode(actionChild)) continue;
        if (actionChild.type === 'ActionPanel.Section') {
          for (const sectionChild of actionChild.children) {
            if (isElementVNode(sectionChild)) {
              actions.push(sectionChild);
            }
          }
        } else {
          actions.push(actionChild);
        }
      }
    }
    return actions;
  }
}
