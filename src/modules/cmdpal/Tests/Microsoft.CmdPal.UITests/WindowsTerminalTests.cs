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

[TestClass]
public class WindowsTerminalTests : CommandPaletteTestBase
{
    public void EnterWindowsTerminalExtension()
    {
        SetSearchBox("Open Windows Terminal Profiles");

        var searchFileItem = this.Find<NavigationViewItem>("Open Windows Terminal Profiles");
        Assert.AreEqual(searchFileItem.Name, "Open Windows Terminal Profiles");
        searchFileItem.DoubleClick();
    }

    public NavigationViewItem SearchTerminalProfileName(string name)
    {
        EnterWindowsTerminalExtension();
        SetWindowsTerminalExtensionSearchBox(name);
        var item = this.Find<NavigationViewItem>(name);
        Assert.IsNotNull(item, $"{name} profile not found.");

        return item;
    }

    [TestMethod]
    public void OpenCommandPromptTest()
    {
        const string profileName = "Command Prompt";
        var terminalProfileItem = SearchTerminalProfileName(profileName);
        terminalProfileItem.DoubleClick();
        var commandPromptWindow = FindWindowsTerminalWindow();
        Assert.IsNotNull(commandPromptWindow, "Command Prompt window not found.");
    }

    [TestMethod]
    public void OpenCommandPromptByPrimaryButtonTest()
    {
        const string profileName = "Command Prompt";
        var terminalProfileItem = SearchTerminalProfileName(profileName);
        terminalProfileItem.Click();

        var primaryButton = this.Find<Button>("Launch profile");
        Assert.IsNotNull(primaryButton, "Primary button not found.");
        primaryButton.Click();

        var commandPromptWindow = FindWindowsTerminalWindow();
        Assert.IsNotNull(commandPromptWindow, "Command Prompt window not found.");
    }

    /*
    [TestMethod]
    public void OpenCommandPromptBySecondaryButtonTest()
    {
        const string profileName = "Command Prompt";
        var terminalProfileItem = SearchTerminalProfileName(profileName);
        terminalProfileItem.Click();

        var secondaryButton = this.Find<Button>("Launch profile as administrator");
        Assert.IsNotNull(secondaryButton, "Secondary button not found.");
        secondaryButton.Click();

        UACConfirm();

        var commandPromptWindow = FindWindowsTerminalWindow();
        Assert.IsNotNull(commandPromptWindow, "Command Prompt window not found.");
    }*/

    [TestMethod]
    public void OpenDeveloperCommandPromptTest()
    {
        const string profileName = "Developer Command Prompt for VS 2022";
        var terminalProfileItem = SearchTerminalProfileName(profileName);
        terminalProfileItem.DoubleClick();
        var commandPromptWindow = FindWindowsTerminalWindow();
        Assert.IsNotNull(commandPromptWindow, "Command Prompt window not found.");
    }

    [TestMethod]
    public void OpenGitBashTest()
    {
        const string profileName = "Git Bash";
        var terminalProfileItem = SearchTerminalProfileName(profileName);
        terminalProfileItem.DoubleClick();
        var commandPromptWindow = FindWindowsTerminalWindow();
        Assert.IsNotNull(commandPromptWindow, "Command Prompt window not found.");
    }
}
