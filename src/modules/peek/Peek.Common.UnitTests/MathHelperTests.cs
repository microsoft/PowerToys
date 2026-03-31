// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.Common.Helpers;

namespace Peek.Common.UnitTests
{
    [TestClass]
    public class MathHelperTests
    {
        #region Modulo

        [TestMethod]
        public void Modulo_PositiveNumbers_ShouldReturnStandardModulo()
        {
            Assert.AreEqual(1, MathHelper.Modulo(7, 3));
        }

        [TestMethod]
        public void Modulo_ZeroDividend_ShouldReturnZero()
        {
            Assert.AreEqual(0, MathHelper.Modulo(0, 5));
        }

        [TestMethod]
        public void Modulo_ExactDivision_ShouldReturnZero()
        {
            Assert.AreEqual(0, MathHelper.Modulo(6, 3));
        }

        [TestMethod]
        public void Modulo_NegativeDividend_ShouldReturnPositiveResult()
        {
            // -1 % 3 in C# returns -1, but Modulo should return 2
            Assert.AreEqual(2, MathHelper.Modulo(-1, 3));
        }

        [TestMethod]
        public void Modulo_NegativeDividend_LargerMagnitude_ShouldWrapCorrectly()
        {
            // -7 % 3: C# gives -1; proper modulo: (-7 % 3 + 3) % 3 = (-1 + 3) % 3 = 2
            Assert.AreEqual(2, MathHelper.Modulo(-7, 3));
        }

        [TestMethod]
        public void Modulo_NegativeDividend_ExactMultiple_ShouldReturnZero()
        {
            // -6 % 3 = 0
            Assert.AreEqual(0, MathHelper.Modulo(-6, 3));
        }

        [TestMethod]
        public void Modulo_PositiveDividend_DivisorOne_ShouldReturnZero()
        {
            Assert.AreEqual(0, MathHelper.Modulo(5, 1));
        }

        [TestMethod]
        public void Modulo_NegativeDividend_DivisorOne_ShouldReturnZero()
        {
            Assert.AreEqual(0, MathHelper.Modulo(-5, 1));
        }

        [TestMethod]
        public void Modulo_LargePositiveNumbers_ShouldWork()
        {
            Assert.AreEqual(1, MathHelper.Modulo(1000001, 1000000));
        }

        [TestMethod]
        public void Modulo_DividendLessThanDivisor_ShouldReturnDividend()
        {
            Assert.AreEqual(2, MathHelper.Modulo(2, 5));
        }

        [TestMethod]
        public void Modulo_NegativeDividend_MinusTwo_ModThree_ShouldReturnOne()
        {
            Assert.AreEqual(1, MathHelper.Modulo(-2, 3));
        }

        #endregion

        #region NumberOfDigits

        [TestMethod]
        public void NumberOfDigits_Zero_ShouldReturnOne()
        {
            Assert.AreEqual(1, MathHelper.NumberOfDigits(0));
        }

        [TestMethod]
        public void NumberOfDigits_SingleDigit_ShouldReturnOne()
        {
            Assert.AreEqual(1, MathHelper.NumberOfDigits(5));
        }

        [TestMethod]
        public void NumberOfDigits_TwoDigits_ShouldReturnTwo()
        {
            Assert.AreEqual(2, MathHelper.NumberOfDigits(42));
        }

        [TestMethod]
        public void NumberOfDigits_ThreeDigits_ShouldReturnThree()
        {
            Assert.AreEqual(3, MathHelper.NumberOfDigits(100));
        }

        [TestMethod]
        public void NumberOfDigits_NegativeNumber_ShouldIgnoreSign()
        {
            Assert.AreEqual(3, MathHelper.NumberOfDigits(-123));
        }

        [TestMethod]
        public void NumberOfDigits_MaxValue_ShouldReturnTenDigits()
        {
            // int.MaxValue = 2147483647, which has 10 digits
            Assert.AreEqual(10, MathHelper.NumberOfDigits(int.MaxValue));
        }

        [TestMethod]
        public void NumberOfDigits_MinValue_ShouldHandleAbsoluteValue()
        {
            // int.MinValue = -2147483648, Math.Abs would overflow but ToString handles it
            // The implementation uses Math.Abs(num).ToString().Length
            // For int.MinValue, Math.Abs throws - test the boundary just above it
            Assert.AreEqual(10, MathHelper.NumberOfDigits(int.MinValue + 1));
        }

        [TestMethod]
        public void NumberOfDigits_PowerOfTen_ShouldReturnCorrectCount()
        {
            Assert.AreEqual(1, MathHelper.NumberOfDigits(1));
            Assert.AreEqual(2, MathHelper.NumberOfDigits(10));
            Assert.AreEqual(3, MathHelper.NumberOfDigits(100));
            Assert.AreEqual(4, MathHelper.NumberOfDigits(1000));
            Assert.AreEqual(5, MathHelper.NumberOfDigits(10000));
        }

        [TestMethod]
        public void NumberOfDigits_BoundaryValues_NineAndTen()
        {
            Assert.AreEqual(1, MathHelper.NumberOfDigits(9));
            Assert.AreEqual(2, MathHelper.NumberOfDigits(10));
        }

        [TestMethod]
        public void NumberOfDigits_BoundaryValues_NinetyNineAndHundred()
        {
            Assert.AreEqual(2, MathHelper.NumberOfDigits(99));
            Assert.AreEqual(3, MathHelper.NumberOfDigits(100));
        }

        #endregion
    }
}
