// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.PowerToys.Run.Plugin.TimeDate.Components;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Run.Plugin.TimeDate.UnitTests
{
    [TestClass]
    public class StringParserTests
    {
        private CultureInfo originalCulture;
        private CultureInfo originalUiCulture;

        [TestInitialize]
        public void Setup()
        {
            // Set culture to 'en-us'
            originalCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("en-us", false);
            originalUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = new CultureInfo("en-us", false);
        }

        [DataTestMethod]
        [DataRow("10/29/2022 17:05:10", true, "G", "10/29/2022 5:05:10 PM")]
        [DataRow("Saturday, October 29, 2022 5:05:10 PM", true, "G", "10/29/2022 5:05:10 PM")]
        [DataRow("10/29/2022", true, "d", "10/29/2022")]
        [DataRow("Saturday, October 29, 2022", true, "d", "10/29/2022")]
        [DataRow("17:05:10", true, "T", "5:05:10 PM")]
        [DataRow("5:05:10 PM", true, "T", "5:05:10 PM")]
        [DataRow("10456", false, "", "")]
        [DataRow("u10456", true, "", "")] // Value is UTC and can be different based on system
        [DataRow("u-10456", true, "", "")] // Value is UTC and can be different based on system
        [DataRow("u+10456", true, "", "")] // Value is UTC and can be different based on system
        [DataRow("ums10456", true, "", "")] // Value is UTC and can be different based on system
        [DataRow("ums-10456", true, "", "")] // Value is UTC and can be different based on system
        [DataRow("ums+10456", true, "", "")] // Value is UTC and can be different based on system
        [DataRow("ft10456", true, "", "")] // Value is UTC and can be different based on system
        public void ConvertStringToDateTime(string typedString, bool expectedBool, string stringType, string expectedString)
        {
            // Act
            bool boolResult = TimeAndDateHelper.ParseStringAsDateTime(in typedString, out DateTime result);

            // Assert
            Assert.AreEqual(expectedBool, boolResult);
            if (!string.IsNullOrEmpty(expectedString))
            {
                Assert.AreEqual(expectedString, result.ToString(stringType, CultureInfo.CurrentCulture));
            }
        }

        [TestCleanup]
        public void CleanUp()
        {
            // Set culture to original value
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
