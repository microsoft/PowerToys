// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ManagedCommon.UnitTests;

[TestClass]
public sealed class InputInjectionTests
{
    private const ushort VkControl = 0x11;
    private const ushort VkShift = 0x10;
    private const ushort VkMenu = 0x12;
    private const ushort VkReturn = 0x0D;
    private const ushort VkV = 0x56;
    private const ushort VkLControl = 0xA2;
    private const ushort VkRControl = 0xA3;
    private const ushort VkLWin = 0x5B;
    private const ushort VkLShift = 0xA0;
    private const ushort VkRMenu = 0xA5;
    private const uint KeyUp = 0x0002;
    private const int ErrorAccessDenied = 5;

    private static readonly UIntPtr TestExtraInfo = (UIntPtr)0x5555;

    [TestMethod]
    public void SendChordSendsWholeChordInOneTaggedBatch()
    {
        var fake = new FakeInputApi();

        var result = InputInjection.SendChord(new[] { VkControl, VkV }, OptionsWithoutWait(), fake);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, fake.Batches.Count);
        var batch = fake.Batches[0];
        Assert.AreEqual(5, batch.Length);
        AssertKey(batch[0], InputInjection.StartMenuSuppressionKey, keyUp: true);
        AssertKey(batch[1], VkControl, keyUp: false);
        AssertKey(batch[2], VkV, keyUp: false);
        AssertKey(batch[3], VkV, keyUp: true);
        AssertKey(batch[4], VkControl, keyUp: true);
    }

    [TestMethod]
    public void SendChordPartialSendReleasesKeysThatWentDown()
    {
        // Start menu suppression, Ctrl down and V down go out; the key-ups don't.
        var fake = new FakeInputApi { SendLimits = { 3 } };

        var result = InputInjection.SendChord(new[] { VkControl, VkV }, OptionsWithoutWait(), fake);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(3u, result.Sent);
        Assert.AreEqual(ErrorAccessDenied, result.Error);
        Assert.AreEqual(2, fake.Batches.Count);
        var cleanup = fake.Batches[1];
        Assert.AreEqual(2, cleanup.Length);
        AssertKey(cleanup[0], VkV, keyUp: true);
        AssertKey(cleanup[1], VkControl, keyUp: true);
        Assert.AreEqual(2u, result.CleanupRequested);
        Assert.AreEqual(2u, result.CleanupSent);
    }

    [TestMethod]
    public void SendChordPartialSendOnlyReleasesKeysWhoseKeyUpWasNotSent()
    {
        var fake = new FakeInputApi { SendLimits = { 4 } };

        var result = InputInjection.SendChord(new[] { VkControl, VkV }, OptionsWithoutWait(), fake);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(2, fake.Batches.Count);
        Assert.AreEqual(1, fake.Batches[1].Length);
        AssertKey(fake.Batches[1][0], VkControl, keyUp: true);
    }

    [TestMethod]
    [DataRow(0u)]
    [DataRow(1u)]
    public void SendChordSendInputStopsBeforeAnyKeyDownSendsNoCleanup(uint limit)
    {
        var fake = new FakeInputApi { SendLimits = { limit } };

        var result = InputInjection.SendChord(new[] { VkControl, VkV }, OptionsWithoutWait(), fake);

        Assert.IsFalse(result.Succeeded);
        Assert.AreEqual(1, fake.Batches.Count);
        Assert.AreEqual(0u, result.CleanupRequested);
    }

    [TestMethod]
    public void SendChordNoKeysAndNothingHeldDoesNotCallSendInput()
    {
        var fake = new FakeInputApi();

        var result = InputInjection.SendChord(Array.Empty<ushort>(), new InputChordOptions { ExtraInfo = TestExtraInfo, SuppressStartMenu = false }, fake);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(0u, result.Requested);
        Assert.AreEqual(0, fake.Batches.Count);
    }

    [TestMethod]
    public void SendChordNoModifiersHeldInjectsNoModifierEvents()
    {
        var fake = new FakeInputApi();

        var result = InputInjection.SendChord(new[] { VkReturn }, new InputChordOptions { ExtraInfo = TestExtraInfo, SuppressStartMenu = false }, fake);

        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(1, fake.Batches.Count);
        Assert.AreEqual(2, fake.Batches[0].Length);
        AssertKey(fake.Batches[0][0], VkReturn, keyUp: false);
        AssertKey(fake.Batches[0][1], VkReturn, keyUp: true);
    }

    [TestMethod]
    public void SendChordHeldModifiersAreReleasedAndNeverPressedAgain()
    {
        // Fail partway so the cleanup path runs too.
        var fake = new FakeInputApi { SendLimits = { 5 } };
        fake.Held.UnionWith(new[] { VkLWin, VkLShift, VkRMenu });

        InputInjection.SendChord(new[] { VkControl, VkV }, OptionsWithoutWait(), fake);

        var batch = fake.Batches[0];
        AssertKey(batch[0], InputInjection.StartMenuSuppressionKey, keyUp: true);
        AssertKey(batch[1], VkLWin, keyUp: true);
        AssertKey(batch[2], VkLShift, keyUp: true);
        AssertKey(batch[3], VkRMenu, keyUp: true);
        AssertKey(batch[4], VkControl, keyUp: false);

        var modifiers = new HashSet<ushort>(InputInjection.ModifierKeys) { VkControl, VkShift, VkMenu };
        var modifierKeyDowns = fake.Batches
            .SelectMany(b => b)
            .Where(i => (i.data.ki.dwFlags & KeyUp) == 0 && modifiers.Contains((ushort)i.data.ki.wVk))
            .Select(i => (ushort)i.data.ki.wVk);
        CollectionAssert.AreEqual(new[] { VkControl }, modifierKeyDowns.ToArray(), "Only the chord's own modifier may be pressed.");
    }

    [TestMethod]
    public void SendChordDoesNotReleaseHeldModifiersWhenDisabled()
    {
        var fake = new FakeInputApi();
        fake.Held.Add(VkLControl);

        InputInjection.SendChord(new[] { VkV }, new InputChordOptions { ExtraInfo = TestExtraInfo, SuppressStartMenu = false, ReleaseHeldModifiers = false }, fake);

        Assert.AreEqual(2, fake.Batches[0].Length);
        AssertKey(fake.Batches[0][0], VkV, keyUp: false);
    }

    [TestMethod]
    public void SendChordWaitsForModifiersToBeReleased()
    {
        var fake = new FakeInputApi { ReleaseAfterSleeps = 3 };
        fake.Held.UnionWith(new[] { VkLControl, VkLShift });

        var result = InputInjection.SendChord(
            new[] { VkControl, VkV },
            new InputChordOptions { ExtraInfo = TestExtraInfo, ModifierReleaseTimeout = TimeSpan.FromSeconds(1) },
            fake);

        Assert.IsFalse(result.ModifierWaitTimedOut);
        Assert.AreEqual(3, fake.Sleeps);
        Assert.AreEqual(5, fake.Batches[0].Length, "No key-ups needed once the user let go.");
    }

    [TestMethod]
    public void SendChordWaitTimesOutReleasesModifiersThatAreStillHeld()
    {
        var fake = new FakeInputApi();
        fake.Held.Add(VkRControl);

        var result = InputInjection.SendChord(
            new[] { VkControl, VkV },
            new InputChordOptions { ExtraInfo = TestExtraInfo, ModifierReleaseTimeout = TimeSpan.FromMilliseconds(50), PollInterval = TimeSpan.FromMilliseconds(10) },
            fake);

        Assert.IsTrue(result.ModifierWaitTimedOut);
        Assert.AreEqual(5, fake.Sleeps);
        AssertKey(fake.Batches[0][1], VkRControl, keyUp: true);
        AssertKey(fake.Batches[0][2], VkControl, keyUp: false);
    }

    private static InputChordOptions OptionsWithoutWait() => new() { ExtraInfo = TestExtraInfo };

    private static void AssertKey(NativeMethods.INPUT input, ushort vk, bool keyUp)
    {
        Assert.AreEqual(NativeMethods.INPUTTYPE.INPUT_KEYBOARD, input.type);
        Assert.AreEqual(vk, (ushort)input.data.ki.wVk);
        Assert.AreEqual(keyUp ? KeyUp : 0u, input.data.ki.dwFlags);
        Assert.AreEqual(TestExtraInfo, input.data.ki.dwExtraInfo);
    }

    // Records SendInput batches and serves a fake keyboard state.
    private sealed class FakeInputApi : IInputInjectionApi
    {
        public List<NativeMethods.INPUT[]> Batches { get; } = new();

        // How many events each SendInput call accepts; all when not set.
        public List<uint> SendLimits { get; } = new();

        public HashSet<ushort> Held { get; } = new();

        public int Sleeps { get; private set; }

        // Clears Held after this many sleeps; never when negative.
        public int ReleaseAfterSleeps { get; init; } = -1;

        public uint SendInput(NativeMethods.INPUT[] inputs, out int lastError)
        {
            uint accepted = Batches.Count < SendLimits.Count ? Math.Min((uint)inputs.Length, SendLimits[Batches.Count]) : (uint)inputs.Length;
            Batches.Add((NativeMethods.INPUT[])inputs.Clone());
            lastError = accepted == inputs.Length ? 0 : ErrorAccessDenied;
            return accepted;
        }

        public bool IsKeyDown(ushort virtualKey) => Held.Contains(virtualKey);

        public void Sleep(TimeSpan duration)
        {
            if (++Sleeps == ReleaseAfterSleeps)
            {
                Held.Clear();
            }
        }
    }
}
