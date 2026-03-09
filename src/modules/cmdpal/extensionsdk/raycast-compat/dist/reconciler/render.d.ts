/**
 * Render function — the public API for rendering Raycast extension React trees.
 *
 * Usage:
 *   const { container, unmount } = render(<SearchCommand />);
 *   // container.children now holds the VNode tree
 *   // Register container.onCommit to be notified of React re-renders
 */
import type { ReactElement } from 'react';
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
export declare function render(element: ReactElement, onCommit?: () => void): RenderResult;
/**
 * Convenience: render and return the VNode tree directly (sync).
 * Best for one-shot rendering and testing.
 */
export declare function renderToVNodeTree(element: ReactElement): AnyVNode[];
//# sourceMappingURL=render.d.ts.map