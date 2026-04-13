// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.Common.Helpers;

namespace Peek.Common.UnitTests
{
    [TestClass]
    public class MathHelperTests
    {
        /// <summary>
        /// Product code: MathHelper.Modulo(int, int)
        /// What: Verifies standard modulo for positive numbers (7 mod 3 = 1)
        /// Why: Baseline correctness — ensures the positive path matches C# % operator
        /// </summary>
        [TestMethod]
        public void Modulo_PositiveNumbers_ShouldReturnStandardModulo()
        {
            Assert.AreEqual(1, MathHelper.Modulo(7, 3));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int)
        /// What: Verifies that 0 mod n = 0
        /// Why: Zero dividend is a common boundary — must not throw or return non-zero
        /// </summary>
        [TestMethod]
        public void Modulo_ZeroDividend_ShouldReturnZero()
        {
            Assert.AreEqual(0, MathHelper.Modulo(0, 5));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int)
        /// What: Verifies that exact division yields zero remainder
        /// Why: Guards against off-by-one in the modulo formula
        /// </summary>
        [TestMethod]
        public void Modulo_ExactDivision_ShouldReturnZero()
        {
            Assert.AreEqual(0, MathHelper.Modulo(6, 3));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int) — negative dividend path
        /// What: Verifies that -1 mod 3 = 2 (mathematical modulo, not C# remainder)
        /// Why: C# % returns -1 for negative dividends; Modulo wraps to positive range
        /// </summary>
        [TestMethod]
        public void Modulo_NegativeDividend_ShouldReturnPositiveResult()
        {
            Assert.AreEqual(2, MathHelper.Modulo(-1, 3));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int) — negative dividend with larger magnitude
        /// What: Verifies -7 mod 3 = 2 (wraps correctly for larger negative values)
        /// Why: Tests the full formula: ((-7 % 3) + 3) % 3 = (-1 + 3) % 3 = 2
        /// </summary>
        [TestMethod]
        public void Modulo_NegativeDividend_LargerMagnitude_ShouldWrapCorrectly()
        {
            Assert.AreEqual(2, MathHelper.Modulo(-7, 3));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int)
        /// What: Verifies -6 mod 3 = 0 (exact negative multiple)
        /// Why: Edge case where negative dividend is an exact multiple of divisor
        /// </summary>
        [TestMethod]
        public void Modulo_NegativeDividend_ExactMultiple_ShouldReturnZero()
        {
            Assert.AreEqual(0, MathHelper.Modulo(-6, 3));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int)
        /// What: Verifies n mod 1 = 0 for positive n
        /// Why: Divisor of 1 always yields 0 — guards against divide-by-zero edge case
        /// </summary>
        [TestMethod]
        public void Modulo_PositiveDividend_DivisorOne_ShouldReturnZero()
        {
            Assert.AreEqual(0, MathHelper.Modulo(5, 1));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int)
        /// What: Verifies -n mod 1 = 0 for negative n
        /// Why: Tests the negative path with divisor=1 (simplest negative case)
        /// </summary>
        [TestMethod]
        public void Modulo_NegativeDividend_DivisorOne_ShouldReturnZero()
        {
            Assert.AreEqual(0, MathHelper.Modulo(-5, 1));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int)
        /// What: Verifies modulo works with large numbers (1000001 mod 1000000 = 1)
        /// Why: Tests that no integer overflow occurs in the formula for large operands
        /// </summary>
        [TestMethod]
        public void Modulo_LargePositiveNumbers_ShouldWork()
        {
            Assert.AreEqual(1, MathHelper.Modulo(1000001, 1000000));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int)
        /// What: Verifies that dividend less than divisor returns the dividend itself
        /// Why: 2 mod 5 = 2 — no division happens, just returns the dividend
        /// </summary>
        [TestMethod]
        public void Modulo_DividendLessThanDivisor_ShouldReturnDividend()
        {
            Assert.AreEqual(2, MathHelper.Modulo(2, 5));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int) — negative dividend
        /// What: Verifies -2 mod 3 = 1 (mathematical modulo)
        /// Why: Tests another negative wrap case: ((-2 % 3) + 3) % 3 = (-2 + 3) % 3 = 1
        /// </summary>
        [TestMethod]
        public void Modulo_NegativeDividend_MinusTwo_ModThree_ShouldReturnOne()
        {
            Assert.AreEqual(1, MathHelper.Modulo(-2, 3));
        }

