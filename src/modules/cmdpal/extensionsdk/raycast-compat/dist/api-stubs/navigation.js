"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.open = open;
exports.closeMainWindow = closeMainWindow;
exports.popToRoot = popToRoot;
exports.launchCommand = launchCommand;
exports.confirmAlert = confirmAlert;
/**
 * Raycast navigation compatibility stubs.
 *
 * These cover the imperative navigation functions that Raycast extensions
 * import from `@raycast/api`. Most are no-ops or console stubs for now —
 * CmdPal's navigation model is declarative (via CommandResult), and the
 * compat runtime will need to bridge imperative calls to declarative results.
 */
const toast_1 = require("./toast");
/**
 * Open a URL in the default browser.
 */
async function open(target, application) {
    void application;
    try {
        const { execSync } = await Promise.resolve().then(() => __importStar(require('child_process')));
        execSync(`start "" "${target}"`, { stdio: 'ignore', shell: 'cmd.exe' });
        console.log(`[Navigation] Opened: ${target}`);
    }
    catch {
        console.warn(`[Navigation] Failed to open: ${target}`);
    }
}
/**
 * Close the main Raycast window.
 * In CmdPal this maps to dismissing the palette.
 */
async function closeMainWindow(options) {
    void options;
    console.log('[Navigation] closeMainWindow() — CmdPal will dismiss via CommandResult');
}
/**
 * Pop the current view from the navigation stack.
 * In CmdPal this maps to CommandResult.goBack().
 */
async function popToRoot(options) {
    void options;
    console.log('[Navigation] popToRoot() — CmdPal will navigate via CommandResult');
}
/**
 * Launch another Raycast command by name.
 * Stub: not supported in CmdPal spike.
 */
async function launchCommand(options) {
    console.warn(`[Navigation] launchCommand("${options.name}") is not yet supported in CmdPal`);
    await (0, toast_1.showToast)({
        style: toast_1.ToastStyle.Failure,
        title: 'Not Supported',
        message: `launchCommand("${options.name}") is not available in CmdPal`,
    });
}
/**
 * Confirm an action with a dialog.
 * For the spike: always resolves true (auto-confirms).
 */
async function confirmAlert(options) {
    console.log(`[Alert] ${options.title}: ${options.message ?? '(no message)'} — auto-confirming`);
    return true;
}
//# sourceMappingURL=navigation.js.map