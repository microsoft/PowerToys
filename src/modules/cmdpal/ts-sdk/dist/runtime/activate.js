"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
/**
 * Helper function for extension activation.
 * Wraps a provider factory with automatic ExtensionHost initialization.
 *
 * @example
 * ```typescript
 * // In your extension's index.ts:
 * import { activate as sdkActivate, CommandProviderBase } from '@microsoft/cmdpal-sdk';
 *
 * class MyProvider extends CommandProviderBase {
 *   id = 'my-ext';
 *   displayName = 'My Extension';
 *   topLevelCommands() { return []; }
 * }
 *
 * export function activate(context: ActivationContext) {
 *   return sdkActivate(context, () => new MyProvider());
 * }
 * ```
 */
function activate(context, providerFactory) {
    // The host will be initialized by the Node host process when it calls
    // initializeWithHost on the provider. This is a convenience wrapper.
    return providerFactory();
}
//# sourceMappingURL=activate.js.map