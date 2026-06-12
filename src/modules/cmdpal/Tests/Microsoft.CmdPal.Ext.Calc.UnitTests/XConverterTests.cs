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
    [DataRow("3x(5)")]
    [DataRow("(10)x5")]
    [DataRow("3x0xFF")]
    [DataRow("0xFFx5")]
    [DataRow("5 x 10")]
    [DataRow("pi x e")]
    public void InputValid_XMultiplication_IsValid(string input)
    {
        var normalized = CalculateHelper.NormalizeMultiplicationSymbols(input);
        var result = CalculateHelper.InputValid(normalized);
        Assert.IsTrue(result, $"'{input}' should be valid");
    }

    [DataTestMethod]
    [DataRow("0xFF")]
    [DataRow("0x10")]
    [DataRow("0XAB")]
    [DataRow("0X123")]
    [DataRow("0x123+0XFF")]
    public void InputValid_HexLiterals_AreValid(string input)
    {
        var result = CalculateHelper.InputValid(input);
        Assert.IsTrue(result, $"Hex literal '{input}' should be valid");
    }

    [DataTestMethod]
    [DataRow("exp(5)")]
    [DataRow("max(3,5)")]
    [DataRow("max (3, 5)")]
    public void InputValid_FunctionNames_AreValid(string input)
    {
        var result = CalculateHelper.InputValid(input);
        Assert.IsTrue(result, $"Function '{input}' should be valid");
    }

    [DataTestMethod]
    [DataRow("3x5", "3*5")]
    [DataRow("3X5", "3*5")]
    [DataRow("10 x 2", "10*2")]
    [DataRow("2 X 3", "2*3")]
    [DataRow("pi x 5", "pi*5")]
    [DataRow("e X 2", "e*2")]
    public void NormalizeMultiplicationSymbols_BasicXMultiplication_CorrectlyConverted(string input, string expected)
    {
        var result = CalculateHelper.NormalizeMultiplicationSymbols(input);
        Assert.AreEqual(expected, result, $"'{input}' should convert to '{expected}'");
    }

    [DataTestMethod]
    [DataRow("0XFF", "0xFF")]
    [DataRow("0X123", "0x123")]
    [DataRow("0XAB+5", "0xAB+5")]
    public void NormalizeMultiplicationSymbols_UppercaseHexPrefix_NormalizedToLowercase(string input, string expected)
    {
        var result = CalculateHelper.NormalizeMultiplicationSymbols(input);
        Assert.AreEqual(expected, result, $"'{input}' uppercase hex prefix should normalize to '{expected}'");
    }

    [TestMethod]
    public void NormalizeMultiplicationSymbols_HexFollowedByXMultiplication_PreservesHexAndConverts()
    {
        var input = "0xFFx5";
        var result = CalculateHelper.NormalizeMultiplicationSymbols(input);
        Assert.AreEqual("0xFF*5", result, "Hex literal should be preserved, x converted to *");
    }

    [DataTestMethod]
    [DataRow("exp(5)", "exp(5)")]
    [DataRow("max(3,5)", "max(3,5)")]
    [DataRow("max (3, 5)", "max (3, 5)")]
    public void NormalizeMultiplicationSymbols_FunctionNames_Preserved(string input, string expected)
    {
        var result = CalculateHelper.NormalizeMultiplicationSymbols(input);
        Assert.AreEqual(expected, result, $"Function '{input}' should remain unchanged as '{expected}'");
    }

    [TestMethod]
    public void NormalizeMultiplicationSymbols_MultipleBasesWithXMultiplication_CorrectlyHandled()
    {
        var input = "0xFFx5x0b1010";
        var result = CalculateHelper.NormalizeMultiplicationSymbols(input);
        Assert.AreEqual("0xFF*5*0b1010", result, "Both x multiplications should convert, bases preserved");
    }

    [TestMethod]
    public void NormalizeMultiplicationSymbols_BracketedExpressions_CorrectlyHandled()
    {
        var input = "3x(5x2)";
        var result = CalculateHelper.NormalizeMultiplicationSymbols(input);
        Assert.AreEqual("3*(5*2)", result, "Both x multiplications in nested expressions should convert");
    }

    [TestMethod]
    public void NormalizeMultiplicationSymbols_ComplexExpressionWithFunctionAndBases_CorrectlyHandled()
    {
        var input = "3x(5 x max(0xFF, 256))";
        var result = CalculateHelper.NormalizeMultiplicationSymbols(input);

        // Only the clearly between-operands x's will convert
        Assert.AreEqual("3*(5*max(0xFF, 256))", result);
    }

    [TestMethod]
    public void NormalizeMultiplicationSymbols_TrailingX_NotConverted()
    {
        var input = "5x";
        var result = CalculateHelper.NormalizeMultiplicationSymbols(input);
        Assert.AreEqual("5x", result, "Trailing x should not be converted (will be caught by validator)");
    }

    [TestMethod]
    public void NormalizeMultiplicationSymbols_LeadingX_NotConverted()
    {
        var input = "x5";
        var result = CalculateHelper.NormalizeMultiplicationSymbols(input);
        Assert.AreEqual("x5", result, "Leading x should not be converted (will be caught by validator)");
    }

    [TestMethod]
    public void NormalizeMultiplicationSymbols_BareXSequence_NotConverted()
    {
        var input = "xXxXx";
        var result = CalculateHelper.NormalizeMultiplicationSymbols(input);
        Assert.AreEqual("xXxXx", result, "Bare x sequence should not be converted (will be caught by validator)");
    }
}
