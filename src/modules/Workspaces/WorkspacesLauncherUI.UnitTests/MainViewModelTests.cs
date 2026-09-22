// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Models;
using WorkspacesLauncherUI.Properties;
using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI.UnitTests
{
    [TestClass]
    public sealed class MainViewModelTests
    {
        private const string FirstId = "{A1111111-B111-C111-D111-E11111111111}";
        private const string SecondId = "{A2222222-B222-C222-D222-E22222222222}";

        [TestMethod]
        public void OriginalLaunchStatusNeedsNoMessageEnvelope()
        {
            Run(session =>
            {
                var changed = false;
                session.ViewModel.PropertyChanged += (_, e) => changed |= e.PropertyName == nameof(MainViewModel.AppsListed);
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

                Assert.IsTrue(changed);
                CollectionAssert.AreEqual(Enumerable.Range(0, 6).ToArray(), session.ViewModel.AppsListed.Select(app => (int)app.LaunchState).ToArray());
                Assert.IsTrue(session.ViewModel.AppsListed.All(app => app.Name == "Example" && app.AppPath == @"C:\Apps\Example.exe"));
                var skipped = session.ViewModel.AppsListed.Single(app => app.LaunchState == LaunchingState.Skipped);
                Assert.IsTrue(skipped.IsSkipped);
                Assert.IsFalse(skipped.Loading);
                Assert.IsFalse(skipped.ShowStateGlyph);
                Assert.AreEqual(Resources.LaunchStateSkipped, skipped.StateDescription);
                Assert.AreEqual(0, session.Windows.Count);
                Assert.AreEqual(0, session.Messages.Count);
            });
        }

        [TestMethod]
        public void PipeCallbackReturnsAndRunRequiresOneQueuedDisplayAcknowledgement()
        {
            Run(session =>
            {
                session.OnLoaded = window =>
                {
                    Assert.IsFalse(window.RunButton.IsEnabled);
                    window.ClickRun();
                    Assert.IsTrue(window.IsVisible);
                    Assert.AreEqual(0, session.Messages.Count);

                    window.Render();
                    window.Render();
                    Assert.IsTrue(window.RunButton.IsEnabled);
                    CollectionAssert.AreEqual(new[] { Shown(FirstId) }, session.Messages);
                    window.ClickRun();
                };
                ExceptionDispatchInfo failure = null;
                var receive = App.IPCMessageReceivedCallback;
                var pipeThread = new Thread(() =>
                {
                    try
                    {
                        receive(Warning(FirstId));
                    }
                    catch (Exception exception)
                    {
                        failure = ExceptionDispatchInfo.Capture(exception);
                    }
                })
                {
                    IsBackground = true,
                };
                pipeThread.Start();
                Assert.IsTrue(pipeThread.Join(TimeSpan.FromSeconds(2)), "The pipe callback must not wait for the UI dispatcher or modal dialog.");
                failure?.Throw();
                Assert.AreEqual(0, session.Windows.Count);
                session.Drain();

                CollectionAssert.AreEqual(new[] { Shown(FirstId), Response(FirstId, "run") }, session.Messages);
            });
        }

        [DataTestMethod]
        [DataRow("close")]
        [DataRow("cancel")]
        [DataRow("unrendered-result")]
        public void EarlyCloseOrCancelSkipsWithoutADisplayAcknowledgement(string action)
        {
            Run(session =>
            {
                session.OnLoaded = window =>
                {
                    Assert.IsFalse(window.RunButton.IsEnabled);
                    switch (action)
                    {
                        case "cancel":
                            var skip = (Button)window.FindName("SkipButton");
                            Assert.IsTrue(skip.IsCancel, "Escape uses the same WPF dialog-cancel action as Skip.");
                            Assert.IsTrue(skip.IsDefault);
                            var peer = new ButtonAutomationPeer(skip);
                            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
                            session.Drain();
                            Assert.IsFalse(window.IsVisible);
                            break;
                        case "unrendered-result":
                            window.DialogResult = true;
                            break;
                        default:
                            window.Close();
                            break;
                    }
                };
                session.Receive(Warning(FirstId));
                session.Drain();

                CollectionAssert.AreEqual(new[] { Response(FirstId, "skip") }, session.Messages);
            });
        }

        [TestMethod]
        public void DismissBeforeScheduledShowSuppressesTheRequestAndItsReplay()
        {
            Run(session =>
            {
                session.Receive(Warning(FirstId));
                session.Receive(Dismiss(FirstId));
                session.Drain();
                session.Receive(Warning(Guid.Parse(FirstId).ToString("D")));
                session.Drain();

                Assert.AreEqual(0, session.Windows.Count);
                Assert.AreEqual(0, session.Messages.Count);
                session.Receive(Warning(SecondId));
                session.Drain();
                Assert.AreEqual(1, session.Windows.Count);
                CollectionAssert.AreEqual(new[] { Response(SecondId, "skip") }, session.Messages);
            });
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void DismissClosesTheWarningEvenInsideAnotherModalDispatcher(bool nestedDialog)
        {
            Run(session =>
            {
                session.OnLoaded = window =>
                {
                    window.Render();
                    void DismissCurrent()
                    {
                        session.Receive(Dismiss(FirstId));
                        session.Drain();
                        Assert.IsFalse(window.IsVisible);
                        Assert.IsFalse(window.RunButton.IsEnabled);
                        window.ClickRun();
                    }

                    if (nestedDialog)
                    {
                        // Copy-details errors likewise enter another modal loop with the warning disabled.
                        var detailsError = new Window
                        {
                            Owner = window,
                            ShowInTaskbar = false,
                            ShowActivated = false,
                            Left = -32000,
                            Top = -32000,
                            Width = 300,
                            Height = 120,
                            Content = new TextBlock { Text = "Test copy-details error." },
                        };
                        detailsError.Loaded += (_, _) => detailsError.Dispatcher.BeginInvoke(
                            DispatcherPriority.Normal,
                            new Action(() => session.InvokeAndClose(detailsError, DismissCurrent)));
                        detailsError.ShowDialog();
                        Assert.IsFalse(detailsError.IsVisible);
                    }
                    else
                    {
                        DismissCurrent();
                    }
                };
                session.Receive(Warning(FirstId));
                session.Drain();
                session.Receive(Dismiss(FirstId));
                session.Receive(Warning(FirstId));
                session.Drain();

                Assert.AreEqual(1, session.Windows.Count);
                CollectionAssert.AreEqual(new[] { Shown(FirstId) }, session.Messages);
            });
        }

        [TestMethod]
        public void DismissClosesTheActualCopyDetailsErrorDialog()
        {
            Run(session =>
            {
                session.OnLoaded = window =>
                {
                    window.Render();
                    window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => session.Receive(Dismiss(FirstId))));
                    window.ShowCopyDetailsError();
                    Assert.IsFalse(window.IsVisible);
                    Assert.IsFalse(window.RunButton.IsEnabled);
                    window.ClickRun();
                };
                session.Receive(Warning(FirstId));
                session.Drain();

                Assert.AreEqual(1, session.Windows.Count);
                CollectionAssert.AreEqual(new[] { Shown(FirstId) }, session.Messages);
            });
        }

        [TestMethod]
        public void OverlappingRequestsAreSkippedOnceWithoutReplacingTheCurrentWarning()
        {
            Run(session =>
            {
                session.OnLoaded = window =>
                {
                    session.Receive(Warning(FirstId));
                    session.Receive(Warning(SecondId));
                    session.Receive(Warning(SecondId));
                    session.Drain();
                    Assert.IsTrue(window.IsVisible);
                    CollectionAssert.AreEqual(new[] { Response(SecondId, "skip") }, session.Messages);
                    window.Render();
                    window.ClickRun();
                };
                session.Receive(Warning(FirstId));
                session.Drain();
                session.Receive(Warning(FirstId));
                session.Receive(Warning(SecondId));
                session.Drain();

                Assert.AreEqual(1, session.Windows.Count);
                CollectionAssert.AreEqual(new[] { Response(SecondId, "skip"), Shown(FirstId), Response(FirstId, "run") }, session.Messages);
            });
        }

        [TestMethod]
        public void UnmatchedDismissDoesNotCloseTheCurrentWarningOrAllowLaterReplay()
        {
            Run(session =>
            {
                session.OnLoaded = window =>
                {
                    window.Render();
                    session.Receive(Dismiss(SecondId));
                    session.Drain();
                    Assert.IsTrue(window.IsVisible);
                    window.ClickRun();
                };
                session.Receive(Warning(FirstId));
                session.Drain();
                session.Receive(Warning(SecondId));
                session.Drain();

                Assert.AreEqual(1, session.Windows.Count);
                CollectionAssert.AreEqual(new[] { Shown(FirstId), Response(FirstId, "run") }, session.Messages);
            });
        }

        [TestMethod]
        public void ResponseIsTerminalBeforeSendingAndCannotClearTheNextRequest()
        {
            Run(session =>
            {
                session.OnLoaded = window =>
                {
                    window.Render();
                    window.ClickRun();
                };
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
                session.Receive(Warning(FirstId));
                session.Receive(Warning(SecondId));
                session.Drain();

                Assert.AreEqual(2, session.Windows.Count);
                CollectionAssert.AreEqual(
                    new[] { Shown(FirstId), Response(FirstId, "run"), Shown(SecondId), Response(SecondId, "run") },
                    session.Messages);
            });
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void CancelOrDisposeStopsPendingAndRenderedWarningsOnce(bool cancel, bool rendered)
        {
            Run(session =>
            {
                var receive = App.IPCMessageReceivedCallback;
                void Stop()
                {
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
                }

                session.OnLoaded = window =>
                {
                    window.Render();
                    Stop();
                    Assert.IsFalse(window.IsVisible);
                    Assert.IsFalse(window.RunButton.IsEnabled);
                    window.ClickRun();
                };
                session.Receive(Warning(FirstId));
                if (!rendered)
                {
                    session.Drain(DispatcherPriority.Send);
                    Stop();
                }

                session.Drain();
                Assert.AreEqual(rendered ? 1 : 0, session.Windows.Count);
                var expected = new List<string>();
                if (rendered)
                {
                    expected.Add(Shown(FirstId));
                }

                expected.Add(cancel ? "cancel" : Response(FirstId, "skip"));
                CollectionAssert.AreEqual(expected, session.Messages);
            });
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void OwnerVisibilityAndDisposalAreRecheckedImmediatelyBeforeShow(bool duringCreation, bool dispose)
        {
            Run(session =>
            {
                void MakeUnavailable()
                {
                    if (dispose)
                    {
                        session.ViewModel.Dispose();
                    }
                    else
                    {
                        session.Owner.Hide();
                    }
                }

                if (duringCreation)
                {
                    session.OnCreate = MakeUnavailable;
                }

                session.Receive(Warning(FirstId));
                if (!duringCreation)
                {
                    session.Drain(DispatcherPriority.Send);
                    MakeUnavailable();
                }

                session.Drain();
                Assert.IsTrue(session.Windows.All(window => !window.WasLoaded));
                CollectionAssert.AreEqual(new[] { Response(FirstId, "skip") }, session.Messages);
            });
        }

        [TestMethod]
        public void WarningCreationFailureSkipsAndDoesNotLeaveAPendingRequest()
        {
            Run(session =>
            {
                session.OnCreate = () => throw new InvalidOperationException("Test window creation failure.");
                session.Receive(Warning(FirstId));
                session.Drain();
                session.OnCreate = null;
                session.Receive(Warning(SecondId));
                session.Drain();

                Assert.AreEqual(1, session.Windows.Count);
                CollectionAssert.AreEqual(new[] { Response(FirstId, "skip"), Response(SecondId, "skip") }, session.Messages);
            });
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
        public void InvalidMessagesFailThePendingWarningClosed(string message)
        {
            Run(session =>
            {
                session.Receive(Warning(FirstId));
                session.Receive(message);
                session.Drain();

                Assert.AreEqual(0, session.Windows.Count);
                CollectionAssert.AreEqual(new[] { Response(FirstId, "skip") }, session.Messages);
            });
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SendFailureClosesTheOwnerInsteadOfLeavingTheParentWaiting(bool responseFails)
        {
            Run(session =>
            {
                session.OnSend = message =>
                {
                    if (message == (responseFails ? Response(FirstId, "run") : Shown(FirstId)))
                    {
                        throw new InvalidOperationException("Test send failure.");
                    }
                };
                session.OnLoaded = window =>
                {
                    window.Render();
                    if (!responseFails)
                    {
                        Assert.IsFalse(window.RunButton.IsEnabled);
                    }

                    window.ClickRun();
                };
                session.Receive(Warning(FirstId));
                session.Drain();

                Assert.IsFalse(session.Owner.IsVisible, "The owned UI must exit if its response cannot be queued.");
                Assert.IsNull(App.IPCMessageReceivedCallback);
                CollectionAssert.AreEqual(
                    new[] { Shown(FirstId), Response(FirstId, responseFails ? "run" : "skip") },
                    session.Messages,
                    "These are send attempts, including the injected failure; there must be no retry or stale response.");
            });
        }

        private static string Warning(string requestId)
        {
            return JsonSerializer.Serialize(new
            {
                type = "elevation-warning",
                requestId,
                appName = "Example",
                path = @"C:\Apps\Example.exe",
                arguments = "--test",
                reason = "unsigned",
                status = "0x800B0100",
            });
        }

        private static string Dismiss(string requestId) => JsonSerializer.Serialize(new { type = "dismiss-warning", requestId });

        private static string Shown(string requestId) => JsonSerializer.Serialize(new { type = "warning-shown", requestId });

        private static string Response(string requestId, string choice) => JsonSerializer.Serialize(new { type = "elevation-response", requestId, choice });

        private static void Run(Action<WarningSession> test)
        {
            SignatureWarningLayoutTests.RunOnSta(() =>
            {
                using var session = new WarningSession();
                test(session);
            });
        }

        private sealed class WarningSession : IDisposable
        {
            private ExceptionDispatchInfo _failure;

            internal WarningSession()
            {
                ViewModel = new MainViewModel(
                    message =>
                    {
                        Messages.Add(message);
                        OnSend?.Invoke(message);
                    },
                    request =>
                    {
                        OnCreate?.Invoke();
                        var window = new TestWarningWindow(request);
                        Windows.Add(window);

                        // Let WPF finish Loaded before a test opens another modal window.
                        window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(
                            DispatcherPriority.Normal,
                            new Action(() => InvokeAndClose(window, () => OnLoaded?.Invoke(window))));
                        return window;
                    });
                Owner = new Window
                {
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Left = -32000,
                    Top = -32000,
                    Width = 1,
                    Height = 1,
                };
                ViewModel.SetSnapshotWindow(Owner);
                Owner.Closing += (_, _) => ViewModel.Dispose();
                Owner.Show();
            }

            internal MainViewModel ViewModel { get; }

            internal Window Owner { get; }

            internal List<string> Messages { get; } = new List<string>();

            internal List<TestWarningWindow> Windows { get; } = new List<TestWarningWindow>();

            internal Action<TestWarningWindow> OnLoaded { get; set; }

            internal Action OnCreate { get; set; }

            internal Action<string> OnSend { get; set; }

            internal void Receive(string message) => App.IPCMessageReceivedCallback(message);

            internal void Drain(DispatcherPriority priority = DispatcherPriority.ApplicationIdle)
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(priority, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                _failure?.Throw();
            }

            internal void InvokeAndClose(Window window, Action action)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    // Do not let the production error boundary turn a callback assertion into a passing test.
                    _failure ??= ExceptionDispatchInfo.Capture(exception);
                }
                finally
                {
                    window.Close();
                }
            }

            public void Dispose()
            {
                ViewModel.Dispose();
                foreach (var window in Windows)
                {
                    window.Close();
                }

                Owner.Close();
            }
        }

        private sealed class TestWarningWindow : SignatureWarningWindow
        {
            internal TestWarningWindow(SignatureWarningRequest request)
                : base(request)
            {
                ShowActivated = false;
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = -32000;
                Top = -32000;
                Loaded += (_, _) => WasLoaded = true;
            }

            internal bool WasLoaded { get; private set; }

            internal Button RunButton => (Button)FindName("RunAnywayButton");

            internal void ClickRun() => RunButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            internal void Render() => base.OnContentRendered(EventArgs.Empty);

            protected override void OnContentRendered(EventArgs e)
            {
                // Tests explicitly raise the rendering event instead of depending on compositor timing.
            }
        }
    }
}
