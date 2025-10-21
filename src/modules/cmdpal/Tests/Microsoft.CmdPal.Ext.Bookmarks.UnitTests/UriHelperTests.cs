// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class UriHelperTests
{
    private static bool TryGetScheme(ReadOnlySpan<char> input, out string scheme, out string remainder)
    {
        return UriHelper.TryGetScheme(input, out scheme, out remainder);
    }

    [DataTestMethod]
    [DataRow("http://example.com", "http", "//example.com")]
    [DataRow("ftp:", "ftp", "")]
    [DataRow("my-app:payload", "my-app", "payload")]
    [DataRow("x-cmdpal://settings/", "x-cmdpal", "//settings/")]
    [DataRow("custom+ext.-scheme:xyz", "custom+ext.-scheme", "xyz")]
    [DataRow("MAILTO:foo@bar", "MAILTO", "foo@bar")]
    [DataRow("a:b", "a", "b")]
    public void TryGetScheme_ValidSchemes_ReturnsTrueAndSplits(string input, string expectedScheme, string expectedRemainder)
    {
        var ok = TryGetScheme(input.AsSpan(), out var scheme, out var remainder);

        Assert.IsTrue(ok, "Expected valid scheme.");
        Assert.AreEqual(expectedScheme, scheme);
        Assert.AreEqual(expectedRemainder, remainder);
    }

    [TestMethod]
    public void TryGetScheme_OnlySchemeAndColon_ReturnsEmptyRemainder()
    {
        var ok = TryGetScheme("http:".AsSpan(), out var scheme, out var remainder);

        Assert.IsTrue(ok);
        Assert.AreEqual("http", scheme);
        Assert.AreEqual(string.Empty, remainder);
    }

    [DataTestMethod]
    [DataRow("123:http")] // starts with digit
    [DataRow(":nope")] // colon at start
    [DataRow("noColon")] // no colon at all
    [DataRow("bad_scheme:")] // underscore not allowed
    [DataRow("bad*scheme:")] // asterisk not allowed
    [DataRow(":")] // syntactically invalid literal just for completeness; won't compile, example only
    public void TryGetScheme_InvalidInputs_ReturnsFalse(string input)
    {
        var ok = TryGetScheme(input.AsSpan(), out var scheme, out var remainder);

        Assert.IsFalse(ok);
        Assert.AreEqual(string.Empty, scheme);
        Assert.AreEqual(string.Empty, remainder);
    }

    [TestMethod]
    public void TryGetScheme_MultipleColons_SplitsOnFirst()
    {
        const string input = "shell:::{645FF040-5081-101B-9F08-00AA002F954E}";
        var ok = TryGetScheme(input.AsSpan(), out var scheme, out var remainder);

        Assert.IsTrue(ok);
        Assert.AreEqual("shell", scheme);
        Assert.AreEqual("::{645FF040-5081-101B-9F08-00AA002F954E}", remainder);
    }

    [TestMethod]
    public void TryGetScheme_MinimumLength_OneLetterAndColon()
    {
        var ok = TryGetScheme("a:".AsSpan(), out var scheme, out var remainder);

        Assert.IsTrue(ok);
        Assert.AreEqual("a", scheme);
        Assert.AreEqual(string.Empty, remainder);
    }

    [TestMethod]
    public void TryGetScheme_TooShort_ReturnsFalse()
    {
        Assert.IsFalse(TryGetScheme("a".AsSpan(), out _, out _), "No colon.");
        Assert.IsFalse(TryGetScheme(":".AsSpan(), out _, out _), "Colon at start; no scheme.");
    }

    [DataTestMethod]
    [DataRow("HTTP://x", "HTTP", "//x")]
    [DataRow("hTtP:rest", "hTtP", "rest")]
    public void TryGetScheme_CaseIsPreserved(string input, string expectedScheme, string expectedRemainder)
    {
        var ok = TryGetScheme(input.AsSpan(), out var scheme, out var remainder);

        Assert.IsTrue(ok);
        Assert.AreEqual(expectedScheme, scheme);
        Assert.AreEqual(expectedRemainder, remainder);
    }

    [TestMethod]
    public void TryGetScheme_WhitespaceInsideScheme_Fails()
    {
        Assert.IsFalse(TryGetScheme("ht tp:rest".AsSpan(), out _, out _));
    }

    [TestMethod]
    public void TryGetScheme_PlusMinusDot_AllowedInMiddleOnly()
    {
        Assert.IsTrue(TryGetScheme("a+b.c-d:rest".AsSpan(), out var s1, out var r1));
        Assert.AreEqual("a+b.c-d", s1);
        Assert.AreEqual("rest", r1);

        // The first character must be a letter; plus is not allowed as first char
        Assert.IsFalse(TryGetScheme("+abc:rest".AsSpan(), out _, out _));
        Assert.IsFalse(TryGetScheme(".abc:rest".AsSpan(), out _, out _));
        Assert.IsFalse(TryGetScheme("-abc:rest".AsSpan(), out _, out _));
    }
}
