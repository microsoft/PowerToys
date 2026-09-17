// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UiTextBox = Microsoft.PowerToys.UITest.Next.TextBox;

namespace Microsoft.MouseWithoutBorders.UITests;

[TestClass]
public sealed class RecordingInfrastructureTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("MwbInfrastructure")]
    public void DesktopAndWindowVideosSurviveAnEarlyFailure()
    {
        var directory = Path.Combine(RunFiles.PersistentResultsRoot(TestContext.TestRunDirectory),
            "mwb-recording-probe-" + Guid.NewGuid().ToString("N"));
        using var subject = new ReceiverController("Recorded window", Guid.NewGuid().ToString());
        subject.FocusInput();
        var session = Session.FromProcess(Environment.ProcessId.ToString(), timeoutMS: 10_000);
        session.Find<UiTextBox>(By.Id("InputReceiver")).SetText("recorded window");

        using var recordings = new TestRecordings(TestContext, directory);
        var expected = new InvalidOperationException("Simulated failure after video capture starts.");
        var actual = Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            try
            {
                recordings.StartDesktop();
                recordings.CaptureSandboxWindow(subject.Handle.ToInt64());
                Thread.Sleep(TimeSpan.FromSeconds(2));

                using var cover = new ReceiverController("Covering window", Guid.NewGuid().ToString());
                cover.FocusInput();
                Assert.AreEqual(cover.Handle, NativeSupport.GetForegroundWindow());
                Thread.Sleep(TimeSpan.FromSeconds(3));
                throw expected;
            }
            finally
            {
                recordings.Complete();
            }
        });
        Assert.AreSame(expected, actual);

        var manifest = RunFiles.Read(Path.Combine(directory, "recordings.json"))["Recordings"]!.AsArray();
        Assert.AreEqual(2, manifest.Count, "The desktop and separately captured window must both be recorded.");
        foreach (var recording in manifest)
        {
            Assert.IsTrue(recording!["Started"]!.GetValue<bool>(), recording.ToJsonString());
            Assert.IsTrue(recording["Completed"]!.GetValue<bool>(), recording.ToJsonString());
            Assert.IsTrue(recording["Available"]!.GetValue<bool>(), recording.ToJsonString());
            var path = Path.Combine(directory, recording["File"]!.GetValue<string>());
            Assert.IsTrue(new FileInfo(path).Length > 0, "A finalized video must persist outside MSTest's deployment tree.");
        }
    }
}
