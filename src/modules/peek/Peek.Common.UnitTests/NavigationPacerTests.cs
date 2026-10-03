// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Peek.UI.Models;

namespace Peek.Common.UnitTests
{
    [TestClass]
    public class NavigationPacerTests
    {
        private static readonly NavigationDirection[] _initialNavigation = [NavigationDirection.Forwards];

        private static readonly NavigationDirection[] _reversedNavigation = [NavigationDirection.Forwards, NavigationDirection.Backwards];

        private static readonly NavigationDirection[] _resumedNavigation = [NavigationDirection.Forwards, NavigationDirection.Backwards, NavigationDirection.Forwards];

        private static readonly NavigationDirection[] _tickNavigation = [NavigationDirection.Forwards, NavigationDirection.Forwards];

        [TestMethod]
        public void Request_RapidPresses_CoalescesToLatestDirection()
        {
            var timer = new TestTimer();
            var navigations = new List<NavigationDirection>();
            var pacer = new NavigationPacer(timer, navigations.Add);

            pacer.Request(NavigationDirection.Forwards);
            pacer.Request(NavigationDirection.Forwards);
            pacer.Request(NavigationDirection.Backwards);
            CollectionAssert.AreEqual(_initialNavigation, navigations);
            Assert.AreEqual(1, timer.StartCount);

            timer.RaiseTick();
            CollectionAssert.AreEqual(_reversedNavigation, navigations);
            Assert.IsTrue(timer.IsEnabled);

            timer.RaiseTick();
            Assert.IsFalse(timer.IsEnabled);

            pacer.Request(NavigationDirection.Forwards);
            CollectionAssert.AreEqual(_resumedNavigation, navigations);
            Assert.AreEqual(2, timer.StartCount);
        }

        [TestMethod]
        public void Suspend_PendingNavigation_DropsStepAndBlocksRequestsUntilResume()
        {
            var timer = new TestTimer();
            var navigations = new List<NavigationDirection>();
            var pacer = new NavigationPacer(timer, navigations.Add);
            pacer.Request(NavigationDirection.Forwards);
            pacer.Request(NavigationDirection.Forwards);

            pacer.Suspend();

            Assert.IsTrue(pacer.IsSuspended);
            Assert.IsFalse(timer.IsEnabled);
            pacer.Request(NavigationDirection.Backwards);
            timer.RaiseTick();
            CollectionAssert.AreEqual(_initialNavigation, navigations);
            Assert.AreEqual(1, timer.StartCount);

            pacer.Resume();

            Assert.IsFalse(pacer.IsSuspended);
            timer.RaiseTick();
            CollectionAssert.AreEqual(_initialNavigation, navigations, "Closing confirmation must not replay the queued step.");
            pacer.Request(NavigationDirection.Backwards);
            CollectionAssert.AreEqual(_reversedNavigation, navigations);
            Assert.AreEqual(2, timer.StartCount);
        }

        [TestMethod]
        public void CancelPending_DuringSuspension_DoesNotResumeNavigation()
        {
            var timer = new TestTimer();
            var navigations = new List<NavigationDirection>();
            var pacer = new NavigationPacer(timer, navigations.Add);
            pacer.Suspend();

            pacer.CancelPending();

            Assert.IsTrue(pacer.IsSuspended);
            pacer.Request(NavigationDirection.Forwards);
            timer.RaiseTick();
            Assert.AreEqual(0, navigations.Count);
            Assert.IsFalse(timer.IsEnabled);
            Assert.AreEqual(0, timer.StartCount);
        }

        [TestMethod]
        public void CancelPending_QueuedStep_StopsTimerAndAllowsFreshRequest()
        {
            var timer = new TestTimer();
            var navigations = new List<NavigationDirection>();
            var pacer = new NavigationPacer(timer, navigations.Add);
            pacer.Request(NavigationDirection.Forwards);
            pacer.Request(NavigationDirection.Forwards);

            pacer.CancelPending();
            timer.RaiseTick();

            Assert.IsFalse(timer.IsEnabled);
            CollectionAssert.AreEqual(_initialNavigation, navigations);
            pacer.Request(NavigationDirection.Backwards);
            timer.RaiseTick();
            CollectionAssert.AreEqual(_reversedNavigation, navigations);
            Assert.IsFalse(timer.IsEnabled);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Navigate_CallbackSuspends_LeavesTimerStopped(bool suspendOnTick)
        {
            var timer = new TestTimer();
            var navigations = new List<NavigationDirection>();
            NavigationPacer? pacer = null;
            pacer = new NavigationPacer(timer, direction =>
            {
                navigations.Add(direction);
                if (navigations.Count == (suspendOnTick ? 2 : 1))
                {
                    pacer!.Suspend();
                }
            });

            pacer.Request(NavigationDirection.Forwards);
            if (suspendOnTick)
            {
                pacer.Request(NavigationDirection.Forwards);
                timer.RaiseTick();
            }

            Assert.IsTrue(pacer.IsSuspended);
            Assert.IsFalse(timer.IsEnabled);
            timer.RaiseTick();
            CollectionAssert.AreEqual(suspendOnTick ? _tickNavigation : _initialNavigation, navigations);
            Assert.AreEqual(1, timer.StartCount);
        }

        [TestMethod]
        public void Request_ReentrantCallback_CoalescesWithoutRestartingTimer()
        {
            var timer = new TestTimer();
            var navigations = new List<NavigationDirection>();
            NavigationPacer? pacer = null;
            pacer = new NavigationPacer(timer, direction =>
            {
                navigations.Add(direction);
                if (direction == NavigationDirection.Forwards)
                {
                    pacer!.Request(NavigationDirection.Backwards);
                }
            });

            pacer.Request(NavigationDirection.Forwards);
            CollectionAssert.AreEqual(_initialNavigation, navigations);
            timer.RaiseTick();
            CollectionAssert.AreEqual(_reversedNavigation, navigations);
            Assert.AreEqual(1, timer.StartCount);
            timer.RaiseTick();
            Assert.IsFalse(timer.IsEnabled);
        }

        private sealed class TestTimer : INavigationPacerTimer
        {
            public event Action? Tick;

            public bool IsEnabled { get; private set; }

            public int StartCount { get; private set; }

            public void Start()
            {
                IsEnabled = true;
                StartCount++;
            }

            public void Stop() => IsEnabled = false;

            // Also permits delivery of a tick already queued when the timer was stopped.
            public void RaiseTick() => Tick?.Invoke();
        }
    }
}
