// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ColorPicker.UITests.Next;

/// <summary>
/// Real ColorPicker module test: assert that toggling the dashboard switch actually causes the
/// PowerToys runner to spawn / terminate the ColorPickerUI process. This exercises:
///   Settings UI (ToggleSwitch) → settings.json write → runner module-state poll →
///   ColorPicker.dll enable()/disable() → ColorPickerUI process lifecycle.
/// </summary>
/// <remarks>
/// <para>
/// We deliberately do NOT try to trigger Win+Shift+C from this test. The ColorPicker
/// activation goes through the runner's <c>WH_KEYBOARD_LL</c> hook + a named-event signal
/// (<c>m_hInvokeEvent</c> in <c>ColorPicker/dllmain.cpp</c>) and synthetic keystrokes from
/// <c>keybd_event</c>/<c>SendInput</c> are unreliable through this chain (focus, virtual
/// desktops, runner-process-vs-test-process associations all interfere). The existing
/// repo harness has the same limitation — that's why <c>ColorPickerUITest.cs</c> ships
/// today as an empty stub.
/// </para>
/// <para>
/// Plus, the picker overlay itself (<c>MainWindow.xaml</c>) is
/// <c>AllowsTransparency=True, Opacity=0.1, ShowInTaskbar=False</c> — UIA correctly hides it,
/// so even if the hotkey fired there would be nothing to assert against. Proper picker
/// testing would need either an in-process signal-event hook (a new ColorPicker test API) or
/// hardware-level HID injection (out of scope for a UI test harness).
/// </para>
/// </remarks>
[TestClass]
public class ColorPickerModuleLifecycleTests : UITestBase
{
    public ColorPickerModuleLifecycleTests()
        : base(PowerToysModule.PowerToysSettings)
    {
    }

    [TestMethod]
    [TestCategory("ColorPicker")]
    [TestCategory("winappcli-POC")]
    public void TogglingModuleStartsAndStopsColorPickerProcess()
    {
        var toggle = Find<ToggleSwitch>("Color Picker");
        var initial = toggle.IsOn;

        try
        {
            // Force module OFF and assert the process actually exits.
            if (toggle.IsOn)
            {
                toggle.Toggle(false);
                Assert.IsTrue(
                    WaitForProcess("PowerToys.ColorPickerUI", expected: false, timeoutMS: 10_000),
                    "PowerToys.ColorPickerUI did not exit within 10s after toggling module OFF.");
            }

            // Force module ON and assert the runner spawns the process.
            toggle.Toggle(true);
            Assert.IsTrue(
                WaitForProcess("PowerToys.ColorPickerUI", expected: true, timeoutMS: 10_000),
                "PowerToys.ColorPickerUI did not start within 10s after toggling module ON.");

            // Cycle once more to prove the lifecycle is repeatable.
            toggle.Toggle(false);
            Assert.IsTrue(
                WaitForProcess("PowerToys.ColorPickerUI", expected: false, timeoutMS: 10_000),
                "Second OFF cycle: PowerToys.ColorPickerUI did not exit.");

            toggle.Toggle(true);
            Assert.IsTrue(
                WaitForProcess("PowerToys.ColorPickerUI", expected: true, timeoutMS: 10_000),
                "Second ON cycle: PowerToys.ColorPickerUI did not start.");
        }
        finally
        {
            // Restore initial state regardless of pass/fail.
            toggle.Toggle(initial);
        }
    }

    private static bool WaitForProcess(string name, bool expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            var running = Process.GetProcessesByName(name).Length > 0;
            if (running == expected)
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }
}
