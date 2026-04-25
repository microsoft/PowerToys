// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class BasicTests : CommandPaletteTestBase
{
    public BasicTests()
    {
    }

    [TestMethod]
    public void BasicFileSearchTest()
    {
        SetSearchBox("files");

        var searchFileItem = this.Find<NavigationViewItem>("Search files");
        Assert.AreEqual("Search files", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetFilesExtensionSearchBox("AppData");

        Assert.IsNotNull(this.Find<NavigationViewItem>("AppData"));
    }

    [TestMethod]
    public void BasicCalculatorTest()
    {
        SetSearchBox("calculator");

        var searchFileItem = this.Find<NavigationViewItem>("Calculator");
        Assert.AreEqual("Calculator", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetCalculatorExtensionSearchBox("1+2");

        Assert.IsNotNull(this.Find<NavigationViewItem>("3"));
    }

    [TestMethod]
    public void BasicTimeAndDateTest()
    {
        SetSearchBox("time and date");

        var searchFileItem = this.Find<NavigationViewItem>("Time and date");
        Assert.AreEqual("Time and date", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetTimeAndDaterExtensionSearchBox("year");

        Assert.IsNotNull(this.Find<NavigationViewItem>("2026"));
    }

    [TestMethod]
    public void BasicWindowsTerminalTest()
    {
        SetSearchBox("Windows Terminal");

        var searchFileItem = this.Find<NavigationViewItem>("Open Windows Terminal profiles");
        Assert.AreEqual("Open Windows Terminal profiles", searchFileItem.Name);
        searchFileItem.DoubleClick();

        // SetSearchBox("PowerShell");
        // Assert.IsNotNull(this.Find<NavigationViewItem>("PowerShell"));
    }

    [TestMethod]
    public void BasicWindowsSettingsTest()
    {
        SetSearchBox("Windows settings");

        var searchFileItem = this.Find<NavigationViewItem>("Windows settings");
        Assert.AreEqual("Windows settings", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetSearchBox("power");

        Assert.IsNotNull(this.Find<NavigationViewItem>("Power and sleep"));
    }

    [TestMethod]
    public void BasicRegistryTest()
    {
        SetSearchBox("Registry");

        var searchFileItem = this.Find<NavigationViewItem>("Registry");
        Assert.AreEqual("Registry", searchFileItem.Name);
        searchFileItem.DoubleClick();

        // Type the string will cause strange behavior.so comment it out for now.
        // SetSearchBox(@"HKEY_LOCAL_MACHINE");
        // Assert.IsNotNull(this.Find<NavigationViewItem>(@"HKEY_LOCAL_MACHINE\SECURITY"));
    }

    [TestMethod]
    public void BasicWindowsServicesTest()
    {
        SetSearchBox("Windows Services");

        var searchFileItem = this.Find<NavigationViewItem>("Windows Services");
        Assert.AreEqual("Windows Services", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetSearchBox("hyper-v");

        Assert.IsNotNull(this.Find<NavigationViewItem>("Hyper-V Heartbeat Service"));
    }

    [TestMethod]
    public void BasicWindowsSystemCommandsTest()
    {
        SetSearchBox("Windows System Commands");

        var searchFileItem = this.Find<NavigationViewItem>("Windows System Commands");
        Assert.AreEqual("Windows System Commands", searchFileItem.Name);
        searchFileItem.DoubleClick();

        SetSearchBox("Sleep");

        Assert.IsNotNull(this.Find<NavigationViewItem>("Put computer to sleep"));
    }
}
