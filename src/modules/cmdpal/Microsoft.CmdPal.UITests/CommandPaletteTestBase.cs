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

namespace Microsoft.CmdPal.UITests;

public class CommandPaletteTestBase : UITestBase
{
    public CommandPaletteTestBase()
        : base(PowerToysModule.CommandPalette)
    {
    }

    protected void SetSearchBox(string text)
    {
        Assert.AreEqual(this.Find<TextBox>("Type here to search...").SetText(text, true).Text, text);
    }

    protected void SetFilesExtensionSearchBox(string text)
    {
        Assert.AreEqual(this.Find<TextBox>("Search for files and folders...").SetText(text, true).Text, text);
    }

    protected void SetCalculatorExtensionSearchBox(string text)
    {
        Assert.AreEqual(this.Find<TextBox>("Type an equation...").SetText(text, true).Text, text);
    }

    protected void SetTimeAndDaterExtensionSearchBox(string text)
    {
        Assert.AreEqual(this.Find<TextBox>("Search values or type a custom time stamp...").SetText(text, true).Text, text);
    }

    protected void OpenContextMenu()
    {
        var contextMenuButton = this.Find<Button>("More");
        Assert.IsNotNull(contextMenuButton, "Context menu button not found.");
        contextMenuButton.Click();
    }
}
