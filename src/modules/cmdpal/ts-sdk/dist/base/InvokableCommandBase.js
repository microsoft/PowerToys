"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.InvokableCommandBase = void 0;
/**
 * Base class for commands that can be invoked (executed) by the user.
 *
 * @example
 * ```typescript
 * import { InvokableCommandBase } from '@microsoft/cmdpal-sdk';
 *
 * class CopyCommand extends InvokableCommandBase {
 *   id = 'copy-text';
 *   name = 'Copy to Clipboard';
 *
 *   async invoke() {
 *     // Perform the action
 *     return { kind: 'showToast', args: { message: 'Copied!' } };
 *   }
 * }
 * ```
 */
class InvokableCommandBase {
    icon = null;
}
exports.InvokableCommandBase = InvokableCommandBase;
//# sourceMappingURL=InvokableCommandBase.js.map