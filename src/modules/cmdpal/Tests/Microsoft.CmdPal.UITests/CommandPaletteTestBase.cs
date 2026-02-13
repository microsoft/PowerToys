// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

public class CommandPaletteTestBase : UITestBase
{
    public CommandPaletteTestBase()
        : base(PowerToysModule.CommandPalette)
    {
    }

    protected void SetSearchBox(string text) => SetSearchBoxText(text);

    protected void SetFilesExtensionSearchBox(string text) => SetSearchBoxText(text);

    protected void SetCalculatorExtensionSearchBox(string text) => SetSearchBoxText(text);

    protected void SetTimeAndDaterExtensionSearchBox(string text) => SetSearchBoxText(text);

    private void SetSearchBoxText(string text)
    {
        Assert.AreEqual(this.Find<TextBox>(By.AccessibilityId("MainSearchBox")).SetText(text, true).Text, text);
    }

    protected void OpenContextMenu()
    {
        var contextMenuButton = this.Find<Button>(By.AccessibilityId("MoreContextMenuButton"));
        Assert.IsNotNull(contextMenuButton, "Context menu button not found.");
        contextMenuButton.Click();
    }

    protected void FindDefaultAppDialogAndClickButton()
    {
        try
        {
            // win11
            var chooseDialog = FindByClassName("NamedContainerAutomationPeer", global: true);

            chooseDialog.Find<Button>("Just once").Click();
        }
        catch
        {
            try
            {
                // win10
                var chooseDialog = FindByClassName("Shell_Flyout", global: true);
                chooseDialog.Find<Button>("OK").Click();
            }
            catch
            {
            }
        }
    }
}
