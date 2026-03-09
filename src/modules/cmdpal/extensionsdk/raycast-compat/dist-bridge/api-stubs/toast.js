"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.Toast = exports.ToastStyle = void 0;
exports.showToast = showToast;
/**
 * Raycast Toast compatibility stub.
 *
 * Maps Raycast's `showToast()` and `Toast` to CmdPal concepts.
 * For the spike: logs to console and stores state. When CmdPal integration
 * lands, this will use the host's status bar / toast notification mechanism.
 */
var ToastStyle;
(function (ToastStyle) {
    ToastStyle["Animated"] = "animated";
    ToastStyle["Success"] = "success";
    ToastStyle["Failure"] = "failure";
})(ToastStyle || (exports.ToastStyle = ToastStyle = {}));
/**
 * Live toast instance. Raycast extensions hold onto these to update
 * toast state (e.g., change from Animated → Success after a fetch).
 */
class Toast {
    static Style = ToastStyle;
    style;
    title;
    message;
    primaryAction;
    secondaryAction;
    constructor(options) {
        this.style = options.style ?? ToastStyle.Animated;
        this.title = options.title;
        this.message = options.message;
        this.primaryAction = options.primaryAction;
        this.secondaryAction = options.secondaryAction;
    }
    /** Update the toast in-place (Raycast pattern: mutate then call show). */
    async show() {
        const icon = this.style === ToastStyle.Success ? '✓' :
            this.style === ToastStyle.Failure ? '✗' : '⟳';
        console.error(`[Toast ${icon}] ${this.title}${this.message ? ': ' + this.message : ''}`);
    }
    async hide() {
        console.error(`[Toast] Hidden: ${this.title}`);
    }
}
exports.Toast = Toast;
/**
 * Show a toast notification.
 *
 * Raycast signature: `showToast(options)` or `showToast(style, title, message)`.
 */
async function showToast(optionsOrStyle, title, message) {
    let options;
    if (typeof optionsOrStyle === 'string') {
        // Legacy 3-arg form: showToast(style, title, message)
        options = { style: optionsOrStyle, title: title ?? '', message };
    }
    else {
        options = optionsOrStyle;
    }
    const toast = new Toast(options);
    await toast.show();
    return toast;
}
//# sourceMappingURL=toast.js.map