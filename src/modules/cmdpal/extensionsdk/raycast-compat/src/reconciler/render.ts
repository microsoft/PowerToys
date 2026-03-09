// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Render function — the public API for rendering Raycast extension React trees.
 *
 * Usage:
 *   const { container, unmount } = render(<SearchCommand />);
 *   // container.children now holds the VNode tree
 *   // Register container.onCommit to be notified of React re-renders
 */

import type { ReactElement } from 'react';
import { reconciler } from './reconciler';
import type { Container, AnyVNode } from './vnode';

export interface RenderResult {
  /** The root container holding the captured VNode tree. */
  container: Container;
  /** Unmount the React tree and clean up. */
  unmount: () => void;
  /**
   * Wait for the initial render commit.
   * Resolves once React has committed the first tree.
   */
  waitForCommit: () => Promise<Container>;
}

/**
 * Render a React element using the custom reconciler.
 * Returns the container with the captured VNode tree.
 *
 * @param element - The React element to render (e.g. <List>...</List>)
 * @param onCommit - Optional callback invoked after each React commit
 */
export function render(
  element: ReactElement,
  onCommit?: () => void,
): RenderResult {
  const container: Container = {
    children: [],
    onCommit,
  };

  // LegacyRoot = 0 ensures synchronous rendering so the tree is
  // immediately available after updateContainer. ConcurrentRoot defers
  // work and our tests need the tree populated synchronously.
  const fiberRoot = reconciler.createContainer(
    container,
    0, // LegacyRoot — synchronous
    null, // hydrationCallbacks
    false, // isStrictMode
    null, // concurrentUpdatesByDefaultOverride
    '', // identifierPrefix
    (err: Error) => console.error('[raycast-compat reconciler]', err),
    null, // transitionCallbacks
  );

  // updateContainerSync ensures the render completes before returning.
  // Falls back to updateContainer + flushSyncWork if unavailable.
  const rec = reconciler as any;
  if (typeof rec.updateContainerSync === 'function') {
    rec.updateContainerSync(element, fiberRoot, null, undefined);
  } else {
    reconciler.updateContainer(element, fiberRoot, null, undefined);
  }
  if (typeof rec.flushSyncWork === 'function') {
    rec.flushSyncWork();
  }

  const unmount = () => {
    reconciler.updateContainer(null, fiberRoot, null, undefined);
  };

  const waitForCommit = (): Promise<Container> => {
    // If children already exist, the commit has happened
    if (container.children.length > 0) {
      return Promise.resolve(container);
    }
    return new Promise<Container>((resolve) => {
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
export function renderToVNodeTree(element: ReactElement): AnyVNode[] {
  const { container, unmount } = render(element);
  const tree = [...container.children];
  unmount();
  return tree;
}
