// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests.Next;

[TestClass]
public class TestBounds : UITestBase
{
    public TestBounds()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Large, enableModules: new[] { TestHelper.ModuleSettingsKey })
    {
    }

    [TestMethod]
    [TestCategory("Spacing")]
    public void TestScreenRulerBoundsTool()
    {
        TestHelper.InitializeTest(this, "bounds test");
        try
        {
            TestHelper.PerformBoundsToolTest(this);
        }
        finally
        {
            TestHelper.CleanupTest(this);
        }
    }

    [TestMethod]
    [TestCategory("Activation")]
    public void TestScreenRulerToolbarTargetsOriginalWindowWithMultipleProcesses()
    {
        var activationKeys = TestHelper.InitializeTest(this, "multiple ruler processes");
        try
        {
            var ruler = TestHelper.ActivateScreenRuler(this, activationKeys, "multiple ruler processes");
            using var original = Process.GetProcessById(ruler.ProcessId);
            var module = original.MainModule;
            Assert.IsNotNull(module, "The activated ruler should expose its executable path.");

            // Reproduce overlapping cold-start launches without depending on CI startup timing.
            using var additional = Process.Start(new ProcessStartInfo(module.FileName) { UseShellExecute = false });
            Assert.IsNotNull(additional, "The additional ruler process should start.");
            var additionalWindow = WindowsFinder.WaitForWindowByApp(
                additional.Id.ToString(CultureInfo.InvariantCulture),
                window => window.Title == TestHelper.ScreenRulerWindowTitle,
                timeoutMS: 20000);
            Assert.IsNotNull(additionalWindow, "Both ruler toolbars must be visible before testing the original session.");

            // The toolbars overlap; test UIA target ownership, not foreground-dependent mouse routing.
            ruler.Find<Element>(By.AccessibilityId(TestHelper.CloseButtonId), 8000).Invoke(msPostAction: 0);
            Assert.IsTrue(original.WaitForExit(10000), "Close should act on the original toolbar.");
            Assert.IsFalse(additional.HasExited, "Closing the original toolbar must not close the other ruler.");
            Assert.IsTrue(
                WindowsFinder.ListByApp(additional.Id.ToString(CultureInfo.InvariantCulture))
                    .Any(window => window.Hwnd == additionalWindow.WindowHandle),
                "The additional toolbar should remain visible.");
        }
        finally
        {
            TestHelper.CleanupTest(this);
        }
    }
}
