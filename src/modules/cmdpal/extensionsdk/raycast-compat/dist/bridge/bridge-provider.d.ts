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
import type { RaycastDynamicListPage, RaycastContentPage } from '../translator/translate-vnode';
import type { IconInfo } from '../translator/translate-vnode';
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
export type PageSnapshot = {
    kind: 'list';
    page: RaycastDynamicListPage;
} | {
    kind: 'content';
    page: RaycastContentPage;
} | null;
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
export declare class RaycastBridgeProvider {
    readonly id: string;
    readonly displayName: string;
    readonly icon?: IconInfo;
    private _manifest;
    private _commandModules;
    private _activeCommandId;
    private _container;
    private _unmount;
    private _snapshot;
    private _commitCount;
    private _notify;
    private _navStack;
    constructor(manifest: RaycastExtensionManifest);
    /** Register a command module (the loaded JS module with `default` export). */
    registerCommand(name: string, mod: RaycastCommandModule): void;
    /** Set the notification callback (wired to JSON-RPC transport). */
    setNotifyFn(fn: NotifyFn): void;
    /** Returns properties for the `provider/getProperties` RPC. */
    getProperties(): Record<string, unknown>;
    /**
     * Returns top-level command items for `provider/getTopLevelCommands`.
     * Each Raycast manifest command becomes a CmdPal ICommandItem.
     */
    topLevelCommands(): Array<Record<string, unknown>>;
    /**
     * Gets a command by ID. For Raycast commands, this mounts the React
     * component and returns a page descriptor.
     * Called by `provider/getCommand` RPC.
     */
    getCommand(id: string): Record<string, unknown> | undefined;
    /** Returns the latest list items for `listPage/getItems`. */
    getItems(): Array<Record<string, unknown>>;
    /** Returns latest content for `contentPage/getContent`. */
    getContent(): Array<Record<string, unknown>>;
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
    setSearchText(searchText: string): void;
    /**
     * Ensure the React command for a given pageId is mounted.
     * Called lazily by page-level RPC handlers (getItems, setSearchText, etc.)
     * since the IPage navigation path skips getCommand() and invokeCommand().
     */
    ensureMounted(pageId: string): void;
    /**
     * Invoke an action command.
     * For Raycast actions, the action's onAction callback is stored in the
     * VNode props. We look up the action by ID and call it.
     */
    invokeCommand(commandId: string): Record<string, unknown>;
    /** Clean up: unmount React tree, clear state. */
    dispose(): void;
    get snapshot(): PageSnapshot;
    get commitCount(): number;
    get activeCommandId(): string | null;
    /** Mount a Raycast command's React component in the reconciler. */
    private _mountCommand;
    /** Unmount the active React command tree. */
    private _unmountActive;
    /**
     * Called by the reconciler's resetAfterCommit via the onCommit callback.
     * This is the heart of push-to-pull: React pushed a new tree, we capture
     * the snapshot and notify CmdPal to pull fresh data.
     */
    private _onReactCommit;
    /**
     * Walk the container's VNode tree, find the root component, translate it,
     * and store as the latest snapshot.
     */
    private _processTree;
    /** Find the first element VNode in the tree (skip text nodes). */
    private _findRootVNode;
    private _commandId;
    private _commandNameFromId;
    /**
     * Convert the current snapshot into a page descriptor that CmdPal's
     * ExtensionServer can serve over JSON-RPC.
     */
    private _snapshotToPage;
    /**
     * Convert a translated RaycastListItem to the wire format that CmdPal
     * expects from getItems(), including action commands with IDs.
     */
    private _listItemToWire;
    /**
     * Invoke an action on a list item by calling the original Raycast
     * Action's onAction callback from the VNode tree.
     */
    private _invokeItemAction;
    /** Handle Action.Push — save current state and render the pushed component. */
    private _handlePushAction;
    /** Collect all List.Item VNodes from a List root (flattening sections). */
    private _collectListItems;
    /** Collect all Action VNodes from a List.Item's ActionPanel. */
    private _collectActions;
}
//# sourceMappingURL=bridge-provider.d.ts.map