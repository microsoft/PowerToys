// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using ColorPicker.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Helpers
{
    [TestClass]
    public class MonitorResolutionHelperTests
    {
        [TestMethod]
        public void AllMonitors_has_at_least_one_named_monitor()
        {
            Assert.IsTrue(MonitorResolutionHelper.AllMonitors.Any());
            Assert.IsFalse(string.IsNullOrEmpty(MonitorResolutionHelper.AllMonitors.First().Name));
        }

        [TestMethod]
        public void Exactly_one_primary_monitor()
        {
            Assert.AreEqual(1, MonitorResolutionHelper.AllMonitors.Count(m => m.IsPrimary));
        }

        [TestMethod]
        public void Primary_bounds_is_a_positive_windows_foundation_rect()
        {
            var b = MonitorResolutionHelper.AllMonitors.First(m => m.IsPrimary).Bounds;
            Assert.IsInstanceOfType(b, typeof(Windows.Foundation.Rect));
            Assert.IsTrue(b.Width > 0 && b.Height > 0);
        }

        [TestMethod]
        public void HasMultipleMonitors_matches_count()
        {
            Assert.AreEqual(MonitorResolutionHelper.AllMonitors.Count() > 1, MonitorResolutionHelper.HasMultipleMonitors());
        }
    }
}
