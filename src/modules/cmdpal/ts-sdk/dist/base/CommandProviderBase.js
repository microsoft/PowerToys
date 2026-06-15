"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.CommandProviderBase = void 0;
/**
 * Base class for Command Palette extension providers.
 * Extend this class to create an extension that provides commands to the palette.
 *
 * @example
 * ```typescript
 * import { CommandProviderBase, ListPageBase } from '@microsoft/cmdpal-sdk';
 *
 * class MyProvider extends CommandProviderBase {
 *   id = 'my-extension';
 *   displayName = 'My Extension';
 *
 *   topLevelCommands() {
 *     return [{ command: new MyPage(), title: 'My Command', icon: null }];
 *   }
 * }
 * ```
 */
class CommandProviderBase {
    icon = null;
    frozen = false;
    settings = null;
    host;
    fallbackCommands() {
        return [];
    }
    getCommand(id) {
        return null;
    }
    initializeWithHost(host) {
        this.host = host;
    }
    dispose() {
        // Override to clean up resources
    }
}
exports.CommandProviderBase = CommandProviderBase;
//# sourceMappingURL=CommandProviderBase.js.map