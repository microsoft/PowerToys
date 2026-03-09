/**
 * Raycast Toast compatibility stub.
 *
 * Maps Raycast's `showToast()` and `Toast` to CmdPal concepts.
 * For the spike: logs to console and stores state. When CmdPal integration
 * lands, this will use the host's status bar / toast notification mechanism.
 */
export declare enum ToastStyle {
    Animated = "animated",
    Success = "success",
    Failure = "failure"
}
export interface ToastOptions {
    style?: ToastStyle;
    title: string;
    message?: string;
    primaryAction?: ToastAction;
    secondaryAction?: ToastAction;
}
export interface ToastAction {
    title: string;
    shortcut?: {
        modifiers: string[];
        key: string;
    };
    onAction?: (toast: Toast) => void | Promise<void>;
}
/**
 * Live toast instance. Raycast extensions hold onto these to update
 * toast state (e.g., change from Animated → Success after a fetch).
 */
export declare class Toast {
    static readonly Style: typeof ToastStyle;
    style: ToastStyle;
    title: string;
    message?: string;
    primaryAction?: ToastAction;
    secondaryAction?: ToastAction;
    constructor(options: ToastOptions);
    /** Update the toast in-place (Raycast pattern: mutate then call show). */
    show(): Promise<void>;
    hide(): Promise<void>;
}
/**
 * Show a toast notification.
 *
 * Raycast signature: `showToast(options)` or `showToast(style, title, message)`.
 */
export declare function showToast(optionsOrStyle: ToastOptions | ToastStyle, title?: string, message?: string): Promise<Toast>;
//# sourceMappingURL=toast.d.ts.map