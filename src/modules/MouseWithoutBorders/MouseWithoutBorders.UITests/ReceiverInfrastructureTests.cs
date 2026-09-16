// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
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
        using var receiver = new ReceiverController("Probe", Guid.NewGuid().ToString());
        receiver.FocusInput();
        var session = Session.FromProcess(Environment.ProcessId.ToString(), timeoutMS: 10_000);
        var input = session.Find<Element>(By.AccessibilityId("InputReceiver"), timeoutMS: 10_000);

        Assert.AreEqual("MWB input receiver", input.Name);
        Assert.AreEqual(string.Empty, receiver.ReceivedText);
        Assert.AreEqual(0, receiver.Clicks);
        Assert.IsTrue(receiver.InputFocused);
        Assert.AreEqual(receiver.Handle, NativeSupport.GetForegroundWindow());
        Assert.IsTrue(Process.GetCurrentProcess().SessionId > 0);

        var evidenceRoot = RunFiles.PersistentResultsRoot(TestContext.TestRunDirectory);
        Directory.CreateDirectory(evidenceRoot);
        var evidence = Path.Combine(evidenceRoot, "receiver-infrastructure-" + Guid.NewGuid().ToString("N") + ".json");
        RunFiles.Write(evidence, new { ForegroundHwnd = receiver.Handle.ToInt64(), receiver.InputFocused });
        TestContext.AddResultFile(evidence);
    }
}
