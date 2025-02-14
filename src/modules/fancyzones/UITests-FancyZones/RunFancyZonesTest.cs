// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            Session.FindElementByName<Button>("Launch layout editor").Click();
            Session.Attach(PowerToysModuleWindow.FancyZone);
            Session.FindElementByName<Button>("Create new layout").Click();
        }
    }
}
