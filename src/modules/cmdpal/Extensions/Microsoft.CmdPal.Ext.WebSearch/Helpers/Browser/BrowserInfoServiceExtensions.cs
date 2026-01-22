// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;

/// <summary>
/// Extension methods for <see cref="IBrowserInfoService"/>.
/// </summary>
/// <seealso cref="IBrowserInfoService"/>
internal static class BrowserInfoServiceExtensions
{
    /// <summary>
    /// Opens the specified URL in the system's default web browser.
    /// </summary>
    /// <param name="browserInfoService">The browser information service used to resolve the system's default browser.</param>
    /// <param name="url">The URL to open.</param>
    /// <returns>
    /// <see langword="true"/> if a default browser is found and the URL launch command is issued successfully;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Returns <see langword="false"/> if the default browser cannot be determined.
    /// </remarks>
    public static bool Open(this IBrowserInfoService browserInfoService, string url)
    {
        var defaultBrowser = browserInfoService.GetDefaultBrowser();
        return defaultBrowser != null && ShellHelpers.OpenCommandInShell(defaultBrowser.Path, defaultBrowser.ArgumentsPattern, url);
    }
}
