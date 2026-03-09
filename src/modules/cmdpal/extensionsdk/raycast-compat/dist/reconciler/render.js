"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.render = render;
exports.renderToVNodeTree = renderToVNodeTree;
const reconciler_1 = require("./reconciler");
/**
 * Render a React element using the custom reconciler.
 * Returns the container with the captured VNode tree.
 *
 * @param element - The React element to render (e.g. <List>...</List>)
 * @param onCommit - Optional callback invoked after each React commit
 */
function render(element, onCommit) {
    const container = {
        children: [],
        onCommit,
    };
    // LegacyRoot = 0 ensures synchronous rendering so the tree is
    // immediately available after updateContainer. ConcurrentRoot defers
    // work and our tests need the tree populated synchronously.
    const fiberRoot = reconciler_1.reconciler.createContainer(container, 0, // LegacyRoot — synchronous
    null, // hydrationCallbacks
    false, // isStrictMode
    null, // concurrentUpdatesByDefaultOverride
    '', // identifierPrefix
    (err) => console.error('[raycast-compat reconciler]', err), null);
    // updateContainerSync ensures the render completes before returning.
    // Falls back to updateContainer + flushSyncWork if unavailable.
    const rec = reconciler_1.reconciler;
    if (typeof rec.updateContainerSync === 'function') {
        rec.updateContainerSync(element, fiberRoot, null, undefined);
    }
    else {
        reconciler_1.reconciler.updateContainer(element, fiberRoot, null, undefined);
    }
    if (typeof rec.flushSyncWork === 'function') {
        rec.flushSyncWork();
    }
    const unmount = () => {
        reconciler_1.reconciler.updateContainer(null, fiberRoot, null, undefined);
    };
    const waitForCommit = () => {
        // If children already exist, the commit has happened
        if (container.children.length > 0) {
            return Promise.resolve(container);
        }
        return new Promise((resolve) => {
            const originalOnCommit = container.onCommit;
            container.onCommit = () => {
                originalOnCommit?.();
                container.onCommit = originalOnCommit;
                resolve(container);
            };
        });
    };
    return { container, unmount, waitForCommit };
}
/**
 * Convenience: render and return the VNode tree directly (sync).
 * Best for one-shot rendering and testing.
 */
function renderToVNodeTree(element) {
    const { container, unmount } = render(element);
    const tree = [...container.children];
    unmount();
    return tree;
}
//# sourceMappingURL=render.js.map