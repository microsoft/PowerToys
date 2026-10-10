// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.MouseWithoutBorders.UITests;

[TestClass]
public sealed class ReceiverFocusTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("MwbInfrastructure")]
    public void ReceiverRegainsFocusFromStartMenuWithoutMouseInput()
    {
        var directory = Path.Combine(RunFiles.PersistentResultsRoot(TestContext.TestRunDirectory), "mwb-focus-" + Guid.NewGuid().ToString("N"));
        if (IsStartMenuForeground())
        {
            KeyboardHelper.SendKeys(Key.Esc);
            RunFiles.Wait(() => !IsStartMenuForeground(), TimeSpan.FromSeconds(5), "Existing Start menu did not close before the deliberate precondition.");
        }

        using var receiver = new ReceiverController("Focus probe", Guid.NewGuid().ToString());
        receiver.FocusInput();
        Assert.AreEqual(receiver.Handle, NativeSupport.GetForegroundWindow(), "Open Start only from the owned receiver.");
        using var recording = new TestRecordings(TestContext, directory);
        recording.StartDesktop();
        try
        {
            KeyboardHelper.SendKeys(Key.LWin);
            RunFiles.Wait(IsStartMenuForeground, TimeSpan.FromSeconds(10), "The deliberate Start-menu precondition did not appear.");
            Thread.Sleep(500);
            var foreground = WindowControl.GetForegroundWindowInfo();
            var before = Path.Combine(directory, "start-menu-before.json");
            RunFiles.Write(before, new { Hwnd = foreground.Hwnd.ToInt64(), foreground.ProcessId, foreground.ProcessName, foreground.ClassName, Path = NativeSupport.ImagePath(foreground.ProcessId) });
            TestContext.AddResultFile(before);
            var watch = Stopwatch.StartNew();
            RunFiles.Wait(
                () => NativeSupport.GetForegroundWindow() == receiver.Handle && receiver.InputFocused,
                TimeSpan.FromSeconds(10),
                "An open Start menu prevented the owned receiver from regaining foreground.",
                receiver.FocusInput);
            KeyboardHelper.SendKeys(Key.A);
            RunFiles.Wait(() => receiver.ReceivedText == "a", TimeSpan.FromSeconds(5), "Physical keyboard input did not reach the receiver after Start closed.");
            Assert.AreEqual(0, receiver.Clicks, "Focus recovery must not inject a mouse click.");
            Assert.AreEqual(0, receiver.MouseDownMessages);
            Assert.AreEqual(0, receiver.MouseUpMessages);
            Assert.AreEqual(1, receiver.StartMenuDismissals, "The deliberate Start menu must be dismissed exactly once.");
            var after = Path.Combine(directory, "receiver-after.json");
            RunFiles.Write(after, new { ForegroundHwnd = NativeSupport.GetForegroundWindow().ToInt64(), receiver.InputFocused, receiver.ReceivedText, receiver.Clicks, receiver.StartMenuDismissals, ElapsedMilliseconds = watch.ElapsedMilliseconds });
            TestContext.AddResultFile(after);
        }
        finally
        {
            recording.Complete();
            if (IsStartMenuForeground())
            {
                KeyboardHelper.SendKeys(Key.Esc);
            }
        }
    }

    [TestMethod]
    [TestCategory("MwbInfrastructure")]
    public void StartMenuDismissalDoesNotActOnAnOrdinaryForegroundWindow()
    {
        using var receiver = new ReceiverController("Not a shell launcher", Guid.NewGuid().ToString());
        receiver.FocusInput();
        Assert.AreEqual(receiver.Handle, NativeSupport.GetForegroundWindow());
        Assert.IsFalse(NativeSupport.IsStartMenuWindow(receiver.Handle));
        var escapeSent = false;
        Assert.IsFalse(NativeSupport.DismissForegroundStartMenu(() => escapeSent = true));
        Assert.IsFalse(escapeSent, "Unknown windows must not receive any dismissal input.");
        Assert.AreEqual(receiver.Handle, NativeSupport.GetForegroundWindow());
        Assert.AreEqual(string.Empty, receiver.ReceivedText);
        Assert.AreEqual(0, receiver.Clicks);
    }

    [TestMethod]
    [TestCategory("MwbInfrastructure")]
    public void StartMenuIdentityRequiresExactOsPathClassAndInteractiveSession()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        const string className = "Windows.UI.Core.CoreWindow";
        foreach (var relative in new[]
        {
            @"SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartMenuExperienceHost.exe",
            @"SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe",
            @"SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy\SearchApp.exe",
        })
        {
            var path = Path.Combine(windows, relative);
            Assert.IsTrue(NativeSupport.IsStartMenuIdentity(className, path, 1, 1));
            Assert.IsTrue(NativeSupport.IsStartMenuIdentity(className, path.ToUpperInvariant(), 1, 1));
            Assert.IsFalse(NativeSupport.IsStartMenuIdentity("OtherWindow", path, 1, 1));
            Assert.IsFalse(NativeSupport.IsStartMenuIdentity(className, path, 2, 1));
            Assert.IsFalse(NativeSupport.IsStartMenuIdentity(className, path, 0, 0));
            Assert.IsFalse(NativeSupport.IsStartMenuIdentity(className, Path.GetFileName(path), 1, 1));
            Assert.IsFalse(NativeSupport.IsStartMenuIdentity(className, Path.Combine(Path.GetTempPath(), Path.GetFileName(path)), 1, 1));
            Assert.IsFalse(NativeSupport.IsStartMenuIdentity(className, path + ".untrusted.exe", 1, 1));
        }
    }

    private static bool IsStartMenuForeground() => NativeSupport.IsStartMenuWindow(NativeSupport.GetForegroundWindow());
}
