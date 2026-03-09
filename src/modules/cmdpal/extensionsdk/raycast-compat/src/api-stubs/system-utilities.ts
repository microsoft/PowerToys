// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Raycast system utility compatibility stubs.
 *
 * These cover system-level functions from `@raycast/api` like file
 * operations, application queries, and HUD display. On Windows we
 * provide real implementations where possible (e.g., `showInFinder`
 * → `explorer /select,`), otherwise stubs with helpful warnings.
 */

import { showToast, ToastStyle } from './toast';

// ── File system utilities ──────────────────────────────────────────────

/**
 * Reveal a file or folder in the system file manager.
 *
 * macOS: Finder → `open -R <path>`
 * Windows: Explorer → `explorer /select,"<path>"`
 */
export async function showInFinder(path: string): Promise<void> {
  try {
    const { execSync } = await import('child_process');
    // explorer /select, highlights the item in its parent folder
    execSync(`explorer /select,"${path.replace(/\//g, '\\')}"`, {
      stdio: 'ignore',
      shell: 'cmd.exe',
    });
    console.error(`[SystemUtils] Revealed in Explorer: ${path}`);
  } catch {
    console.warn(`[SystemUtils] Failed to reveal in Explorer: ${path}`);
  }
}

/**
 * Move a file or folder to the system trash/recycle bin.
 *
 * macOS: Finder trash
 * Windows: Recycle Bin via PowerShell
 */
export async function trash(path: string | string[]): Promise<void> {
  const paths = Array.isArray(path) ? path : [path];
  try {
    const { execSync } = await import('child_process');
    for (const p of paths) {
      const escaped = p.replace(/'/g, "''");
      execSync(
        `powershell -NoProfile -Command "Add-Type -AssemblyName Microsoft.VisualBasic; [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile('${escaped}', 'OnlyErrorDialogs', 'SendToRecycleBin')"`,
        { stdio: 'ignore' },
      );
    }
    console.error(`[SystemUtils] Moved to Recycle Bin: ${paths.join(', ')}`);
  } catch {
    console.warn(`[SystemUtils] Failed to move to Recycle Bin: ${paths.join(', ')}`);
  }
}

// ── HUD display ────────────────────────────────────────────────────────

/**
 * Show a brief heads-up display message.
 * Maps to a CmdPal toast with auto-dismiss style.
 */
export async function showHUD(title: string): Promise<void> {
  console.error(`[HUD] ${title}`);
  await showToast({ style: ToastStyle.Success, title });
}

// ── Text selection ─────────────────────────────────────────────────────

/**
 * Get the currently selected text in the frontmost application.
 * Stub: not supported on Windows in the CmdPal compat layer.
 */
export async function getSelectedText(): Promise<string> {
  console.warn('[SystemUtils] getSelectedText() is not supported in CmdPal');
  return '';
}

// ── Finder items ───────────────────────────────────────────────────────

export interface FileSystemItem {
  path: string;
}

/**
 * Get the files/folders currently selected in the file manager.
 * Stub: not supported in CmdPal.
 */
export async function getSelectedFinderItems(): Promise<FileSystemItem[]> {
  console.warn('[SystemUtils] getSelectedFinderItems() is not supported in CmdPal');
  return [];
}

// ── Application queries ────────────────────────────────────────────────

export interface Application {
  name: string;
  path: string;
  bundleId?: string;
}

/**
 * Get a list of installed applications.
 * Stub: returns an empty array.
 */
export async function getApplications(): Promise<Application[]> {
  console.warn('[SystemUtils] getApplications() is not yet implemented in CmdPal');
  return [];
}

/**
 * Get the default application for a file type or URL scheme.
 * Stub: returns a placeholder.
 */
export async function getDefaultApplication(
  pathOrUrl: string,
): Promise<Application> {
  void pathOrUrl;
  console.warn('[SystemUtils] getDefaultApplication() is not yet implemented in CmdPal');
  return { name: 'Default', path: '' };
}

/**
 * Get the frontmost (focused) application.
 * Stub: returns a placeholder.
 */
export async function getFrontmostApplication(): Promise<Application> {
  console.warn('[SystemUtils] getFrontmostApplication() is not yet implemented in CmdPal');
  return { name: 'Unknown', path: '' };
}
