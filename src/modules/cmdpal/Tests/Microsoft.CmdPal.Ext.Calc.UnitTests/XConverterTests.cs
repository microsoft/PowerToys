// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Numerics;
using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

[TestClass]
public class XConverterTests
{
    [DataTestMethod]
    [DataRow("3x5")]
    [DataRow("3X5")]
    [DataRow("10x2")]
    [DataRow("2X3")]
    [DataRow("pi x 5")]
    [DataRow("e X 2")]

    public void InputValid_XMultiplication_IsValid(string input)
    {
        var result = CalculateHelper.InputValid(input);
        Assert.IsTrue(result, $"'{input}' should be valid");
    }

    [DataTestMethod]
    [DataRow("3x5", "3 * 5")]
    [DataRow("3X5", "3 * 5")]
    [DataRow("10x2", "10 * 2")]
    [DataRow("2X3", "2 * 3")]
    public void FixHumanMultiplicationExpressions_XMultiplication_ConvertedToAsterisk(string input, string expectedContains)
    {
        var result = CalculateHelper.FixHumanMultiplicationExpressions(input);
        Assert.IsTrue(result.Contains('*'), $"'{input}' should be converted to contain '*'");
    }

    [DataTestMethod]
    [DataRow("0xFF")]
    [DataRow("0x10")]
    [DataRow("0XAB")]
    public void InputValid_HexLiterals_AreValid(string input)
    {
        var result = CalculateHelper.InputValid(input);
        Assert.IsTrue(result, $"Hex literal '{input}' should be valid");
    }

    [DataTestMethod]
    [DataRow("exp(5)")]
    [DataRow("EXP(5)")]
    [DataRow("max(3,5)")]
    public void FixHumanMultiplicationExpressions_FunctionNamesPreserved(string input)
    {
        var result = CalculateHelper.FixHumanMultiplicationExpressions(input);

        Assert.IsFalse(result.Contains("e*p"), "exp() should not have x replaced");
        Assert.IsFalse(result.Contains("m*x"), "max() should not have x replaced");
    }

    [DataTestMethod]
    [DataRow("3x5")]
    [DataRow("3X5")]
    public void NormalizeCharsForDisplayQuery_XNotReplaced_StaysAsX(string input)
    {
        var result = CalculateHelper.NormalizeCharsForDisplayQuery(input);
        Assert.IsTrue(result.Contains('x') || result.Contains('X'), $"'{input}' should preserve x in display");
    }

    [TestMethod]
    public void InputValid_XFollowedByHexLiteralNoSpace_IsValid()
    {
        var result = CalculateHelper.InputValid("5x0xFF");
        Assert.IsTrue(result, "5x0xFF should be valid (x multiplication before hex literal)");
    }

    [TestMethod]
    public void FixHumanMultiplicationExpressions_XWithMultipleBases_ConvertsCorrectly()
    {
        var input = "0xFFx5x0b1010";
        var result = CalculateHelper.FixHumanMultiplicationExpressions(input);

        Assert.IsTrue(result.Contains("0x"), "Hex literal 0x should be preserved");
        Assert.IsTrue(result.Contains("0b"), "Binary literal 0b should be preserved");
        var asteriskCount = result.Split('*').Length - 1;
        Assert.IsTrue(asteriskCount >= 2, "Both x multiplications should be converted to *");
    }
}
