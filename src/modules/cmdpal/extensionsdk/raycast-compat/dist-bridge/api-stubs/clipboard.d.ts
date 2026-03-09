export interface ClipboardContent {
    text?: string;
    html?: string;
    file?: string;
}
export declare const Clipboard: {
    /**
     * Copy text (or rich content) to the system clipboard.
     */
    copy(content: string | ClipboardContent): Promise<void>;
    /**
     * Paste text into the frontmost application.
     * Limited support — Raycast can inject keystrokes, but CmdPal cannot.
     */
    paste(content: string): Promise<void>;
    /**
     * Read current clipboard contents.
     */
    read(): Promise<ClipboardContent>;
    /**
     * Read plain text from the clipboard.
     */
    readText(): Promise<string>;
    /** In-memory fallback when system clipboard is unavailable. */
    _inMemory: string;
};
//# sourceMappingURL=clipboard.d.ts.map