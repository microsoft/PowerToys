// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.FancyZones.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_FancyZones
{
    [TestClass]
    public class RunFancyZonesTest : UITestBase
    {
        [TestMethod]
        public void RunFancyZones()
        {
            Thread.Sleep(2000);
            Session = SessionManager.Current;
            Assert.IsNotNull(Session, "Session is null");

            Session?.FindElementByName<Element>("Launch layout editor").Click();
            Thread.Sleep(4000);
            Session = SessionManager.AttachSession(PowerToysModuleWindow.Fancyzone);
        }
    }
}
