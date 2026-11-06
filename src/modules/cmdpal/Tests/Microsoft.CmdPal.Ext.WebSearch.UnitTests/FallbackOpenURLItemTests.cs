// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.WebSearch.UnitTests;

[TestClass]
public class FallbackOpenURLItemTests
{
    [TestMethod]
    [DataRow("www.example.com", "https://www.example.com")]
    [DataRow("github.com/user/repo", "https://github.com/user/repo")]
    [DataRow("192.168.1.1:8080", "https://192.168.1.1:8080")]
    [DataRow("example.com:8080/path", "https://example.com:8080/path")]
    [DataRow("intranet.corp:8443", "https://intranet.corp:8443")]
    [DataRow("example.com:443", "https://example.com:443")]
    [DataRow("https://example.com:8080/path", "https://example.com:8080/path")]
    [DataRow("http://test.org", "http://test.org")]
    [DataRow("ftp://files.example.net", "ftp://files.example.net")]
    [DataRow("file:///C:/Users/Test/file.txt", "file:///C:/Users/Test/file.txt")]
    [DataRow("mailto:someone@example.com", "mailto:someone@example.com")]
    public void UpdateQueryAddsHttpsOnlyWhenNoSchemeIsPresent(string query, string expectedUrl)
    {
        // Setup
        var item = new FallbackOpenURLItem(new MockSettingsInterface(), new MockBrowserInfoService());

        // Act
        item.UpdateQuery(query);

        // Assert
        Assert.AreEqual(expectedUrl, ((OpenURLCommand)item.Command).Url);
    }
}
