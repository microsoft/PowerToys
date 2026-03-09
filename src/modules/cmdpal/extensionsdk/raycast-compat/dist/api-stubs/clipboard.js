"use strict";
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
Object.defineProperty(exports, "__esModule", { value: true });
exports.Clipboard = void 0;
/**
 * Raycast Clipboard compatibility stub.
 *
 * Maps Raycast's `Clipboard.copy()` / `Clipboard.paste()` / `Clipboard.read()`
 * to platform-appropriate clipboard access.
 *
 * For the spike: uses a PowerShell/clip.exe approach on Windows via child_process.
 * CmdPal integration will use the host's clipboard command pattern instead.
 */
const child_process_1 = require("child_process");
exports.Clipboard = {
    /**
     * Copy text (or rich content) to the system clipboard.
     */
    async copy(content) {
        const text = typeof content === 'string' ? content : (content.text ?? '');
        try {
            // Windows: pipe to clip.exe
            (0, child_process_1.execSync)('clip', { input: text, stdio: ['pipe', 'ignore', 'ignore'] });
            console.log(`[Clipboard] Copied ${text.length} chars`);
        }
        catch {
            console.warn('[Clipboard] copy failed — falling back to in-memory store');
            exports.Clipboard._inMemory = text;
        }
    },
    /**
     * Paste text into the frontmost application.
     * Limited support — Raycast can inject keystrokes, but CmdPal cannot.
     */
    async paste(content) {
        console.warn('[Clipboard] paste() has limited support in CmdPal — copying to clipboard instead');
        await exports.Clipboard.copy(content);
    },
    /**
     * Read current clipboard contents.
     */
    async read() {
        try {
            const text = (0, child_process_1.execSync)('powershell -command "Get-Clipboard"', { encoding: 'utf-8' }).trim();
            return { text };
        }
        catch {
            console.warn('[Clipboard] read failed — returning in-memory fallback');
            return { text: exports.Clipboard._inMemory };
        }
    },
    /**
     * Read plain text from the clipboard.
     */
    async readText() {
        const content = await exports.Clipboard.read();
        return content.text ?? '';
    },
    /** In-memory fallback when system clipboard is unavailable. */
    _inMemory: '',
};
//# sourceMappingURL=clipboard.js.map