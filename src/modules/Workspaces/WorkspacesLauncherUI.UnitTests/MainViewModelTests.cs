// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Models;
using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class MainViewModelTests
    {
        private const string FirstId = "{A1111111-B111-C111-D111-E11111111111}";
        private const string SecondId = "{A2222222-B222-C222-D222-E22222222222}";

        [TestMethod]
        public void MissingConfirmationOwnerCannotBeWatched()
        {
            var notified = false;
            Assert.IsFalse(RunnerHelper.TryWaitForProcessExit(0, () => notified = true));
            Assert.IsFalse(notified);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void ConfirmationOnlyDoesNotNeedAStatusWindow(bool allow)
        {
            using var session = new WarningSession(confirmationOnly: true);
            session.Receive(Warning(FirstId));
            session.Drain();
            var window = session.Windows.Single();
            Assert.IsFalse(session.OwnerVisible);
            Assert.IsFalse(window.RunEnabled);
            window.Render();
            window.Choose(allow);
            CollectionAssert.AreEqual(new[] { Shown(FirstId), Response(FirstId, allow ? "run" : "skip") }, session.Messages);
        }

        [TestMethod]
        public void OriginalLaunchStatusNeedsNoMessageEnvelope()
        {
            using var session = new WarningSession();
            var appsListed = session.ViewModel.AppsListed;
            session.Receive(JsonSerializer.Serialize(new
            {
                processId = 1234,
                apps = new
                {
                    appLaunchInfos = Enumerable.Range(0, 6).Select(state => new
                    {
                        state,
                        application = new Dictionary<string, string>
                        {
                            ["application"] = "Example",
                            ["application-path"] = @"C:\Apps\Example.exe",
                        },
                    }),
                },
            }));
            session.Drain();
            Assert.AreSame(appsListed, session.ViewModel.AppsListed);
            CollectionAssert.AreEqual(Enumerable.Range(0, 6).ToArray(), appsListed.Select(app => (int)app.LaunchState).ToArray());
            var skipped = appsListed.Single(app => app.LaunchState == LaunchingState.Skipped);
            Assert.IsTrue(skipped.IsSkipped);
            Assert.IsFalse(skipped.Loading);
            Assert.IsFalse(skipped.ShowStateGlyph);
            Assert.IsFalse(string.IsNullOrWhiteSpace(skipped.StateDescription));
        }

        [TestMethod]
        public void PipeCallbackReturnsAndRunRequiresOneDisplayAcknowledgement()
        {
            using var session = new WarningSession();
            var receive = App.IPCMessageReceivedCallback;
            var pipeThread = new Thread(() => receive(Warning(FirstId))) { IsBackground = true };
            pipeThread.Start();
            Assert.IsTrue(pipeThread.Join(TimeSpan.FromSeconds(2)));
            Assert.AreEqual(0, session.Windows.Count);
            session.Drain();
            var window = session.Windows.Single();
            Assert.IsFalse(window.RunEnabled);
            window.Render();
            window.Render();
            Assert.IsTrue(window.RunEnabled);
            window.Choose(true);
            window.Choose(true);
            CollectionAssert.AreEqual(new[] { Shown(FirstId), Response(FirstId, "run") }, session.Messages);
        }

        [TestMethod]
        public void UnrenderedResultFailsClosed()
        {
            using var session = new WarningSession();
            session.Receive(Warning(FirstId));
            session.Drain();
            session.Windows.Single().Choose(true);
            CollectionAssert.AreEqual(new[] { Response(FirstId, "skip") }, session.Messages);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DismissSuppressesPendingOrRenderedRequestAndReplay(bool rendered)
        {
            using var session = new WarningSession();
            session.Receive(Warning(FirstId));
            if (rendered)
            {
                session.Drain();
                session.Windows.Single().Render();
            }

            session.Receive(Dismiss(FirstId));
            session.Drain();
            session.Receive(Warning(Guid.Parse(FirstId).ToString("D")));
            session.Drain();
            Assert.IsTrue(session.Windows.All(window => !window.IsVisible && !window.RunEnabled));
            CollectionAssert.AreEqual(rendered ? new[] { Shown(FirstId) } : Array.Empty<string>(), session.Messages);
        }

        [TestMethod]
        public void OverlappingRequestsAreSkippedOnceWithoutReplacingCurrentWarning()
        {
            using var session = new WarningSession();
            session.Receive(Warning(FirstId));
            session.Drain();
            var window = session.Windows.Single();
            session.Receive(Warning(FirstId));
            session.Receive(Warning(SecondId));
            session.Receive(Warning(SecondId));
            session.Drain();
            Assert.IsTrue(window.IsVisible);
            window.Render();
            window.Choose(true);
            CollectionAssert.AreEqual(new[] { Response(SecondId, "skip"), Shown(FirstId), Response(FirstId, "run") }, session.Messages);
        }

        [TestMethod]
        public void UnmatchedDismissDoesNotCloseCurrentWarningOrAllowReplay()
        {
            using var session = new WarningSession();
            session.Receive(Warning(FirstId));
            session.Drain();
            session.Receive(Dismiss(SecondId));
            session.Drain();
            var window = session.Windows.Single();
            Assert.IsTrue(window.IsVisible);
            window.Render();
            window.Choose(true);
            session.Receive(Warning(SecondId));
            session.Drain();
            Assert.AreEqual(1, session.Windows.Count);
            CollectionAssert.AreEqual(new[] { Shown(FirstId), Response(FirstId, "run") }, session.Messages);
        }

        [TestMethod]
        public void ResponseIsTerminalBeforeReentrantNextRequest()
        {
            using var session = new WarningSession();
            session.OnSend = message =>
            {
                if (message == Response(FirstId, "run"))
                {
                    session.Receive(Warning(SecondId));
                    session.Drain();
                }
            };
            session.Receive(Warning(FirstId));
            session.Drain();
            session.Windows[0].Render();
            session.Windows[0].Choose(true);
            Assert.IsTrue(session.Windows[1].IsVisible);
            session.Windows[1].Render();
            session.Windows[1].Choose(true);
            CollectionAssert.AreEqual(new[] { Shown(FirstId), Response(FirstId, "run"), Shown(SecondId), Response(SecondId, "run") }, session.Messages);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void CancelOrDisposeStopsPendingAndRenderedWarningsOnce(bool cancel, bool rendered)
        {
            using var session = new WarningSession();
            var receive = App.IPCMessageReceivedCallback;
            session.Receive(Warning(FirstId));
            if (rendered)
            {
                session.Drain();
                session.Windows.Single().Render();
            }
            else
            {
                session.Drain(highOnly: true);
            }

            if (cancel)
            {
                session.ViewModel.CancelLaunch();
                session.ViewModel.CancelLaunch();
            }
            else
            {
                session.ViewModel.Dispose();
            }

            session.ViewModel.Dispose();
            receive(Warning(SecondId));
            session.Drain();
            Assert.IsNull(App.IPCMessageReceivedCallback);
            Assert.IsTrue(session.Windows.All(window => !window.IsVisible && !window.RunEnabled));
            var expected = new List<string>();
            if (rendered)
            {
                expected.Add(Shown(FirstId));
            }

            expected.Add(cancel ? "cancel" : Response(FirstId, "skip"));
            CollectionAssert.AreEqual(expected, session.Messages);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void OwnerAvailabilityIsRecheckedBeforeShow(bool duringCreation, bool dispose)
        {
            using var session = new WarningSession();
            void MakeUnavailable()
            {
                if (dispose)
                {
                    session.ViewModel.Dispose();
                }
                else
                {
                    session.OwnerVisible = false;
                }
            }

            if (duringCreation)
            {
                session.OnCreate = MakeUnavailable;
            }

            session.Receive(Warning(FirstId));
            if (!duringCreation)
            {
                session.Drain(highOnly: true);
                MakeUnavailable();
            }

            session.Drain();
            Assert.IsTrue(session.Windows.All(window => !window.IsVisible));
            CollectionAssert.AreEqual(new[] { Response(FirstId, "skip") }, session.Messages);
        }

        [TestMethod]
        public void WarningCreationFailureSkipsAndDoesNotLeavePendingRequest()
        {
            using var session = new WarningSession();
            session.OnCreate = () => throw new InvalidOperationException("Test window creation failure.");
            session.Receive(Warning(FirstId));
            session.Drain();
            session.OnCreate = null;
            session.Receive(Warning(SecondId));
            session.Drain();
            session.Windows.Single().Choose(false);
            CollectionAssert.AreEqual(new[] { Response(FirstId, "skip"), Response(SecondId, "skip") }, session.Messages);
        }

        [DataTestMethod]
        [DataRow("not json")]
        [DataRow("[]")]
        [DataRow("{}")]
        [DataRow("{\"type\":5}")]
        [DataRow("{\"type\":\"unknown\"}")]
        [DataRow("{\"type\":\"dismiss-warning\",\"requestId\":null}")]
        [DataRow("{\"type\":\"dismiss-warning\",\"requestId\":\"invalid\"}")]
        [DataRow("{\"type\":\"dismiss-warning\",\"requestId\":\"00000000-0000-0000-0000-000000000000\"}")]
        [DataRow("{\"type\":\"elevation-warning\",\"requestId\":\"22222222-2222-2222-2222-222222222222\",\"appName\":5}")]
        [DataRow("{\"type\":\"elevation-warning\",\"requestId\":\"22222222-2222-2222-2222-222222222222\",\"appName\":\"Example\",\"path\":\"example.exe\",\"arguments\":\"\",\"reason\":\"unsigned\"}")]
        public void InvalidMessagesFailPendingWarningClosed(string message)
        {
            using var session = new WarningSession();
            session.Receive(Warning(FirstId));
            session.Receive(message);
            session.Drain();
            Assert.AreEqual(0, session.Windows.Count);
            CollectionAssert.AreEqual(new[] { Response(FirstId, "skip") }, session.Messages);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SendFailureClosesOwnerInsteadOfLeavingParentWaiting(bool responseFails)
        {
            using var session = new WarningSession();
            session.OnSend = message =>
            {
                if (message == (responseFails ? Response(FirstId, "run") : Shown(FirstId)))
                {
                    throw new InvalidOperationException("Test send failure.");
                }
            };
            session.Receive(Warning(FirstId));
            session.Drain();
            var window = session.Windows.Single();
            window.Render();
            window.Choose(true);
            Assert.IsFalse(session.OwnerVisible);
            Assert.IsNull(App.IPCMessageReceivedCallback);
            CollectionAssert.AreEqual(new[] { Shown(FirstId), Response(FirstId, responseFails ? "run" : "skip") }, session.Messages);
        }

        private static string Warning(string requestId) => JsonSerializer.Serialize(new
        {
            type = "elevation-warning",
            requestId,
            appName = "Example",
            path = @"C:\Apps\Example.exe",
            arguments = "--test",
            reason = "unsigned",
            status = "0x800B0100",
        });

        private static string Dismiss(string requestId) => JsonSerializer.Serialize(new { type = "dismiss-warning", requestId });

        private static string Shown(string requestId) => JsonSerializer.Serialize(new { type = "warning-shown", requestId });

        private static string Response(string requestId, string choice) => JsonSerializer.Serialize(new { type = "elevation-response", requestId, choice });

        private sealed class WarningSession : IDisposable
        {
            private readonly Queue<Action> _high = new Queue<Action>();
            private readonly Queue<Action> _normal = new Queue<Action>();

            internal WarningSession(bool confirmationOnly = false)
            {
                OwnerVisible = !confirmationOnly;
                ViewModel = new MainViewModel(
                    message =>
                    {
                        Messages.Add(message);
                        OnSend?.Invoke(message);
                    },
                    _ =>
                    {
                        OnCreate?.Invoke();
                        var window = new TestWarningWindow();
                        Windows.Add(window);
                        return window;
                    },
                    (action, high) => (high ? _high : _normal).Enqueue(action),
                    () => confirmationOnly || OwnerVisible,
                    () => OwnerVisible = false);
            }

            internal MainViewModel ViewModel { get; }

            internal bool OwnerVisible { get; set; }

            internal List<string> Messages { get; } = new List<string>();

            internal List<TestWarningWindow> Windows { get; } = new List<TestWarningWindow>();

            internal Action OnCreate { get; set; }

            internal Action<string> OnSend { get; set; }

            internal void Receive(string message) => App.IPCMessageReceivedCallback(message);

            internal void Drain(bool highOnly = false)
            {
                while (_high.Count != 0 || (!highOnly && _normal.Count != 0))
                {
                    (_high.Count != 0 ? _high : _normal).Dequeue()();
                }
            }

            public void Dispose() => ViewModel.Dispose();
        }

        private sealed class TestWarningWindow : ISignatureWarningWindow
        {
            public event EventHandler Rendered;

            public event EventHandler<bool> ChoiceMade;

            public bool IsVisible { get; private set; }

            internal bool RunEnabled { get; private set; }

            public void Show() => IsVisible = true;

            public void EnableRunChoice() => RunEnabled = true;

            public void DismissWithoutResponse()
            {
                IsVisible = false;
                RunEnabled = false;
            }

            internal void Choose(bool run) => ChoiceMade?.Invoke(this, run);

            internal void Render() => Rendered?.Invoke(this, EventArgs.Empty);
        }
    }
}
