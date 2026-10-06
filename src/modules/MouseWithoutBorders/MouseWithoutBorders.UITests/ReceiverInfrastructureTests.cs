// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
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
            var digest = PublishWhileClipboardIsOwned(receiver);
            Assert.AreEqual(64, digest.Length, "Publication must acknowledge the generated token, not an empty transient read.");
            RunFiles.Wait(() => receiver.ClipboardDigest() == digest, TimeSpan.FromSeconds(15), "The receiver did not publish its synthetic clipboard token.");
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

    private static string PublishWhileClipboardIsOwned(ReceiverController receiver)
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
                Thread.Sleep(2000);
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
            return receiver.PublishClipboard();
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
