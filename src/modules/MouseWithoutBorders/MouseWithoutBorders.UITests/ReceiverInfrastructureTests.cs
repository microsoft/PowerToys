// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.MouseWithoutBorders.UITests;

[TestClass]
public sealed class ReceiverInfrastructureTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("MwbInfrastructure")]
    public void ReceiverInfrastructureRemainsResponsiveToUiAutomation()
    {
        using var receiver = new ReceiverController("Host", Guid.NewGuid().ToString());
        receiver.FocusInput();
        var session = Session.FromProcess(Environment.ProcessId.ToString(), timeoutMS: 10_000);
        var input = session.Find<Element>(By.AccessibilityId("InputReceiver"), timeoutMS: 10_000);

        Assert.AreEqual("MWB input receiver", input.Name);
        Assert.AreEqual(string.Empty, receiver.ReceivedText);
        Assert.AreEqual(0, receiver.Clicks);
        Assert.IsTrue(receiver.InputFocused);
        Assert.AreEqual(receiver.Handle, NativeSupport.GetForegroundWindow());
        Assert.IsTrue(Process.GetCurrentProcess().SessionId > 0);

        try
        {
            var digest = WhileClipboardIsOwned(receiver.PublishClipboard);
            Assert.AreEqual(64, digest.Length, "Publication must acknowledge the generated token, not an empty transient read.");
            RunFiles.Wait(() => receiver.ClipboardDigest() == digest, TimeSpan.FromSeconds(15), "The receiver did not publish its synthetic clipboard token.");

            var contended = WhileClipboardIsOwned(receiver.ObserveClipboard);
            Assert.IsFalse(contended.Available, "A held clipboard must be reported as unavailable, not empty.");
            Assert.AreEqual(string.Empty, contended.Digest);
            Assert.AreEqual(unchecked((int)0x800401D0), contended.ErrorHResult);
            var available = receiver.ObserveClipboard();
            Assert.IsTrue(available.Available);
            Assert.IsNull(available.ErrorHResult);
            Assert.AreEqual(digest, available.Digest, "Contention must not replace or republish the existing token.");
        }
        finally
        {
            receiver.RestoreClipboard();
        }

        var evidenceRoot = RunFiles.PersistentResultsRoot(TestContext.TestRunDirectory);
        Directory.CreateDirectory(evidenceRoot);
        var evidence = Path.Combine(evidenceRoot, "receiver-infrastructure-" + Guid.NewGuid().ToString("N") + ".json");
        RunFiles.Write(evidence, new { ForegroundHwnd = receiver.Handle.ToInt64(), receiver.InputFocused });
        TestContext.AddResultFile(evidence);
    }

    [TestMethod]
    [TestCategory("MwbInfrastructure")]
    public void ClipboardObservationDistinguishesContentionFromReadableData()
    {
        var busy = ClipboardObservation.Read(() => throw new ExternalException("Clipboard is owned.", unchecked((int)0x800401D0)));
        Assert.IsFalse(busy.Available);
        Assert.AreEqual(string.Empty, busy.Digest);
        Assert.AreEqual(unchecked((int)0x800401D0), busy.ErrorHResult);
        var unrelated = new ExternalException("Unrelated clipboard failure.", unchecked((int)0x80004005));
        Assert.AreSame(unrelated, Assert.ThrowsExactly<ExternalException>(() => ClipboardObservation.Read(() => throw unrelated)));

        var observation = new JsonObject
        {
            ["ClipboardAvailable"] = false,
            ["ClipboardErrorHResult"] = unchecked((int)0x800401D0),
            ["ClipboardDigest"] = null,
        };
        Assert.IsNull(TwoEndpointFixture.ReadClipboardDigest(observation));
        observation["ClipboardErrorHResult"] = unchecked((int)0x80004005);
        Assert.ThrowsExactly<AssertFailedException>(() => TwoEndpointFixture.ReadClipboardDigest(observation));
        observation["ClipboardAvailable"] = true;
        Assert.ThrowsExactly<AssertFailedException>(() => TwoEndpointFixture.ReadClipboardDigest(observation));
        observation["ClipboardErrorHResult"] = null;
        observation["ClipboardDigest"] = string.Empty;
        Assert.AreEqual(string.Empty, TwoEndpointFixture.ReadClipboardDigest(observation));
        observation["ClipboardDigest"] = new string('A', 64);
        Assert.AreEqual(new string('A', 64), TwoEndpointFixture.ReadClipboardDigest(observation));
        Assert.IsFalse(TwoEndpointFixture.ClipboardMatches(null, new string('A', 64), TimeSpan.Zero));
        Assert.IsTrue(TwoEndpointFixture.ClipboardMatches(new string('A', 64), new string('A', 64), TimeSpan.FromSeconds(15)));
        Assert.IsFalse(TwoEndpointFixture.ClipboardMatches(new string('A', 64), new string('A', 64), TimeSpan.FromSeconds(15) + TimeSpan.FromTicks(1)));
    }

    private static T WhileClipboardIsOwned<T>(Func<T> action)
    {
        using var acquired = new ManualResetEventSlim();
        var locker = Task.Run(() =>
        {
            var watch = Stopwatch.StartNew();
            while (!OpenClipboard(IntPtr.Zero))
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(5))
                {
                    throw new InvalidOperationException("The test-owned clipboard lock could not be acquired.");
                }

                Thread.Sleep(50);
            }

            bool closed;
            try
            {
                acquired.Set();
                Thread.Sleep(8000);
            }
            finally
            {
                closed = CloseClipboard();
            }

            if (!closed)
            {
                throw new InvalidOperationException("The test-owned clipboard lock did not close.");
            }
        });
        try
        {
            Assert.IsTrue(acquired.Wait(TimeSpan.FromSeconds(10)), "The real Windows clipboard lock did not acknowledge ownership.");
            return action();
        }
        finally
        {
            locker.GetAwaiter().GetResult();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
}
