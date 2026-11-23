// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

public abstract class KeyboardManagerTestBase : UITestBase
{
    private static readonly string[] EnabledModules = { KeyboardManagerTestConstants.ModuleName };
    private static readonly Key[] KeysToRelease =
    {
        Key.A,
        Key.B,
        Key.C,
        Key.D,
        Key.E,
        Key.F,
        Key.Q,
        Key.U,
        Key.V,
        Key.W,
        Key.F4,
        Key.F11,
        Key.F12,
        (Key)0x7C,
        (Key)0x7D,
        (Key)0x7E,
        (Key)0x7F,
        (Key)0x80,
        (Key)0x81,
        (Key)0x82,
        (Key)0x83,
        (Key)0x84,
        (Key)0x85,
        (Key)0x86,
        (Key)0x87,
        Key.Tab,
        (Key)0xA2,
        (Key)0xA4,
        Key.Ctrl,
        Key.Alt,
        Key.LWin,
        Key.Shift,
        Key.LShift,
    };

    protected KeyboardManagerTestBase()
        : base(PowerToysModule.PowerToysSettings, enableModules: EnabledModules)
    {
    }

    protected void Step(string message) =>
        TestContext.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");

    protected Session NavigateToKeyboardManagerSettings()
    {
        Step("Binding to PowerToys Settings");
        var settings = Session.FromProcess(
            "PowerToys.Settings",
            PowerToysModule.PowerToysSettings,
            timeoutMS: 15_000);

        if (!settings.Has(By.AccessibilityId("KeyboardManagerNavItem"), timeoutMS: 500))
        {
            Step("Expanding the Input / Output navigation group");
            settings.Find<NavigationViewItem>(By.AccessibilityId("InputOutputNavItem"), timeoutMS: 10_000)
                .Click(msPostAction: 500);
        }

        Step("Opening Keyboard Manager settings");
        settings.Find<NavigationViewItem>(By.AccessibilityId("KeyboardManagerNavItem"), timeoutMS: 10_000)
            .Click(msPostAction: 500);
        Assert.IsTrue(
            settings.WaitFor(
                () => settings.Has(By.AccessibilityId("KeyboardManagerLaunchEditorButton"), timeoutMS: 500) ||
                    FindExact<Button>(settings, "Open editor", timeoutMS: 500) is not null,
                timeoutMS: 15_000,
                pollIntervalMS: 250),
            "Keyboard Manager settings did not expose the unified editor launch button.");

        var enabledToggle = settings.Find<ToggleSwitch>(By.Name(KeyboardManagerTestConstants.ModuleName), timeoutMS: 10_000);
        Assert.IsTrue(enabledToggle.IsOn, "Keyboard Manager did not start from the deterministic enabled baseline.");
        return settings;
    }

    protected Session OpenEditor()
    {
        Assert.IsTrue(CloseEditor(), "A stale Keyboard Manager editor process remained before launch.");
        var settings = NavigateToKeyboardManagerSettings();

        Step("Launching the unified Keyboard Manager editor");
        var launchButton = settings.Has(By.AccessibilityId("KeyboardManagerLaunchEditorButton"), timeoutMS: 500)
            ? settings.Find<Button>(By.AccessibilityId("KeyboardManagerLaunchEditorButton"), timeoutMS: 5_000)
            : FindExact<Button>(settings, "Open editor", timeoutMS: 5_000);
        Assert.IsNotNull(launchButton, "The unified editor launch button could not be addressed.");
        launchButton!.Click(msPostAction: 300);

        var editor = WindowsFinder.WaitForWindowByApp(
            KeyboardManagerTestConstants.EditorProcessName,
            window => window.Width > 0 && window.Height > 0,
            timeoutMS: 20_000);
        Assert.IsNotNull(editor, "The unified Keyboard Manager editor window did not open.");
        Assert.IsTrue(
            editor!.WaitForElement(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 15_000),
            "The Keyboard Manager editor did not expose its Add new remapping button.");

        editor.EnsureForeground();
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(editor.WindowHandle), timeoutMS: 10_000, requiredConsecutiveMatches: 2),
            $"The Keyboard Manager editor did not own foreground input. Current foreground: {WindowControl.GetForegroundWindowInfo()}.");
        return editor;
    }

    protected static T? FindExact<T>(Session session, string name, int timeoutMS = 2_000)
        where T : Element, new() =>
        session.FindAll<T>(By.Name(name), timeoutMS)
            .FirstOrDefault(element => element.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    internal static bool CloseEditor()
    {
        WindowControl.TryCloseByApp(KeyboardManagerTestConstants.EditorProcessName, timeoutMS: 5_000);
        WindowControl.TryKillProcessTreeByNameAndWait(KeyboardManagerTestConstants.EditorProcessName, timeoutMS: 5_000);

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var processes = GetEditorProcesses();
            if (processes.Count == 0)
            {
                return true;
            }

            try
            {
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }
                }
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }

            Thread.Sleep(200);
        }

        var remainingProcesses = GetEditorProcesses();
        try
        {
            return remainingProcesses.Count == 0;
        }
        finally
        {
            foreach (var process in remainingProcesses)
            {
                process.Dispose();
            }
        }
    }

    private static IReadOnlyList<Process> GetEditorProcesses()
    {
        var matches = new List<Process>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.ProcessName.Equals(KeyboardManagerTestConstants.EditorProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(process);
                    continue;
                }
            }
            catch
            {
            }

            process.Dispose();
        }

        return matches;
    }

    protected async Task CleanupKeyboardManagerTestAsync()
    {
        await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
        bool editorClosed = CloseEditor();
        if (editorClosed)
        {
            KeyboardManagerSettings.ResetToEmptyProfile();
        }

        foreach (var key in KeysToRelease)
        {
            KeyboardHelper.ReleaseKey(key);
        }

        KeyboardHelper.SendKey(Key.Esc);
        Assert.IsTrue(editorClosed, "The Keyboard Manager editor process survived test cleanup; settings were not reset to avoid a write race.");
    }
}
