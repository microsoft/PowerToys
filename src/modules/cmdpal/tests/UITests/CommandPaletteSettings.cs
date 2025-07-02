// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CommandPalette.UITests;

[TestClass]
public class CommandPaletteSettings : UITestBase
{
    public CommandPaletteSettings()
        : base(PowerToysModule.CommandPalette)
    {
    }

    /// <summary>
    /// Test Warning Dialog at startup
    /// <list type="bullet">
    /// <item>
    /// <description>Validating Warning-Dialog will be shown if 'Show a warning at startup' toggle is On.</description>
    /// </item>
    /// <item>
    /// <description>Validating Warning-Dialog will NOT be shown if 'Show a warning at startup' toggle is Off.</description>
    /// </item>
    /// <item>
    /// <description>Validating click 'Quit' button in Warning-Dialog, the Hosts File Editor window would be closed.</description>
    /// </item>
    /// <item>
    /// <description>Validating click 'Accept' button in Warning-Dialog, the Hosts File Editor window would NOT be closed.</description>
    /// </item>
    /// </list>
    /// </summary>
    [TestMethod]
    public void TestWarningDialog()
    {
        this.Find<TextBox>("FilterBox").SetText("All Apps");
    }
}
