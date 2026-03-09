"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.hostConfig = void 0;
/**
 * Strip internal React props (key, ref, children) — they are managed by React,
 * not by our host instances.
 */
function sanitizeProps(rawProps) {
    const { children, key, ref, ...rest } = rawProps;
    return rest;
}
// The @types/react-reconciler package lags behind react-reconciler 0.32.
// We type the config as Record<string, unknown> and let the runtime validate.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
exports.hostConfig = {
    // ── Feature flags ───────────────────────────────────────────────────
    supportsMutation: true,
    supportsPersistence: false,
    supportsHydration: false,
    isPrimaryRenderer: true,
    // ── Instance creation ───────────────────────────────────────────────
    createInstance(type, props) {
        return {
            type,
            props: sanitizeProps(props),
            children: [],
        };
    },
    createTextInstance(text) {
        return { type: '#text', text };
    },
    // ── Tree building (initial render) ─────────────────────────────────
    appendInitialChild(parent, child) {
        parent.children.push(child);
    },
    finalizeInitialChildren() {
        // Return false — we don't need to do host-side work after children are set
        return false;
    },
    // ── Tree mutations (updates) ───────────────────────────────────────
    appendChild(parent, child) {
        parent.children.push(child);
    },
    appendChildToContainer(container, child) {
        container.children.push(child);
    },
    removeChild(parent, child) {
        const idx = parent.children.indexOf(child);
        if (idx !== -1) {
            parent.children.splice(idx, 1);
        }
    },
    removeChildFromContainer(container, child) {
        const idx = container.children.indexOf(child);
        if (idx !== -1) {
            container.children.splice(idx, 1);
        }
    },
    insertBefore(parent, child, beforeChild) {
        const idx = parent.children.indexOf(beforeChild);
        if (idx !== -1) {
            parent.children.splice(idx, 0, child);
        }
        else {
            parent.children.push(child);
        }
    },
    insertInContainerBefore(container, child, beforeChild) {
        const idx = container.children.indexOf(beforeChild);
        if (idx !== -1) {
            container.children.splice(idx, 0, child);
        }
        else {
            container.children.push(child);
        }
    },
    // ── Prop updates ───────────────────────────────────────────────────
    prepareUpdate(_instance, _type, _oldProps, newProps) {
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
    commitUpdate(instance, _type, _oldProps, newProps) {
        instance.props = sanitizeProps(newProps);
    },
    commitTextUpdate(_textInstance, _oldText, newText) {
        _textInstance.text = newText;
    },
    // ── Container lifecycle ────────────────────────────────────────────
    prepareForCommit(_container) {
        return null;
    },
    resetAfterCommit(container) {
        // Notify the bridge that React committed a new tree snapshot.
        if (container.onCommit) {
            container.onCommit();
        }
    },
    clearContainer(container) {
        container.children.length = 0;
    },
    // ── Context ────────────────────────────────────────────────────────
    getRootHostContext() {
        return {};
    },
    getChildHostContext(parentContext) {
        return parentContext;
    },
    // ── Misc required methods ──────────────────────────────────────────
    shouldSetTextContent() {
        return false;
    },
    getPublicInstance(instance) {
        return instance;
    },
    preparePortalMount() {
        // no-op
    },
    scheduleTimeout: setTimeout,
    cancelTimeout: clearTimeout,
    noTimeout: -1,
    getCurrentEventPriority() {
        // DefaultEventPriority from react-reconciler constants
        return 0b0000000000000000000000000100000;
    },
    getInstanceFromNode() {
        return null;
    },
    beforeActiveInstanceBlur() {
        // no-op
    },
    afterActiveInstanceBlur() {
        // no-op
    },
    prepareScopeUpdate() {
        // no-op
    },
    getInstanceFromScope() {
        return null;
    },
    detachDeletedInstance() {
        // no-op
    },
    requestPostPaintCallback() {
        // no-op
    },
    maySuspendCommit() {
        return false;
    },
    preloadInstance() {
        return true;
    },
    startSuspendingCommit() {
        // no-op
    },
    suspendInstance() {
        // no-op
    },
    waitForCommitToBeReady() {
        return null;
    },
    NotPendingTransition: null,
    resetFormInstance() {
        // no-op
    },
    setCurrentUpdatePriority() {
        // no-op
    },
    getCurrentUpdatePriority() {
        return 0b0000000000000000000000000100000;
    },
    resolveUpdatePriority() {
        return 0b0000000000000000000000000100000;
    },
    shouldAttemptEagerTransition() {
        return false;
    },
    trackSchedulerEvent() {
        // no-op
    },
};
//# sourceMappingURL=host-config.js.map