        /// <summary>
        /// Product code: MathHelper.Modulo(int, int) — zero divisor
        /// What: Documents that dividing by zero throws DivideByZeroException
        /// Why: Edge case — callers must handle zero divisor; the method does not guard against it
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public void Modulo_ZeroDivisor_ShouldThrow()
        {
            MathHelper.Modulo(5, 0);
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int)
        /// What: Verifies that 0 has 1 digit
        /// Why: Zero is a special case — Math.Abs(0).ToString() = "0" which has length 1
        /// </summary>
        [TestMethod]
        public void NumberOfDigits_Zero_ShouldReturnOne()
        {
            Assert.AreEqual(1, MathHelper.NumberOfDigits(0));
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int)
        /// What: Verifies single-digit number returns 1
        /// Why: Baseline case for the simplest positive input
        /// </summary>
        [TestMethod]
        public void NumberOfDigits_SingleDigit_ShouldReturnOne()
        {
            Assert.AreEqual(1, MathHelper.NumberOfDigits(5));
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int)
        /// What: Verifies two-digit number returns 2
        /// Why: Tests the transition from single to multi-digit
        /// </summary>
        [TestMethod]
        public void NumberOfDigits_TwoDigits_ShouldReturnTwo()
        {
            Assert.AreEqual(2, MathHelper.NumberOfDigits(42));
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int)
        /// What: Verifies three-digit number (100) returns 3
        /// Why: Tests exact power of 10 boundary (100 vs 99)
        /// </summary>
        [TestMethod]
        public void NumberOfDigits_ThreeDigits_ShouldReturnThree()
        {
            Assert.AreEqual(3, MathHelper.NumberOfDigits(100));
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int) — negative number
        /// What: Verifies that sign is ignored via Math.Abs
        /// Why: Negative sign is not a digit — only magnitude matters
        /// </summary>
        [TestMethod]
        public void NumberOfDigits_NegativeNumber_ShouldIgnoreSign()
        {
            Assert.AreEqual(3, MathHelper.NumberOfDigits(-123));
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int) — int.MaxValue
        /// What: Verifies int.MaxValue (2147483647) returns 10 digits
        /// Why: Upper boundary of int range — ensures no overflow in Math.Abs path
        /// </summary>
        [TestMethod]
        public void NumberOfDigits_MaxValue_ShouldReturnTenDigits()
        {
            Assert.AreEqual(10, MathHelper.NumberOfDigits(int.MaxValue));
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int) — near int.MinValue
        /// What: Verifies int.MinValue + 1 (-2147483647) returns 10 digits
        /// Why: int.MinValue itself overflows Math.Abs — this tests the safe boundary
        /// </summary>
        [TestMethod]
        public void NumberOfDigits_MinValue_ShouldHandleAbsoluteValue()
        {
            Assert.AreEqual(10, MathHelper.NumberOfDigits(int.MinValue + 1));
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int) — int.MinValue
        /// What: Documents that int.MinValue causes OverflowException due to Math.Abs
        /// Why: Edge case — Math.Abs(-2147483648) has no positive int representation
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(OverflowException))]
        public void NumberOfDigits_MinValue_ShouldThrowOverflow()
        {
            MathHelper.NumberOfDigits(int.MinValue);
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int) — powers of 10
        /// What: Verifies digit count at each power-of-10 boundary (1, 10, 100, 1000, 10000)
        /// Why: Powers of 10 are the exact transition points — off-by-one bugs surface here
        /// </summary>
        [TestMethod]
        public void NumberOfDigits_PowerOfTen_ShouldReturnCorrectCount()
        {
            Assert.AreEqual(1, MathHelper.NumberOfDigits(1));
            Assert.AreEqual(2, MathHelper.NumberOfDigits(10));
            Assert.AreEqual(3, MathHelper.NumberOfDigits(100));
            Assert.AreEqual(4, MathHelper.NumberOfDigits(1000));
            Assert.AreEqual(5, MathHelper.NumberOfDigits(10000));
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int) — 9→10 boundary
        /// What: Verifies that 9 returns 1 digit and 10 returns 2 digits
        /// Why: Tests the first single-to-double digit transition
        /// </summary>
        [TestMethod]
        public void NumberOfDigits_BoundaryValues_NineAndTen()
        {
            Assert.AreEqual(1, MathHelper.NumberOfDigits(9));
            Assert.AreEqual(2, MathHelper.NumberOfDigits(10));
        }

        /// <summary>
        /// Product code: MathHelper.NumberOfDigits(int) — 99→100 boundary
        /// What: Verifies that 99 returns 2 digits and 100 returns 3 digits
        /// Why: Tests the double-to-triple digit transition
        /// </summary>
        [TestMethod]
        public void NumberOfDigits_BoundaryValues_NinetyNineAndHundred()
        {
            Assert.AreEqual(2, MathHelper.NumberOfDigits(99));
            Assert.AreEqual(3, MathHelper.NumberOfDigits(100));
        }
    }
}
