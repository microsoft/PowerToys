// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

[TestClass]
public class BrowserInfoServiceExtensionsTests
{
    [TestMethod]
    public void OpenUsesDefaultBrowserCommandForWebUrl()
    {
        // Setup
        var browser = new BrowserInfo
        {
            Name = "Microsoft Edge",
            Path = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            ArgumentsPattern = "--single-argument %1",
        };
        var browserInfoService = new StubBrowserInfoService(browser);
        string? launchedPath = null;
        string? launchedPattern = null;
        string? launchedArguments = null;

        // Act
        var result = BrowserInfoServiceExtensions.Open(
            browserInfoService,
            "https://example.com/search?q=PowerToys",
            (path, pattern, arguments) =>
            {
                launchedPath = path;
                launchedPattern = pattern;
                launchedArguments = arguments;
                return true;
            });

        // Assert
        Assert.IsTrue(result);
        Assert.AreEqual(browser.Path, launchedPath);
        Assert.AreEqual(browser.ArgumentsPattern, launchedPattern);
        Assert.AreEqual("https://example.com/search?q=PowerToys", launchedArguments);
    }

    [TestMethod]
    public void OpenDoesNotLaunchWhenDefaultBrowserIsUnknown()
    {
        // Setup
        var browserInfoService = new StubBrowserInfoService(null);

        // Act
        var result = BrowserInfoServiceExtensions.Open(
            browserInfoService,
            "https://example.com",
            (_, _, _) =>
            {
                Assert.Fail("The browser command must not run without a default browser.");
                return false;
            });

        // Assert
        Assert.IsFalse(result);
    }

    private sealed class StubBrowserInfoService(BrowserInfo? browser) : IBrowserInfoService
    {
        public BrowserInfo? GetDefaultBrowser() => browser;
    }
}
