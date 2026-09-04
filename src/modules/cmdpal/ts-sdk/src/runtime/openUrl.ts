// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/**
 * Opens a URL in the user's default browser. Command Palette runs on Windows,
 * so the default opener uses the shell `start` verb; macOS and Linux fallbacks
 * are provided so the SDK behaves during local development on other platforms.
 */

import { spawn } from 'node:child_process';

/** Opens the given URL. Injected into {@link OpenUrlCommand} for testing. */
export type UrlOpener = (url: string) => void;

function hasControlCharacters(value: string): boolean {
  for (let index = 0; index < value.length; index += 1) {
    if (value.charCodeAt(index) < 0x20) {
      return true;
    }
  }
  return false;
}

/**
 * Opens a URL in the default browser via the platform launcher.
 *
 * @throws Error when the URL contains a double quote or a control character,
 * which could break out of the launcher command line.
 */
export const openUrlInDefaultBrowser: UrlOpener = (url) => {
  if (url.includes('"') || hasControlCharacters(url)) {
    throw new Error(`Refusing to open a URL with quote or control characters: ${url}`);
  }

  if (process.platform === 'win32') {
    // The empty `""` title keeps `start` from treating the quoted URL as a
    // window title, and verbatim arguments preserve the quotes we add so that
    // characters such as `&` inside the query string are not reinterpreted.
    const child = spawn('cmd.exe', ['/s', '/c', 'start', '""', `"${url}"`], {
      windowsVerbatimArguments: true,
      detached: true,
      stdio: 'ignore',
      windowsHide: true,
    });
    child.unref();
    return;
  }

  const launcher = process.platform === 'darwin' ? 'open' : 'xdg-open';
  const child = spawn(launcher, [url], { detached: true, stdio: 'ignore' });
  child.unref();
};
