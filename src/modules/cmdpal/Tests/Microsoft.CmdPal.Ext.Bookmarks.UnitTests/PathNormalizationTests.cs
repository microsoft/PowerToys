// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Text;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

/// <summary>
/// Tests for <see cref="PathNormalization.NormalizePathForWindows(string)"/>.
/// <para>
/// Long-path / NT-object prefix stripping is Windows-only behavior, so the
/// prefix-handling tests assert Windows expectations and use
/// <see cref="Assert.Inconclusive(string)"/> on non-Windows agents so CI on
/// Linux/macOS skips them cleanly without reporting failures.
/// </para>
/// <para>
/// Cross-platform behavior (NFC, trailing whitespace) is asserted on every OS.
/// </para>
/// </summary>
[TestClass]
public class PathNormalizationTests
{
    private static void RequireWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows-only behavior; skipped on non-Windows CI agents.");
        }
    }

    [TestMethod]
    public void Null_Or_Empty_Is_Returned_Unchanged()
    {
        Assert.AreEqual(string.Empty, PathNormalization.NormalizePathForWindows(string.Empty));
        Assert.AreEqual(null, PathNormalization.NormalizePathForWindows(null!));
    }

    [TestMethod]
    public void Ascii_Path_Is_Unchanged()
    {
        const string path = @"C:\Users\Public\Documents\readme.txt";
        Assert.AreEqual(path, PathNormalization.NormalizePathForWindows(path));
    }

    [TestMethod]
    public void Decomposed_Accents_Are_Normalized_To_NFC()
    {
        // "ÉCOLES" composed two ways: precomposed (NFC) vs decomposed (NFD).
        var nfc = "\u00C9COLES"; // É (U+00C9) + COLES
        var nfd = "E\u0301COLES"; // E + COMBINING ACUTE ACCENT (U+0301) + COLES

        Assert.AreNotEqual(nfc, nfd, "Sanity: the two encodings differ before normalization.");

        var normalized = PathNormalization.NormalizePathForWindows(nfd);

        Assert.AreEqual(nfc, normalized);
        Assert.IsTrue(normalized.IsNormalized(NormalizationForm.FormC));
    }

    [TestMethod]
    public void NonAscii_Characters_Are_Preserved_Without_Loss()
    {
        // Mix of accented Latin, CJK, emoji (surrogate pair), and a Cyrillic letter.
        const string original = "C:\\données\\café\\日本語\\🚀\\Привет.txt";

        var normalized = PathNormalization.NormalizePathForWindows(original);

        Assert.AreEqual(original.Normalize(NormalizationForm.FormC), normalized);
        StringAssert.Contains(normalized, "données");
        StringAssert.Contains(normalized, "café");
        StringAssert.Contains(normalized, "日本語");
        StringAssert.Contains(normalized, "🚀");
        StringAssert.Contains(normalized, "Привет");
    }

    [TestMethod]
    public void Trailing_Ascii_Whitespace_Is_Trimmed()
    {
        Assert.AreEqual(@"C:\foo\bar.txt", PathNormalization.NormalizePathForWindows("C:\\foo\\bar.txt   "));
        Assert.AreEqual(@"C:\foo\bar.txt", PathNormalization.NormalizePathForWindows("C:\\foo\\bar.txt\t"));
        Assert.AreEqual(@"C:\foo\bar.txt", PathNormalization.NormalizePathForWindows("C:\\foo\\bar.txt\r\n"));
    }

    [TestMethod]
    public void Trailing_NonBreaking_Space_Is_Trimmed()
    {
        // U+00A0 NO-BREAK SPACE — frequently introduced by copy/paste from web pages.
        var input = "C:\\foo\\bar.txt\u00A0";
        Assert.AreEqual(@"C:\foo\bar.txt", PathNormalization.NormalizePathForWindows(input));
    }

    [TestMethod]
    public void Internal_Whitespace_Is_Preserved()
    {
        const string input = @"C:\Program Files\My App\file name.txt";
        Assert.AreEqual(input, PathNormalization.NormalizePathForWindows(input));
    }

    [TestMethod]
    public void Trailing_Dots_Are_Preserved()
    {
        // Trimming trailing dots could be lossy (e.g., "foo..." vs "foo").
        // We deliberately keep them; Windows callers can decide what to do.
        const string input = @"C:\foo\bar...";
        Assert.AreEqual(input, PathNormalization.NormalizePathForWindows(input));
    }

    [TestMethod]
    public void LongPath_Prefix_Is_Stripped_On_Windows()
    {
        RequireWindows();

        Assert.AreEqual(
            @"C:\very\long\path\file.txt",
            PathNormalization.NormalizePathForWindows(@"\\?\C:\very\long\path\file.txt"));
    }

    [TestMethod]
    public void LongUnc_Prefix_Is_Rewritten_To_Unc_On_Windows()
    {
        RequireWindows();

        Assert.AreEqual(
            @"\\server\share\file.txt",
            PathNormalization.NormalizePathForWindows(@"\\?\UNC\server\share\file.txt"));
    }

    [TestMethod]
    public void NtObject_Prefix_Is_Stripped_On_Windows()
    {
        RequireWindows();

        Assert.AreEqual(
            @"C:\Windows\System32\notepad.exe",
            PathNormalization.NormalizePathForWindows(@"\??\C:\Windows\System32\notepad.exe"));
    }

    [TestMethod]
    public void Unc_Without_LongPath_Prefix_Is_Untouched_On_Windows()
    {
        RequireWindows();

        const string input = @"\\server\share\file.txt";
        Assert.AreEqual(input, PathNormalization.NormalizePathForWindows(input));
    }
}
