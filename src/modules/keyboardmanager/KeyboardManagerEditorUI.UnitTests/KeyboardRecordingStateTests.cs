// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace KeyboardManagerEditorUI.UnitTests
{
    [TestClass]
    public class KeyboardRecordingStateTests
    {
        [TestMethod]
        [DataRow(VirtualKey.Shift)]
        [DataRow(VirtualKey.LeftShift)]
        [DataRow(VirtualKey.RightShift)]
        public void TryCompleteRestoration_ShouldAllowHeldShiftOnlyOnce(VirtualKey key)
        {
            var state = new KeyboardRecordingState();
            int generation = state.StartRecording();
            state.RequestExit();
            uint marker = state.AddRestoration(key);
            var heldKeys = new HashSet<VirtualKey> { key };

            Assert.IsFalse(state.CanCompleteExit(generation));
            Assert.IsTrue(state.TryCompleteRestoration(marker, heldKeys, out bool suppress));
            Assert.IsFalse(suppress);
            Assert.IsTrue(state.CanCompleteExit(generation));
            Assert.IsFalse(state.TryCompleteRestoration(marker, heldKeys, out _));
        }

        [TestMethod]
        public void TryCompleteRestoration_ShouldSuppressShiftReleasedBeforeItsTag()
        {
            var state = new KeyboardRecordingState();
            int generation = state.StartRecording();
            state.RequestExit();
            uint marker = state.AddRestoration(VirtualKey.LeftShift);
            var heldKeys = new HashSet<VirtualKey> { VirtualKey.LeftShift };

            heldKeys.Remove(VirtualKey.LeftShift);

            Assert.IsTrue(state.TryCompleteRestoration(marker, heldKeys, out bool suppress));
            Assert.IsTrue(suppress);
            Assert.IsTrue(state.CanCompleteExit(generation));
        }

        [TestMethod]
        public void TryCompleteRestoration_ShouldSuppressPendingShiftAfterRecordingStops()
        {
            var state = new KeyboardRecordingState();
            int generation = state.StartRecording();
            state.RequestExit();
            uint marker = state.AddRestoration(VirtualKey.LeftShift);
            var heldKeys = new HashSet<VirtualKey> { VirtualKey.LeftShift };

            state.StopRecording();

            Assert.IsFalse(state.IsRecording);
            Assert.IsFalse(state.CanCompleteExit(generation));
            Assert.IsTrue(state.NeedsHook);
            Assert.IsTrue(state.TryCompleteRestoration(marker, heldKeys, out bool suppress));
            Assert.IsTrue(suppress);
            Assert.IsFalse(state.NeedsHook);
        }

        [TestMethod]
        public void TryCompleteRestoration_ShouldNotRestoreShiftIntoNewRecording()
        {
            var state = new KeyboardRecordingState();
            int oldGeneration = state.StartRecording();
            state.RequestExit();
            uint oldMarker = state.AddRestoration(VirtualKey.LeftShift);
            state.StopRecording();
            int currentGeneration = state.StartRecording();
            var heldKeys = new HashSet<VirtualKey> { VirtualKey.LeftShift };

            Assert.IsTrue(state.TryCompleteRestoration(oldMarker, heldKeys, out bool suppress));

            Assert.IsTrue(suppress);
            Assert.AreEqual(currentGeneration, state.Generation);
            Assert.IsTrue(state.IsRecording);
            Assert.IsFalse(state.ExitRequested);
            Assert.IsFalse(state.CanCompleteExit(oldGeneration));
            Assert.IsFalse(state.CanCompleteExit(currentGeneration));
            Assert.IsTrue(state.NeedsHook);
        }

        [TestMethod]
        public void CanCompleteExit_ShouldIgnoreCleanupForEarlierRecording()
        {
            var state = new KeyboardRecordingState();
            int oldGeneration = state.StartRecording();
            state.RequestExit();
            Assert.IsTrue(state.CanCompleteExit(oldGeneration));

            // The old cleanup was queued, but a new recording starts before it runs.
            state.StopRecording();
            int currentGeneration = state.StartRecording();

            Assert.IsFalse(state.CanCompleteExit(oldGeneration));
            Assert.IsTrue(state.IsRecording);
            Assert.IsFalse(state.ExitRequested);

            state.RequestExit();

            Assert.IsFalse(state.CanCompleteExit(oldGeneration));
            Assert.IsTrue(state.CanCompleteExit(currentGeneration));
        }

        [TestMethod]
        public void CanCompleteExit_ShouldFinishNewTabExitWhileCanceledRestorationIsPending()
        {
            var state = new KeyboardRecordingState();
            state.StartRecording();
            state.RequestExit();
            uint oldMarker = state.AddRestoration(VirtualKey.LeftShift);
            state.StopRecording();
            int currentGeneration = state.StartRecording();
            state.RequestExit();

            Assert.IsTrue(state.CanCompleteExit(currentGeneration));
            Assert.IsTrue(state.HasPendingRestorations);

            state.StopRecording();

            Assert.IsTrue(state.NeedsHook);
            Assert.IsTrue(state.TryCompleteRestoration(oldMarker, new HashSet<VirtualKey> { VirtualKey.LeftShift }, out bool suppress));
            Assert.IsTrue(suppress);
            Assert.IsFalse(state.NeedsHook);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void TryCompleteRestoration_ShouldKeepOldAndCurrentMarkersIndependent(bool oldMarkerArrivesFirst)
        {
            var state = new KeyboardRecordingState();
            int oldGeneration = state.StartRecording();
            state.RequestExit();
            uint oldMarker = state.AddRestoration(VirtualKey.LeftShift);
            state.StopRecording();
            int currentGeneration = state.StartRecording();
            state.RequestExit();
            uint currentMarker = state.AddRestoration(VirtualKey.LeftShift);
            var heldKeys = new HashSet<VirtualKey> { VirtualKey.LeftShift };

            uint firstMarker = oldMarkerArrivesFirst ? oldMarker : currentMarker;
            uint secondMarker = oldMarkerArrivesFirst ? currentMarker : oldMarker;

            Assert.IsTrue(state.TryCompleteRestoration(firstMarker, heldKeys, out bool suppressFirst));
            Assert.AreEqual(oldMarkerArrivesFirst, suppressFirst);
            Assert.AreEqual(!oldMarkerArrivesFirst, state.CanCompleteExit(currentGeneration));
            Assert.IsTrue(state.NeedsHook);

            Assert.IsTrue(state.TryCompleteRestoration(secondMarker, heldKeys, out bool suppressSecond));
            Assert.AreEqual(!oldMarkerArrivesFirst, suppressSecond);
            Assert.AreEqual(currentGeneration, state.Generation);
            Assert.IsTrue(state.IsRecording);
            Assert.IsFalse(state.CanCompleteExit(oldGeneration));
            Assert.IsTrue(state.CanCompleteExit(currentGeneration));
        }

        [TestMethod]
        public void TryCompleteRestoration_ShouldWaitForEveryCurrentShiftRestoration()
        {
            var state = new KeyboardRecordingState();
            int generation = state.StartRecording();
            state.RequestExit();
            uint leftMarker = state.AddRestoration(VirtualKey.LeftShift);
            uint rightMarker = state.AddRestoration(VirtualKey.RightShift);
            var heldKeys = new HashSet<VirtualKey> { VirtualKey.LeftShift };

            Assert.IsTrue(state.TryCompleteRestoration(rightMarker, heldKeys, out bool suppressRight));
            Assert.IsTrue(suppressRight);
            Assert.IsFalse(state.CanCompleteExit(generation));

            Assert.IsTrue(state.TryCompleteRestoration(leftMarker, heldKeys, out bool suppressLeft));
            Assert.IsFalse(suppressLeft);
            Assert.IsTrue(state.CanCompleteExit(generation));
        }

        [TestMethod]
        public void RemoveUnsentRestoration_ShouldAllowExitAfterFailedSend()
        {
            var state = new KeyboardRecordingState();
            int generation = state.StartRecording();
            state.RequestExit();
            uint leftMarker = state.AddRestoration(VirtualKey.LeftShift);
            uint rightMarker = state.AddRestoration(VirtualKey.RightShift);

            state.RemoveUnsentRestoration(leftMarker);
            Assert.IsFalse(state.CanCompleteExit(generation));
            state.RemoveUnsentRestoration(rightMarker);

            Assert.IsTrue(state.CanCompleteExit(generation));
            state.StopRecording();
            Assert.IsFalse(state.NeedsHook);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void RemoveUnsentRestoration_ShouldDrainPartialSendInEitherOrder(bool sentInputArrivesBeforeSendReturns)
        {
            var state = new KeyboardRecordingState();
            int generation = state.StartRecording();
            state.RequestExit();
            uint sentMarker = state.AddRestoration(VirtualKey.LeftShift);
            uint unsentMarker = state.AddRestoration(VirtualKey.RightShift);
            var heldKeys = new HashSet<VirtualKey> { VirtualKey.LeftShift, VirtualKey.RightShift };

            if (sentInputArrivesBeforeSendReturns)
            {
                Assert.IsTrue(state.TryCompleteRestoration(sentMarker, heldKeys, out bool suppress));
                Assert.IsFalse(suppress);
                Assert.IsFalse(state.CanCompleteExit(generation));
                state.RemoveUnsentRestoration(unsentMarker);
            }
            else
            {
                state.RemoveUnsentRestoration(unsentMarker);
                Assert.IsFalse(state.CanCompleteExit(generation));
                Assert.IsTrue(state.TryCompleteRestoration(sentMarker, heldKeys, out bool suppress));
                Assert.IsFalse(suppress);
            }

            Assert.IsTrue(state.CanCompleteExit(generation));
            state.StopRecording();
            Assert.IsFalse(state.NeedsHook);
        }

        [TestMethod]
        public void RemoveUnsentRestoration_ShouldPreserveNewCaptureWhenEarlierSendFails()
        {
            var state = new KeyboardRecordingState();
            state.StartRecording();
            state.RequestExit();
            uint oldMarker = state.AddRestoration(VirtualKey.LeftShift);
            state.StopRecording();
            int currentGeneration = state.StartRecording();
            state.RequestExit();
            uint currentMarker = state.AddRestoration(VirtualKey.LeftShift);

            state.RemoveUnsentRestoration(oldMarker);

            Assert.IsFalse(state.CanCompleteExit(currentGeneration));
            Assert.IsTrue(state.TryCompleteRestoration(currentMarker, new HashSet<VirtualKey> { VirtualKey.LeftShift }, out bool suppress));
            Assert.IsFalse(suppress);
            Assert.IsTrue(state.CanCompleteExit(currentGeneration));
        }

        [TestMethod]
        public void NeedsHook_ShouldRemainInstalledUntilRecordingStops()
        {
            var state = new KeyboardRecordingState();
            Assert.IsFalse(state.NeedsHook);

            int generation = state.StartRecording();
            Assert.IsTrue(state.NeedsHook);

            state.RequestExit();
            Assert.IsTrue(state.CanCompleteExit(generation));
            Assert.IsTrue(state.NeedsHook);

            state.StopRecording();
            Assert.IsFalse(state.NeedsHook);
        }
    }
}
