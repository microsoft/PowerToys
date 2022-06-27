// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Classes;
using Microsoft.PowerToys.Run.Plugin.TimeZone.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NLog;
using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.TimeZone.UnitTests
{
    [TestClass]
    public class ResultHelperTest
    {
        private TimeZoneList _timeZoneList;
        private TimeZoneSettings _timeZoneSettings;
        private string _actionKeyword;

        [TestInitialize]
        public void SetUp()
        {
            _actionKeyword = "&";
            _timeZoneList = JsonHelper.ReadAllPossibleTimeZones();
            _timeZoneSettings = new TimeZoneSettings
            {
                ShowTimeNames = true,
                ShowTimeZoneNames = true,
            };
        }

        [DataTestMethod]
        [DataRow("&MEST", 1)]
        [DataRow("&GMT", 1)]
        [DataRow("&Germany", 1)] // https://github.com/microsoft/PowerToys/issues/17349
        [DataRow("&AWST", 1)] // https://github.com/microsoft/PowerToys/issues/16695
        [DataRow("&AEDT", 1)] // https://github.com/microsoft/PowerToys/issues/16695
        [DataRow("&AEST", 1)] // https://github.com/microsoft/PowerToys/issues/16695
        public void GetResultsTest(string search, int expectedResultCount)
        {
            var query = new Query(search, _actionKeyword);
            var results = ResultHelper.GetResults(_timeZoneList.TimeZones, _timeZoneSettings, query, string.Empty);

            Assert.AreEqual(expectedResultCount, results.Count());

            foreach (var result in results)
            {
                Assert.AreEqual(!result.Title.Contains("UTC"), _timeZoneSettings.ShowTimeZoneNames);
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.SubTitle));
            }
        }
    }
}
