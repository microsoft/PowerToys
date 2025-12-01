// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;

/// <summary>
///     Provides functionality to retrieve information about the system's default web browser.
/// </summary>
public interface IBrowserInfoService
{
    /// <summary>
    ///     Gets information about the system's default web browser.
    /// </summary>
    /// <returns></returns>
    BrowserInfo? GetDefaultBrowser();
}
