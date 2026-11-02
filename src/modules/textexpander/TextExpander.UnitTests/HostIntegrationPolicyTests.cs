// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
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
    public void ARegionSettingsChangeReadsTheUsersFormatsAgain()
    {
        // .NET keeps the user's culture from when it was first read: a new short date format set in Region
        // settings showed in the editor, but the engine typed the old one until PowerToys restarted.
        IntPtr region = Marshal.StringToHGlobalUni("intl");
        IntPtr theme = Marshal.StringToHGlobalUni("ImmersiveColorSet");
        try
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            Assert.IsFalse(Program.RefreshCultureOnRegionChange(Native.WM_SETTINGCHANGE, theme));
            Assert.IsFalse(Program.RefreshCultureOnRegionChange(Native.WM_SETTINGCHANGE, IntPtr.Zero));
            Assert.IsFalse(Program.RefreshCultureOnRegionChange(Native.WM_DESTROY, region));
            Assert.AreSame(before, CultureInfo.CurrentCulture);

            Assert.IsTrue(Program.RefreshCultureOnRegionChange(Native.WM_SETTINGCHANGE, region));
            Assert.AreNotSame(before, CultureInfo.CurrentCulture, "Read again from Windows.");
            Assert.AreEqual(before.Name, CultureInfo.CurrentCulture.Name);
        }
        finally
        {
            Marshal.FreeHGlobal(region);
            Marshal.FreeHGlobal(theme);
        }
    }

    [TestMethod]
    public void HostInputTagsDistinguishOwnInjectedRemappedAndOrdinaryInput()
    {
        // Keyboard Manager's own tags (KeyboardManagerConstants.h). Only a bare 0x1, which it never sends, was
        // taken for a remap, and 0x111, which it swallows itself, counted as a key press. With remaps not counting
        // as typing, a remap was then dropped like our own input, although it still reaches the app.
        IntPtr own = KeyboardHook.InjectionSignature;
        (long Tag, HostInputKind RemapsCount, HostInputKind RemapsDoNotCount)[] cases =
        [
            (0x11, HostInputKind.Typing, HostInputKind.Unmatched), // a single-key remap
            (0x101, HostInputKind.Typing, HostInputKind.Unmatched), // a shortcut or text remap
            (HostInputTags.KeyboardManagerInjected, HostInputKind.Typing, HostInputKind.Unmatched),
            (0xFFC3D44F, HostInputKind.Typing, HostInputKind.Unmatched), // another app's tag with the same bit
            (0x111, HostInputKind.Ignored, HostInputKind.Ignored), // swallowed by Keyboard Manager itself
            (HostInputTags.CentralizedHookDontTrigger, HostInputKind.Ignored, HostInputKind.Ignored),
            (own.ToInt64(), HostInputKind.Ignored, HostInputKind.Ignored),
            (0, HostInputKind.Typing, HostInputKind.Typing),
        ];

        bool original = HostInputTags.AcceptRemappedInput;
        try
        {
            foreach ((long tag, HostInputKind remapsCount, HostInputKind remapsDoNotCount) in cases)
            {
                HostInputTags.AcceptRemappedInput = true;
                Assert.AreEqual(remapsCount, HostInputTags.Classify(new IntPtr(tag), own), $"0x{tag:X}");
                HostInputTags.AcceptRemappedInput = false;
                Assert.AreEqual(remapsDoNotCount, HostInputTags.Classify(new IntPtr(tag), own), $"0x{tag:X}, remaps not counted");
            }
        }
        finally
        {
            HostInputTags.AcceptRemappedInput = original;
        }
    }

    [TestMethod]
    public void OwnInputCarriesKeyboardManagersBitAndIsIgnoredEitherWay()
    {
        // Keyboard Manager remaps every key without this bit: with Ctrl or V remapped, the paste chord
        // became another key after the trigger had already been erased.
        Assert.AreEqual(HostInputTags.KeyboardManagerInjected, KeyboardHook.InjectionSignature.ToInt64() & HostInputTags.KeyboardManagerInjected);

        // Matched exactly first, so it is never taken for a remap.
        bool original = HostInputTags.AcceptRemappedInput;
        try
        {
            HostInputTags.AcceptRemappedInput = true;
            Assert.AreEqual(HostInputKind.Ignored, HostInputTags.Classify(KeyboardHook.InjectionSignature, KeyboardHook.InjectionSignature));
            HostInputTags.AcceptRemappedInput = false;
            Assert.AreEqual(HostInputKind.Ignored, HostInputTags.Classify(KeyboardHook.InjectionSignature, KeyboardHook.InjectionSignature));
        }
        finally
        {
            HostInputTags.AcceptRemappedInput = original;
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
        IntPtr own = KeyboardHook.InjectionSignature;
        bool original = HostInputTags.AcceptRemappedInput;
        try
        {
            HostInputTags.AcceptRemappedInput = false;
            Assert.AreEqual(HostInputKind.Unmatched, HostInputTags.Classify(new IntPtr(0x11), own), "Not matched, but still input.");
            Assert.AreEqual(HostInputKind.Typing, HostInputTags.Classify(IntPtr.Zero, own));
        }
        finally
        {
            HostInputTags.AcceptRemappedInput = original;
        }
    }
}
