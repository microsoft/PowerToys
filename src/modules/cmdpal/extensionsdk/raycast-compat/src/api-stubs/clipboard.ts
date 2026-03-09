// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast Clipboard compatibility stub.
 *
 * Maps Raycast's `Clipboard.copy()` / `Clipboard.paste()` / `Clipboard.read()`
 * to platform-appropriate clipboard access.
 *
 * For the spike: uses a PowerShell/clip.exe approach on Windows via child_process.
 * CmdPal integration will use the host's clipboard command pattern instead.
 */

import { execSync } from 'child_process';

export interface ClipboardContent {
  text?: string;
  html?: string;
  file?: string;
}

export const Clipboard = {
  /**
   * Copy text (or rich content) to the system clipboard.
   */
  async copy(content: string | ClipboardContent): Promise<void> {
    const text = typeof content === 'string' ? content : (content.text ?? '');
    try {
      // Windows: pipe to clip.exe
      execSync('clip', { input: text, stdio: ['pipe', 'ignore', 'ignore'] });
      console.error(`[Clipboard] Copied ${text.length} chars`);
    } catch {
      console.warn('[Clipboard] copy failed — falling back to in-memory store');
      Clipboard._inMemory = text;
    }
  },

  /**
   * Paste text into the frontmost application.
   * Limited support — Raycast can inject keystrokes, but CmdPal cannot.
   */
  async paste(content: string): Promise<void> {
    console.warn('[Clipboard] paste() has limited support in CmdPal — copying to clipboard instead');
    await Clipboard.copy(content);
  },

  /**
   * Read current clipboard contents.
   */
  async read(): Promise<ClipboardContent> {
    try {
      const text = execSync('powershell -command "Get-Clipboard"', { encoding: 'utf-8' }).trim();
      return { text };
    } catch {
      console.warn('[Clipboard] read failed — returning in-memory fallback');
      return { text: Clipboard._inMemory };
    }
  },

  /**
   * Read plain text from the clipboard.
   */
  async readText(): Promise<string> {
    const content = await Clipboard.read();
    return content.text ?? '';
  },

  /** In-memory fallback when system clipboard is unavailable. */
  _inMemory: '',
};
