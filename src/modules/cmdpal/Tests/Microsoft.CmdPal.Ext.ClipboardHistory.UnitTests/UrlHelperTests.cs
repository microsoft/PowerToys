// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
    public void IsValidUrl_ReturnsFalse_WhenUrlIsNullOrWhitespace(string url)
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

    [TestMethod]
    [DataRow(@"C:\Users\Test\Documents\file.txt")]
    [DataRow(@"D:\Projects\MyProject\readme.md")]
    [DataRow(@"E:\")]
    [DataRow(@"F:")]
    [DataRow(@"G:\folder\subfolder")]
    public void IsValidUrl_ReturnsTrue_ForValidLocalPaths(string path)
    {
        // Act
        var result = UrlHelper.IsValidUrl(path);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow(@"\\server\share")]
    [DataRow(@"\\server\share\folder")]
    [DataRow(@"\\192.168.1.100\public")]
    [DataRow(@"\\myserver\documents\file.docx")]
    [DataRow(@"\\domain.com\share\folder\file.pdf")]
    public void IsValidUrl_ReturnsTrue_ForValidNetworkPaths(string path)
    {
        // Act
        var result = UrlHelper.IsValidUrl(path);

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow(@"\\")]
    [DataRow(@":")]
    [DataRow(@"Z")]
    [DataRow(@"folder")]
    [DataRow(@"folder\file.txt")]
    [DataRow(@"documents\project\readme.md")]
    [DataRow(@"./config/settings.json")]
    [DataRow(@"../data/input.csv")]
    public void IsValidUrl_ReturnsFalse_ForInvalidPathsAndRelativePaths(string path)
    {
        // Act
        var result = UrlHelper.IsValidUrl(path);

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    [DataRow(@"C:\Users\Test\Documents\file.txt")]
    [DataRow(@"D:\Projects\MyProject")]
    [DataRow(@"E:\")]
    public void NormalizeUrl_ConvertsLocalPathToFileUri_WhenValidLocalPath(string path)
    {
        // Act
        var result = UrlHelper.NormalizeUrl(path);

        // Assert
        Assert.IsTrue(result.StartsWith("file:///", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.Contains(path.Replace('\\', '/')));
    }

    [TestMethod]
    [DataRow(@"\\server\share")]
    [DataRow(@"\\192.168.1.100\public")]
    [DataRow(@"\\myserver\documents")]
    public void NormalizeUrl_ConvertsNetworkPathToFileUri_WhenValidNetworkPath(string path)
    {
        // Act
        var result = UrlHelper.NormalizeUrl(path);

        // Assert
        Assert.IsTrue(result.StartsWith("file://", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.Contains(path.Replace('\\', '/')));
    }

    [TestMethod]
    [DataRow("file:///C:/Users/Test/file.txt")]
    [DataRow("file://server/share/folder")]
    public void NormalizeUrl_ReturnsUnchanged_WhenAlreadyFileUri(string fileUri)
    {
        // Act
        var result = UrlHelper.NormalizeUrl(fileUri);

        // Assert
        Assert.AreEqual(fileUri, result);
    }
}
