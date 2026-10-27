// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.GrabAndMove.UITests;

[TestClass]
[DoNotParallelize]
public sealed class TransportSmokeTests : IDisposable
{
    private TestWindow? fixture;
    private Point originalPointer;
    private UmdfHidInput? input;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("UmdfHidSmoke")]
    public void HidEventsAreNotMarkedInjected()
    {
        Assert.IsFalse(ElevationHelper.IsCurrentProcessElevated(), "The smoke probe must run non-elevated.");
        using var process = Process.GetCurrentProcess();
        TestContext.WriteLine($"OS architecture={RuntimeInformation.OSArchitecture}; process architecture={RuntimeInformation.ProcessArchitecture}; session={process.SessionId}.");
        var (x, y) = MouseHelper.GetMousePosition();
        originalPointer = new Point(x, y);
        KeyboardHelper.SendKey(Key.Esc);
        fixture = new TestWindow();
        fixture.Focus();
        using var trace = new InputTrace();
        input = UmdfHidInput.Activate();
        var bounds = fixture.Bounds;
        var target = new Point(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2));
        MouseHelper.MoveTo(target.X, target.Y);
        MouseHelper.LeftDown();
        MouseHelper.LeftUp();
        KeyboardHelper.PressKey(Key.LShift);
        KeyboardHelper.ReleaseKey(Key.LShift);
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => trace.Samples,
                items => items is not null && items.Count(item => item.Kind == "keyboard") >= 2 &&
                    items.Count(item => item.Kind == "mouse" && item.Message is 0x201 or 0x202) >= 2,
                5_000).Succeeded,
            "The HID transport did not produce both keyboard and mouse button hook events.");
        var hid = trace.Samples;
        TestContext.WriteLine($"Real HID device: {input.DevicePath}");
        TestContext.WriteLine($"HID events: {JsonSerializer.Serialize(hid)}");
        Assert.IsTrue(hid.Where(item => item.Kind == "keyboard").All(item => (item.Flags & 0x10) == 0), "HID keyboard input was marked injected.");
        Assert.IsTrue(hid.Where(item => item.Kind == "mouse").All(item => (item.Flags & 1) == 0), "HID mouse input was marked injected.");
        var cursor = WaitHelper.WaitForStable(
            MouseHelper.GetMousePosition,
            actual => actual == (target.X, target.Y),
            5_000,
            requiredConsecutiveMatches: 3);
        Assert.IsTrue(cursor.Succeeded, $"Absolute HID positioning was not pixel exact: {cursor.LastObservation}.");

        input.Dispose();
        input = null;
        trace.Clear();
        KeyboardHelper.PressKey(Key.LShift);
        KeyboardHelper.ReleaseKey(Key.LShift);
        MouseHelper.LeftDown();
        MouseHelper.LeftUp();
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                () => trace.Samples,
                items => items is not null && items.Any(item => item.Kind == "keyboard" && (item.Flags & 0x10) != 0) &&
                    items.Any(item => item.Kind == "mouse" && (item.Flags & 1) != 0),
                5_000).Succeeded,
            "The positive control failed to detect injected flags from the ordinary helpers.");
        var injected = trace.Samples;
        TestContext.WriteLine($"Injected control: {JsonSerializer.Serialize(injected)}");
        var file = Path.Combine(TestContext.TestRunResultsDirectory!, "umdf-input-flags.json");
        File.WriteAllText(file, JsonSerializer.Serialize(new { Hid = hid, Injected = injected }));
        TestContext.AddResultFile(file);
    }

    [TestCleanup]
    public void CleanupTransport()
    {
        if (TestContext.CurrentTestOutcome != UnitTestOutcome.Passed)
        {
            var screen = System.Windows.Forms.SystemInformation.PrimaryMonitorSize;
            using var image = new Bitmap(screen.Width, screen.Height);
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.CopyFromScreen(Point.Empty, Point.Empty, screen);
            }

            var screenshot = Path.Combine(TestContext.TestRunResultsDirectory!, "umdf-smoke-failure.png");
            image.Save(screenshot, System.Drawing.Imaging.ImageFormat.Png);
            TestContext.AddResultFile(screenshot);
        }

        try
        {
            KeyboardHelper.ReleaseKey(Key.LShift);
            MouseHelper.LeftUp();
            if (fixture is not null)
            {
                MouseHelper.MoveTo(originalPointer.X, originalPointer.Y);
            }
        }
        finally
        {
            try
            {
                input?.Dispose();
                input = null;
            }
            finally
            {
                Dispose();
            }
        }
    }

    public void Dispose()
    {
        fixture?.Dispose();
        fixture = null;
        GC.SuppressFinalize(this);
    }
}
