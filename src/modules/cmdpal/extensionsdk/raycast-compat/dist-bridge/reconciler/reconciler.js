"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.reconciler = void 0;
/**
 * Custom React reconciler instance.
 *
 * This creates a reconciler using our HostConfig that captures VNode trees
 * instead of rendering to any real surface. Used by the renderer (render.ts)
 * to drive React rendering of Raycast extension components.
 */
const react_reconciler_1 = __importDefault(require("react-reconciler"));
const host_config_1 = require("./host-config");
// eslint-disable-next-line @typescript-eslint/no-explicit-any
exports.reconciler = (0, react_reconciler_1.default)(host_config_1.hostConfig);
// Enable batched updates for performance
exports.reconciler.injectIntoDevTools({
    bundleType: 0, // production
    version: '0.1.0',
    rendererPackageName: '@cmdpal/raycast-compat',
});
//# sourceMappingURL=reconciler.js.map