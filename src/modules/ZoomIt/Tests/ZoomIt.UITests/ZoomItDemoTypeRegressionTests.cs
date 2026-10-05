// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.ZoomIt.UITests;

public sealed partial class ZoomItTests
{
    [TestMethod]
    public void DemoTypeInNotepadDoesNotInjectIndentationProbe()
    {
        const string expected = "ZoomIt50606";
        PrepareDemoScript(expected + "[end]");
        ui.SetSlider("ZoomItDemoTypeSpeedSlider", 10, "DemoTypeSpeedSlider", 10);
        var shortcut = ui.Shortcut("ZoomItDemoTypeShortcut");
        Assert.IsFalse(shortcut.Any(key => key is Key.Space or Key.Backspace), "The activation shortcut must not contain either probe key.");
        OpenNotepad();
        Assert.IsNotNull(notepad);

        var notepadWindow = new IntPtr(notepad.WindowHandle);
        var observer = new KeyboardInputObserver(notepadWindow);
        var activationEventIndex = -1;
        try
        {
            // Positively verify both probe keys, then exclude these calibration events from the assertion.
            KeyboardHelper.SendChord(Key.Space);
            WaitForNotepadText(" ", 10_000);
            KeyboardHelper.SendChord(Key.Backspace);
            WaitForNotepadText(string.Empty, 10_000);
            (uint VirtualKey, bool IsKeyDown)[] calibrationKeys =
            [
                ((uint)Key.Space, true),
                ((uint)Key.Space, false),
                ((uint)Key.Backspace, true),
                ((uint)Key.Backspace, false),
            ];
            var calibrated = WaitHelper.WaitForStable(
                observer.Snapshot,
                inputs => inputs is not null && inputs.Select(input => (input.VirtualKey, input.IsKeyDown)).SequenceEqual(calibrationKeys),
                10_000,
                2,
                pollIntervalMS: 50);
            Assert.IsTrue(calibrated.Succeeded, "The observer must see both injected Space/Backspace down/up pairs before trusting a negative result.");
            activationEventIndex = calibrated.LastObservation!.Length;

            ui.Step("Observing Demo Type's real injected keys in the owned Notepad document");
            KeyboardHelper.SendChord(shortcut);
            WaitForNotepadText(expected, 90_000);
            var delivered = WaitHelper.WaitForStable(
                () => observer.Snapshot().Skip(activationEventIndex).Count(input => input.IsUnicodePacket),
                count => count >= expected.Length * 2,
                10_000,
                2,
                pollIntervalMS: 50);
            Assert.IsTrue(delivered.Succeeded, $"The observer missed Demo Type's Unicode key pairs: expected {expected.Length * 2}, actual {delivered.LastObservation}.");
        }
        finally
        {
            try
            {
                observer.Dispose();
            }
            finally
            {
                var captured = observer.Snapshot();
                var path = CaptureEvidencePath("demo-type-input-events.json");
                File.WriteAllText(path, JsonSerializer.Serialize(new
                {
                    NotepadWindow = $"0x{notepadWindow.ToInt64():X}",
                    ExpectedText = expected,
                    ActivationEventIndex = activationEventIndex,
                    observer.Overflowed,
                    ObserverFailure = observer.Failure?.ToString(),
                    Events = captured,
                }));
                TestContext.AddResultFile(path);
                TestContext.WriteLine($"Demo Type input events (activation starts at index {activationEventIndex}): {string.Join("; ", captured)}");
            }
        }

        Assert.IsNull(observer.Failure, $"Input observation failed: {observer.Failure}");
        Assert.IsFalse(observer.Overflowed, "The bounded input buffer overflowed; absence of probe events cannot be established.");
        var emitted = observer.Snapshot().Skip(activationEventIndex).ToArray();
        var expectedPackets = expected.SelectMany(character => new[] { ((uint)character, true), ((uint)character, false) }).ToArray();
        var actualPackets = emitted.Where(input => input.IsUnicodePacket).Select(input => (input.ScanCode, input.IsKeyDown)).ToArray();
        CollectionAssert.AreEqual(expectedPackets, actualPackets, "Every scripted Unicode down/up pair must be observed in order; an inactive hook must not pass.");
        SaveDesktop("demo-type-notepad-no-indentation-probe");

        // The old probe leaves identical final text, so assert its input events independently of spelling/autocorrect.
        var probeEvents = emitted.Where(input => input.IsProbeKey).ToArray();
        Assert.IsEmpty(probeEvents, $"Demo Type must not inject Space/Backspace to discover Notepad indentation. Observed: {string.Join("; ", probeEvents)}");
    }
}
