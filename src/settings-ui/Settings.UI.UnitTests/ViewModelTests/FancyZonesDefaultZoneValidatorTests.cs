// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class FancyZonesDefaultZoneValidatorTests
    {
        [TestMethod]
        [DataRow("1", 4, new int[] { 0 })]
        [DataRow("4", 4, new int[] { 3 })]
        [DataRow("2-3", 4, new int[] { 1, 2 })]
        [DataRow(" 2 - 3 ", 4, new int[] { 1, 2 })]
        [DataRow("1-1", 4, new int[] { 0 })]
        public void SingleAndRangeValuesParseToZeroBasedIndexes(string text, int zoneCount, int[] expected)
        {
            var result = FancyZonesDefaultZoneValidator.TryParse(text, zoneCount, out List<int> zoneIndexSet);

            Assert.AreEqual(FancyZonesDefaultZoneValidator.ValidationResult.Valid, result);
            CollectionAssert.AreEqual(expected, zoneIndexSet);
        }

        [TestMethod]
        public void EmptyValueIsReportedAsEmptyNotAnError()
        {
            var result = FancyZonesDefaultZoneValidator.TryParse(string.Empty, 4, out List<int> zoneIndexSet);

            Assert.AreEqual(FancyZonesDefaultZoneValidator.ValidationResult.Empty, result);
            Assert.IsNull(zoneIndexSet);
        }

        [TestMethod]
        [DataRow("4-2")]
        [DataRow("1-0")]
        public void ReversedRangeIsInvalid(string text)
        {
            var result = FancyZonesDefaultZoneValidator.TryParse(text, 4, out List<int> zoneIndexSet);

            Assert.AreEqual(FancyZonesDefaultZoneValidator.ValidationResult.ReversedRange, result);
            Assert.IsNull(zoneIndexSet);
        }

        [TestMethod]
        [DataRow("0", 4)]
        [DataRow("5", 4)]
        [DataRow("1-5", 4)]
        [DataRow("-1", 4)]
        public void OutOfRangeValueIsInvalid(string text, int zoneCount)
        {
            var result = FancyZonesDefaultZoneValidator.TryParse(text, zoneCount, out List<int> zoneIndexSet);

            Assert.AreEqual(FancyZonesDefaultZoneValidator.ValidationResult.OutOfRange, result);
            Assert.IsNull(zoneIndexSet);
        }

        [TestMethod]
        [DataRow("abc")]
        [DataRow("1-abc")]
        [DataRow("abc-3")]
        public void NonNumericValueIsInvalid(string text)
        {
            var result = FancyZonesDefaultZoneValidator.TryParse(text, 4, out List<int> zoneIndexSet);

            Assert.AreEqual(FancyZonesDefaultZoneValidator.ValidationResult.NotANumber, result);
            Assert.IsNull(zoneIndexSet);
        }
    }
}
