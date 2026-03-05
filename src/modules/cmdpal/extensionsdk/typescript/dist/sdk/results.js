"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.CommandResult = void 0;
const types_1 = require("../generated/types");
/**
 * Helper class for creating command results.
 */
class CommandResult {
    constructor(kind, args) {
        this.kind = kind;
        this.args = args;
    }
    /** Dismiss the command palette. */
    static dismiss() {
        return new CommandResult(types_1.CommandResultKind.Dismiss);
    }
    /** Navigate to the home page. */
    static goHome() {
        return new CommandResult(types_1.CommandResultKind.GoHome);
    }
    /** Navigate back to the previous page. */
    static goBack() {
        return new CommandResult(types_1.CommandResultKind.GoBack);
    }
    /** Hide the command palette without dismissing. */
    static hide() {
        return new CommandResult(types_1.CommandResultKind.Hide);
    }
    /** Keep the command palette open. */
    static keepOpen() {
        return new CommandResult(types_1.CommandResultKind.KeepOpen);
    }
    /**
     * Navigate to a specific page.
     * @param pageId The ID of the page to navigate to
     * @param mode The navigation mode (default: Push)
     */
    static goToPage(pageId, mode = types_1.NavigationMode.Push) {
        const args = { pageId, mode };
        return new CommandResult(types_1.CommandResultKind.GoToPage, args);
    }
    /**
     * Show a toast notification.
     * @param message The message to display
     * @param dismissAfterMs Optional auto-dismiss time in milliseconds
     */
    static showToast(message, dismissAfterMs) {
        const args = { message, dismissAfterMs };
        return new CommandResult(types_1.CommandResultKind.ShowToast, args);
    }
    /**
     * Show a confirmation dialog.
     * @param title The dialog title
     * @param description The dialog description
     * @param primaryCommand The command to execute on confirmation
     */
    static confirm(title, description, primaryCommand) {
        const args = { title, description, primaryCommand };
        return new CommandResult(types_1.CommandResultKind.Confirm, args);
    }
}
exports.CommandResult = CommandResult;
//# sourceMappingURL=results.js.map