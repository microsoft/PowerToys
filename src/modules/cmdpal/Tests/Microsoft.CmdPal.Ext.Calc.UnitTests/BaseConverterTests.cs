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
public class BaseConverterTests
{
    // Hex tests
    [DataTestMethod]
    [DataRow(0L, "0x0")]
    [DataRow(1L, "0x1")]
    [DataRow(16L, "0x10")]
    [DataRow(255L, "0xFF")]
    [DataRow(-1L, "-0x1")]
    [DataRow(long.MaxValue, "0x7FFFFFFFFFFFFFFF")]
    public void Convert_Hex_ReturnsExpected_WhenCalled(long input, string expected)
    {
        var result = BaseConverter.Convert(new BigInteger(input), 16);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Convert_Hex_HandlesLargeValues_WhenCalled()
    {
        var large = BigInteger.Parse("99999999999999999999", CultureInfo.InvariantCulture);
        var result = BaseConverter.Convert(large, 16);
        Assert.AreEqual("0x56BC75E2D630FFFFF", result);
    }

    // Binary tests
    [DataTestMethod]
    [DataRow(0L, "0b0")]
    [DataRow(1L, "0b1")]
    [DataRow(10L, "0b1010")]
    [DataRow(255L, "0b11111111")]
    [DataRow(-5L, "-0b101")]
    public void Convert_Binary_ReturnsExpected_WhenCalled(long input, string expected)
    {
        var result = BaseConverter.Convert(new BigInteger(input), 2);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Convert_Binary_HandlesLargeValues_WhenCalled()
    {
        var large = BigInteger.Parse("99999999999999999999", CultureInfo.InvariantCulture);
        var result = BaseConverter.Convert(large, 2);
        Assert.IsTrue(result.StartsWith("0b", StringComparison.Ordinal));
        Assert.IsTrue(result.Length > 60);
    }

    // Octal tests
    [DataTestMethod]
    [DataRow(0L, "0o0")]
    [DataRow(1L, "0o1")]
    [DataRow(8L, "0o10")]
    [DataRow(255L, "0o377")]
    [DataRow(-1L, "-0o1")]
    [DataRow(-255L, "-0o377")]
    [DataRow(long.MaxValue, "0o777777777777777777777")]
    public void Convert_Octal_ReturnsExpected_WhenCalled(long input, string expected)
    {
        var result = BaseConverter.Convert(new BigInteger(input), 8);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Convert_Octal_HandlesLargeValues_WhenCalled()
    {
        var large = (BigInteger)long.MaxValue + 1;
        var result = BaseConverter.Convert(large, 8);
        Assert.AreEqual("0o1000000000000000000000", result);
    }

    [TestMethod]
    public void Convert_Octal_HandlesLargeNegativeValues_WhenCalled()
    {
        var value = -BigInteger.Parse("99999999999999999999", CultureInfo.InvariantCulture);
        var result = BaseConverter.Convert(value, 8);
        Assert.IsTrue(result.StartsWith("-0o", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Convert_Hex_DecimalMaxValue_WhenCalled()
    {
        var value = BigInteger.Parse("79228162514264337593543950335", CultureInfo.InvariantCulture);
        Assert.AreEqual("0xFFFFFFFFFFFFFFFFFFFFFFFF", BaseConverter.Convert(value, 16));
    }

    [TestMethod]
    public void Convert_Hex_NegativeDecimalMaxValue_WhenCalled()
    {
        var value = -BigInteger.Parse("79228162514264337593543950335", CultureInfo.InvariantCulture);
        Assert.AreEqual("-0xFFFFFFFFFFFFFFFFFFFFFFFF", BaseConverter.Convert(value, 16));
    }

    [TestMethod]
    public void Convert_Binary_DecimalMaxValue_WhenCalled()
    {
        var value = BigInteger.Parse("79228162514264337593543950335", CultureInfo.InvariantCulture);
        var result = BaseConverter.Convert(value, 2);
        Assert.AreEqual("0b" + new string('1', 96), result);
    }

    [TestMethod]
    public void Convert_Binary_NegativeDecimalMaxValue_WhenCalled()
    {
        var value = -BigInteger.Parse("79228162514264337593543950335", CultureInfo.InvariantCulture);
        var result = BaseConverter.Convert(value, 2);
        Assert.AreEqual("-0b" + new string('1', 96), result);
    }

    [TestMethod]
    public void Convert_Octal_DecimalMaxValue_WhenCalled()
    {
        var value = BigInteger.Parse("79228162514264337593543950335", CultureInfo.InvariantCulture);
        Assert.AreEqual("0o" + new string('7', 32), BaseConverter.Convert(value, 8));
    }

    [TestMethod]
    public void Convert_Octal_NegativeDecimalMaxValue_WhenCalled()
    {
        var value = -BigInteger.Parse("79228162514264337593543950335", CultureInfo.InvariantCulture);
        Assert.AreEqual("-0o" + new string('7', 32), BaseConverter.Convert(value, 8));
    }

    // Invalid base
    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(17)]
    [DataRow(-1)]
    public void Convert_ThrowsArgumentOutOfRange_WhenBaseInvalid(int toBase)
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => BaseConverter.Convert(BigInteger.One, toBase));
    }
}
