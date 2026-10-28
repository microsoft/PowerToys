// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.GPOWrapper;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class HostIntegrationPolicyTests
{
    [TestMethod]
    public void EachSignedInSessionGetsItsOwnEngine()
    {
        // A Global\ name let the first session's engine keep every other session's from starting,
        // although the runner, its own instance mutex and the exit event are all per session.
        Assert.AreEqual(@"Local\PowerToys.TextExpander.SingleInstance.powertoys", Program.InstanceMutexName(HostLink.ParseInstanceSuffix(["1234", "--managed", "--instance:powertoys"])));
        Assert.AreEqual(@"Local\PowerToys.TextExpander.SingleInstance", Program.InstanceMutexName(HostLink.ParseInstanceSuffix([])));
    }

    [TestMethod]
    public void OnlyAPolicyThatTurnsTextExpanderOffKeepsTheEngineFromStarting()
    {
        // The runner checked the policy, but the engine could still be started on its own.
        Assert.IsFalse(Program.PolicyAllowsStart(GpoRuleConfigured.Disabled));
        Assert.IsTrue(Program.PolicyAllowsStart(GpoRuleConfigured.Enabled));
        Assert.IsTrue(Program.PolicyAllowsStart(GpoRuleConfigured.NotConfigured));
        Assert.IsTrue(Program.PolicyAllowsStart(GpoRuleConfigured.Unavailable));
        Assert.IsTrue(Program.PolicyAllowsStart(GpoRuleConfigured.WrongValue));
    }

    [TestMethod]
    public void HostInputTagsDistinguishOwnInjectedRemappedAndOrdinaryInput()
    {
        var own = new IntPtr(0x47544558);
        Assert.IsTrue(HostInputTags.ShouldIgnore(own, own));
        Assert.IsTrue(HostInputTags.ShouldIgnore(new IntPtr(HostInputTags.CentralizedHookDontTrigger), own));
        Assert.IsFalse(HostInputTags.ShouldIgnore(new IntPtr(HostInputTags.KeyboardManagerInjected), own));
        Assert.IsFalse(HostInputTags.ShouldIgnore(IntPtr.Zero, own));

        try
        {
            HostInputTags.AcceptRemappedInput = false;
            Assert.IsTrue(HostInputTags.ShouldIgnore(new IntPtr(HostInputTags.KeyboardManagerInjected), own));
        }
        finally
        {
            HostInputTags.AcceptRemappedInput = true;
        }
    }

    [TestMethod]
    public void HostLinkParsesPowerToysContractArguments()
    {
        Assert.AreEqual(HostLink.DefaultExitEventName, HostLink.ParseExitEventName([]));
        Assert.IsTrue(HostLink.DefaultExitEventName.StartsWith(@"Local\", StringComparison.Ordinal));
        Assert.AreEqual("Local\\Custom", HostLink.ParseExitEventName(["--exit-event:Local\\Custom"]));
        Assert.AreEqual(4321, HostLink.ParseHostProcessId(["4321"]));
        Assert.AreEqual(99, HostLink.ParseHostProcessId(["7", "--host-pid:99"]));
        Assert.IsNull(HostLink.ParseHostProcessId(["--user:1234"]));
        Assert.AreEqual(".powertoys", HostLink.ParseInstanceSuffix(["--instance:powertoys"]));
        Assert.AreEqual(string.Empty, HostLink.ParseInstanceSuffix([@"--instance:Global\x"]));
    }

    [TestMethod]
    public void HostLinkRejectsEmptyInvalidAndNonPositiveValues()
    {
        Assert.AreEqual(HostLink.DefaultExitEventName, HostLink.ParseExitEventName(["--exit-event:   "]));
        Assert.IsNull(HostLink.ParseHostProcessId(["0"]));
        Assert.IsNull(HostLink.ParseHostProcessId(["-5"]));
        Assert.IsNull(HostLink.ParseHostProcessId(["--managed"]));
        Assert.AreEqual(string.Empty, HostLink.ParseInstanceSuffix(["--instance:  "]));
    }

    [TestMethod]
    public void HostInputTagsCanOptOutOfRemappedInputWithoutSuppressingOrdinaryInput()
    {
        var own = new IntPtr(0x47544558);
        try
        {
            HostInputTags.AcceptRemappedInput = false;
            Assert.IsTrue(HostInputTags.ShouldIgnore(new IntPtr(HostInputTags.KeyboardManagerInjected), own));
            Assert.IsFalse(HostInputTags.ShouldIgnore(IntPtr.Zero, own));
        }
        finally
        {
            HostInputTags.AcceptRemappedInput = true;
        }
    }
}
