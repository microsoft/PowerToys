// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Custom React reconciler instance.
 *
 * This creates a reconciler using our HostConfig that captures VNode trees
 * instead of rendering to any real surface. Used by the renderer (render.ts)
 * to drive React rendering of Raycast extension components.
 */

import ReactReconciler from 'react-reconciler';
import { hostConfig } from './host-config';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export const reconciler = ReactReconciler(hostConfig as any);

// Enable batched updates for performance
reconciler.injectIntoDevTools({
  bundleType: 0, // production
  version: '0.1.0',
  rendererPackageName: '@cmdpal/raycast-compat',
});
