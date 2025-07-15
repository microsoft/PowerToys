// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class BasicLaunchTests : UITestBase
{
    public BasicLaunchTests()
        : base(PowerToysModule.CommandPalette)
    {
    }

    [TestMethod]
    public void TestWarningDialog()
    {
        this.Find<TextBox>("FilterBox").SetText("All Apps");
    }
}
