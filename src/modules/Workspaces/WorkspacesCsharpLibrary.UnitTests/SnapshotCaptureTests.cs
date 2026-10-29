// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesEditor;

namespace WorkspacesCsharpLibrary.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed class SnapshotCaptureTests
{
    [TestMethod]
    public void PendingCaptureStaysVisibleAndCannotBeStartedTwice()
    {
        RunOnSta(() =>
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int captures = 0;
            int closes = 0;
            var window = CreateWindow(
                () =>
                {
                    captures++;
                    return completion.Task;
                },
                () => closes++);
            ExceptionDispatchInfo? failure = null;
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var capture = (Button)window.FindName("SnapshotButton");
                    var cancel = (Button)window.FindName("CancelButton");
                    var progress = (ProgressBar)window.FindName("CaptureProgress");
                    capture.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.AreEqual(1, captures);
                    Assert.IsTrue(window.IsVisible);
                    Assert.IsFalse(window.Topmost, "Capture must not obscure a subsequent error dialog.");
                    Assert.IsFalse(capture.IsEnabled);
                    Assert.IsFalse(cancel.IsEnabled);
                    Assert.AreEqual(Visibility.Visible, progress.Visibility);
                    capture.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    window.Close();
                    Assert.AreEqual(1, captures);
                    Assert.AreEqual(0, closes);
                    Assert.IsTrue(window.IsVisible);
                }
                catch (Exception error)
                {
                    failure = ExceptionDispatchInfo.Capture(error);
                }
                finally
                {
                    completion.TrySetResult();
                    if (captures == 0)
                    {
                        window.Close();
                    }
                }
            }));
            window.ShowDialog();
            failure?.Throw();
            Assert.AreEqual(1, captures);
            Assert.AreEqual(1, closes);
            Assert.IsFalse(window.IsVisible);
        });
    }

    [TestMethod]
    public void CancelBeforeCaptureDoesNotStartAnOperation()
    {
        RunOnSta(() =>
        {
            int captures = 0;
            int closes = 0;
            var window = CreateWindow(
                () =>
                {
                    captures++;
                    return Task.CompletedTask;
                },
                () => closes++);
            window.Dispatcher.BeginInvoke(new Action(() =>
                ((Button)window.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
            window.ShowDialog();
            Assert.AreEqual(0, captures);
            Assert.AreEqual(1, closes);
        });
    }

    private static SnapshotWindow CreateWindow(Func<Task> capture, Action cancel)
    {
        var constructor = typeof(SnapshotWindow).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(Func<Task>), typeof(Action)],
            modifiers: null)!;
        var window = (SnapshotWindow)constructor.Invoke([capture, cancel]);
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowActivated = false;
        window.Topmost = true;
        window.Left = -32000;
        window.Top = -32000;
        return window;
    }

    private static void RunOnSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                failure = ExceptionDispatchInfo.Capture(error);
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "Capture UI did not finish its bounded test.");
        failure?.Throw();
    }
}
