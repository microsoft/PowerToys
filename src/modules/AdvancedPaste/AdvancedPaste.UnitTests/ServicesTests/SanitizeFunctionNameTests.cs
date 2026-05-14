// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using AdvancedPaste.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AdvancedPaste.UnitTests.ServicesTests;

[TestClass]
public sealed class SanitizeFunctionNameTests
{
    [TestMethod]
    [DataRow("MyAction", "MyAction")]
    [DataRow("my_action", "my_action")]
    [DataRow("Action123", "Action123")]
    [DataRow("_privateAction", "_privateAction")]
    public void SanitizeFunctionName_AsciiOnly_ReturnsUnchanged(string input, string expected)
    {
        var result = KernelServiceBase.SanitizeFunctionName(input);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("My翻譯Action", "MyAction")]
    [DataRow("Translate中文", "Translate")]
    [DataRow("日本語Test", "Test")]
    [DataRow("한글Action123", "Action123")]
    public void SanitizeFunctionName_MixedAsciiAndNonAscii_StripsNonAscii(string input, string expected)
    {
        var result = KernelServiceBase.SanitizeFunctionName(input);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("翻譯成中文")]
    [DataRow("日本語")]
    [DataRow("한국어")]
    [DataRow("العربية")]
    [DataRow("ελληνικά")]
    public void SanitizeFunctionName_PureNonAscii_ReturnsHashBasedName(string input)
    {
        var result = KernelServiceBase.SanitizeFunctionName(input);

        // Should start with "CustomAction_" prefix
        Assert.IsTrue(result.StartsWith("CustomAction_", StringComparison.Ordinal), $"Expected result to start with 'CustomAction_', got: {result}");

        // Should have valid ASCII characters only
        Assert.IsTrue(IsValidFunctionName(result), $"Expected valid function name, got: {result}");

        // Should be deterministic (same input produces same output)
        var result2 = KernelServiceBase.SanitizeFunctionName(input);
        Assert.AreEqual(result, result2, "Expected deterministic output for same input");
    }

    [TestMethod]
    [DataRow("1Action", "_1Action")]
    [DataRow("123Test", "_123Test")]
    [DataRow("9", "_9")]
    public void SanitizeFunctionName_StartsWithDigit_PrependUnderscore(string input, string expected)
    {
        var result = KernelServiceBase.SanitizeFunctionName(input);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("Action１２３", "Action")] // Full-width digits are non-ASCII
    [DataRow("Test٤٥٦", "Test")] // Arabic-Indic digits
    public void SanitizeFunctionName_UnicodeDigits_StripsUnicodeDigits(string input, string expected)
    {
        var result = KernelServiceBase.SanitizeFunctionName(input);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow("Açtión", "Atin")]
    [DataRow("Naïve", "Nave")]
    [DataRow("Résumé", "Rsum")]
    [DataRow("Zürich", "Zrich")]
    public void SanitizeFunctionName_AccentedLatin_StripsAccents(string input, string expected)
    {
        var result = KernelServiceBase.SanitizeFunctionName(input);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void SanitizeFunctionName_EmptyAfterSanitization_ReturnsFallback()
    {
        // All special characters that get stripped
        var result = KernelServiceBase.SanitizeFunctionName("!@#$%^&*()");

        // Should return a valid fallback name
        Assert.IsTrue(result.StartsWith("CustomAction_", StringComparison.Ordinal), $"Expected result to start with 'CustomAction_', got: {result}");
        Assert.IsTrue(IsValidFunctionName(result), $"Expected valid function name, got: {result}");
    }

    [TestMethod]
    public void SanitizeFunctionName_DifferentNonAsciiNames_ProduceDifferentHashes()
    {
        var result1 = KernelServiceBase.SanitizeFunctionName("翻譯成中文");
        var result2 = KernelServiceBase.SanitizeFunctionName("日本語に翻訳");

        Assert.AreNotEqual(result1, result2, "Different inputs should produce different hash-based names");
    }

    [TestMethod]
    [DataRow("Test Action", "TestAction")]
    [DataRow("Test-Action", "TestAction")]
    [DataRow("Test.Action", "TestAction")]
    public void SanitizeFunctionName_SpecialCharacters_StripsSpecialChars(string input, string expected)
    {
        var result = KernelServiceBase.SanitizeFunctionName(input);
        Assert.AreEqual(expected, result);
    }

    /// <summary>
    /// Validates that a function name matches Semantic Kernel requirements:
    /// - Only ASCII letters, digits, and underscores
    /// - Must start with a letter or underscore
    /// </summary>
    private static bool IsValidFunctionName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        // Must start with letter or underscore
        if (!((name[0] >= 'a' && name[0] <= 'z') ||
              (name[0] >= 'A' && name[0] <= 'Z') ||
              name[0] == '_'))
        {
            return false;
        }

        // All characters must be ASCII letters, digits, or underscores
        foreach (char c in name)
        {
            if (!((c >= 'a' && c <= 'z') ||
                  (c >= 'A' && c <= 'Z') ||
                  (c >= '0' && c <= '9') ||
                  c == '_'))
            {
                return false;
            }
        }

        return true;
    }
}
