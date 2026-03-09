/**
 * Custom React reconciler instance.
 *
 * This creates a reconciler using our HostConfig that captures VNode trees
 * instead of rendering to any real surface. Used by the renderer (render.ts)
 * to drive React rendering of Raycast extension components.
 */
import ReactReconciler from 'react-reconciler';
export declare const reconciler: ReactReconciler.Reconciler<unknown, unknown, unknown, unknown, unknown>;
//# sourceMappingURL=reconciler.d.ts.map