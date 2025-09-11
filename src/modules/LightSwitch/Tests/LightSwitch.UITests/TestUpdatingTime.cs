﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LightSwitch.UITests
{
    [TestClass]
    public class TestUpdatingTime : UITestBase
    {
        public TestUpdatingTime()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
        {
        }

        [TestMethod("LightSwitch.UpdateTime")]
        [TestCategory("Time")]
        public void TestScreenRulerBoundsTool()
        {
            TestHelper.InitializeTest(this, "update time test");
            TestHelper.PerformUpdateTimeTest(this);
            TestHelper.CleanupTest(this);
        }
    }
}
