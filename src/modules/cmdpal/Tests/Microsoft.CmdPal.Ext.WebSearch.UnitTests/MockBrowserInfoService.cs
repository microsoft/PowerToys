// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

public class MockBrowserInfoService : IBrowserInfoService
{
    public BrowserInfo GetDefaultBrowser() => new() { Name = "mocked browser", Path = "C:\\mockery\\mock.exe" };
}
