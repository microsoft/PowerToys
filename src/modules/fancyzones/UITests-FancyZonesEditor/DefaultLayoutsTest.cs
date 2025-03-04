// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using FancyZonesEditorCommon.Data;
using Microsoft.FancyZonesEditor.UITests;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace UITests_FancyZonesEditor
{
    [TestClass]
    public class DefaultLayoutsTest : UITestBase
    {
        public DefaultLayoutsTest()
            : base(PowerToysModule.FancyZone)
        {
            // FancyZonesEditorHelper.InitFancyZonesLayout();
        }

        [TestMethod]
        public void ClickMonitor()
        {
            Assert.IsNotNull(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 1"));
            Assert.IsNotNull(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 2"));

            // verify that the monitor 1 is selected initially
            Assert.IsTrue(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 1").Selected);
            Assert.IsFalse(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 2").Selected);

            Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 2").Click();

            // verify that the monitor 2 is selected after click
            Assert.IsFalse(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 1").Selected);
            Assert.IsTrue(Session.FindByAccessibilityId<Element>("Monitors").Find<Element>("Monitor 2").Selected);
        }
    }
}
