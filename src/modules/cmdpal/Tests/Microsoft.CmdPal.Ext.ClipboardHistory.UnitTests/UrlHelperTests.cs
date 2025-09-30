// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public class UrlHelperTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("\t")]
    [DataRow("\r\n")]
    public void IsValidUrl_ReturnssFalse_WhenUrlIsNullOrWhitespace(string url)
    {
        // Act
        var result = UrlHelper.IsValidUrl(url);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("test\nurl")]
    [DataRow("test\rurl")]
    [DataRow("http://example.com\nmalicious")]
    [DataRow("https://test.com\r\nheader")]
    public void IsValidUrl_ReturnsFalse_WhenUrlContainsNewlines(string url)
    {
        // Act
        var result = UrlHelper.IsValidUrl(url);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("com")]
    [DataRow("org")]
    [DataRow("localhost")]
    [DataRow("test")]
    [DataRow("http")]
    [DataRow("https")]
    public void IsValidUrl_ReturnsFalse_WhenUrlDoesNotContainDot(string url)
    {
        // Act
        var result = UrlHelper.IsValidUrl(url);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("https://www.example.com")]
    [DataRow("http://test.org")]
    [DataRow("ftp://files.example.net")]
    [DataRow("file://localhost/path/to/file.txt")]
    [DataRow("https://subdomain.example.co.uk")]
    [DataRow("http://192.168.1.1")]
    [DataRow("https://example.com:8080/path")]
    public void IsValidUrl_ReturnsTrue_WhenUrlIsWellFormedAbsolute(string url)
    {
        // Act
        var result = UrlHelper.IsValidUrl(url);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow("www.example.com")]
    [DataRow("example.org")]
    [DataRow("subdomain.test.net")]
    [DataRow("github.com/user/repo")]
    [DataRow("stackoverflow.com/questions/123")]
    [DataRow("192.168.1.1")]
    public void IsValidUrl_ReturnsTrue_WhenUrlIsValidWithoutProtocol(string url)
    {
        // Act
        var result = UrlHelper.IsValidUrl(url);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow("not a url")]
    [DataRow("invalid..url")]
    [DataRow("http://")]
    [DataRow("https://")]
    [DataRow("://example.com")]
    [DataRow("ht tp://example.com")]
    public void IsValidUrl_ReturnsFalse_WhenUrlIsInvalid(string url)
    {
        // Act
        var result = UrlHelper.IsValidUrl(url);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow("  https://www.example.com  ")]
    [DataRow("\t\tgithub.com\t\t")]
    [DataRow(" \r\n stackoverflow.com \r\n ")]
    public void IsValidUrl_TrimsWhitespace_BeforeValidation(string url)
    {
        // Act
        var result = UrlHelper.IsValidUrl(url);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow("tel:+1234567890")]
    [DataRow("javascript:alert('test')")]
    public void IsValidUrl_ReturnsFalse_ForNonWebProtocols(string url)
    {
        // Act
        var result = UrlHelper.IsValidUrl(url);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void NormalizeUrl_ReturnsInput_WhenUrlIsNullOrWhitespace(string url)
    {
        // Act
        var result = UrlHelper.NormalizeUrl(url);

        // Assert
        Assert.AreEqual(url, result);
    }

    [TestMethod]
    [DataRow("https://www.example.com")]
    [DataRow("http://test.org")]
    [DataRow("ftp://files.example.net")]
    [DataRow("file://localhost/path/to/file.txt")]
    public void NormalizeUrl_ReturnsUnchanged_WhenUrlIsAlreadyWellFormed(string url)
    {
        // Act
        var result = UrlHelper.NormalizeUrl(url);

        // Assert
        Assert.AreEqual(url, result);
    }

    [TestMethod]
    [DataRow("www.example.com", "https://www.example.com")]
    [DataRow("example.org", "https://example.org")]
    [DataRow("github.com/user/repo", "https://github.com/user/repo")]
    [DataRow("stackoverflow.com/questions/123", "https://stackoverflow.com/questions/123")]
    public void NormalizeUrl_AddsHttpsPrefix_WhenNoProtocolPresent(string input, string expected)
    {
        // Act
        var result = UrlHelper.NormalizeUrl(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("  www.example.com  ", "https://www.example.com")]
    [DataRow("\t\tgithub.com\t\t", "https://github.com")]
    [DataRow(" \r\n stackoverflow.com \r\n ", "https://stackoverflow.com")]
    public void NormalizeUrl_TrimsWhitespace_BeforeNormalizing(string input, string expected)
    {
        // Act
        var result = UrlHelper.NormalizeUrl(input);

        // Assert
        Assert.AreEqual(expected, result);
    }
}
