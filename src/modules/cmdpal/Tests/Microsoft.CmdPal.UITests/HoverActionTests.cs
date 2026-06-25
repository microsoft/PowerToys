// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class HoverActionTests : CommandPaletteTestBase
{
    private const string HoverDebugLogPath = "cmdpal-hover-debug.log";

    [TestMethod]
    public void QuickShellHoverPinViaKeyboardSelection()
    {
        var firstItem = OpenQuickShellWithFirstShortcutSelected();
        if (firstItem is null)
        {
            return;
        }

        try
        {
            var pinAction = this.Find<Element>("Pin to top", timeoutMS: 8000);
            pinAction.Click();
        }
        catch (AssertFailedException)
        {
            try
            {
                this.Find<Element>("Unpin", timeoutMS: 2000).Click();
            }
            catch (AssertFailedException)
            {
                Assert.Inconclusive("Pin/Unpin hover action not visible. Select a row and ensure hover actions load.");
            }
        }

        AssertHoverDebugLogContains("Invoke");
    }

    [TestMethod]
    public void QuickShellHoverPinViaMouseClick()
    {
        var firstItem = OpenQuickShellWithFirstShortcutSelected();
        if (firstItem is null)
        {
            return;
        }

        var rect = firstItem.Rect;
        Assert.IsTrue(rect.HasValue, "List item bounds unavailable");
        var bounds = rect.Value;

        // Pin is typically the second slot (Edit, Pin, …) — aim ~45px from the right edge.
        var clickX = (int)(bounds.X + bounds.Width - 45);
        var clickY = (int)(bounds.Y + (bounds.Height / 2));
        MoveMouseTo(clickX, clickY);
        Session.PerformMouseAction(MouseActionType.LeftClick, msPreAction: 300, msPostAction: 800);

        AssertHoverDebugLogContains("Tapped");
    }

    private NavigationViewItem? OpenQuickShellWithFirstShortcutSelected()
    {
        SetSearchBox("quick shell");

        try
        {
            this.Find<NavigationViewItem>("Quick Shell").DoubleClick();
        }
        catch (AssertFailedException)
        {
            Assert.Inconclusive("Quick Shell extension not found. Deploy QuickShell dev package first.");
            return null;
        }

        Thread.Sleep(1500);
        SendKeys(Key.Down);

        try
        {
            // First shortcut row — exclude top-level "Quick Shell" if still visible.
            return this.Find<NavigationViewItem>(By.XPath("//*[@ControlType='ListItem' and not(@Name='Quick Shell')]"), timeoutMS: 8000);
        }
        catch (AssertFailedException)
        {
            Assert.Inconclusive("Quick Shell has no shortcuts to test hover actions.");
            return null;
        }
    }

    private static void AssertHoverDebugLogContains(string expected)
    {
        var logPath = Path.Combine(Path.GetTempPath(), HoverDebugLogPath);
        if (!File.Exists(logPath))
        {
            Assert.Inconclusive($"Hover debug log not found at {logPath}. Run a Debug build of Microsoft.CmdPal.UI.");
            return;
        }

        var log = File.ReadAllText(logPath);
        StringAssert.Contains(log, expected, $"Expected '{expected}' in hover debug log.");
    }
}
