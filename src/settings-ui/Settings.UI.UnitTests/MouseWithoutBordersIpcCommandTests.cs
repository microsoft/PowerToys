// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StreamJsonRpc;

namespace Microsoft.PowerToys.Settings.UI.UnitTests
{
    [TestClass]
    public sealed class MouseWithoutBordersIpcCommandTests
    {
        [DataTestMethod]
        [DataRow(typeof(IOException))]
        [DataRow(typeof(TimeoutException))]
        [DataRow(typeof(ConnectionLostException))]
        [DataRow(typeof(UnauthorizedAccessException))]
        [DataRow(typeof(RemoteInvocationException))]
        [DataRow(typeof(InvalidOperationException))]
        [DataRow(typeof(ObjectDisposedException))]
        public async Task ExpectedIpcFailureShowsErrorWithoutClearingInputsOrReplaying(Type exceptionType)
        {
            var pcName = "fixture-peer";
            var securityKey = "fixture-value";
            var invocationCount = 0;
            var successfulCompletions = 0;
            var errorNotifications = new List<bool>();
            var failure = exceptionType == typeof(RemoteInvocationException)
                ? new RemoteInvocationException("Fixture remote failure", -32603, (object)null)
                : (Exception)Activator.CreateInstance(exceptionType, "Fixture IPC failure")!;

            var succeeded = await MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                () =>
                {
                    invocationCount++;
                    return Task.FromException(failure);
                },
                errorNotifications.Add,
                () =>
                {
                    successfulCompletions++;
                    pcName = string.Empty;
                    securityKey = string.Empty;
                });

            Assert.IsFalse(succeeded);
            Assert.AreEqual(1, invocationCount);
            Assert.AreEqual(0, successfulCompletions);
            Assert.AreEqual("fixture-peer", pcName);
            Assert.AreEqual("fixture-value", securityKey);
            Assert.AreEqual(2, errorNotifications.Count);
            Assert.IsFalse(errorNotifications[0]);
            Assert.IsTrue(errorNotifications[1]);
        }

        [TestMethod]
        public async Task SynchronousIpcFailureIsHandledWithoutReplay()
        {
            var errorVisible = false;
            var invocationCount = 0;

            var succeeded = await MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                () =>
                {
                    invocationCount++;
                    throw new IOException("Fixture IPC failure");
                },
                visible => errorVisible = visible);

            Assert.IsFalse(succeeded);
            Assert.IsTrue(errorVisible);
            Assert.AreEqual(1, invocationCount);
        }

        [TestMethod]
        public async Task ConnectInputsAreClearedOnlyAfterAcknowledgement()
        {
            var acknowledgement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pcName = "fixture-peer";
            var securityKey = "fixture-value";
            var errorVisible = true;
            var invocationCount = 0;
            var successfulCompletions = 0;
            var operation = MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                () =>
                {
                    invocationCount++;
                    return acknowledgement.Task;
                },
                visible => errorVisible = visible,
                () =>
                {
                    successfulCompletions++;
                    pcName = string.Empty;
                    securityKey = string.Empty;
                });
            try
            {
                Assert.IsFalse(operation.IsCompleted);
                Assert.IsFalse(errorVisible);
                Assert.AreEqual("fixture-peer", pcName);
                Assert.AreEqual("fixture-value", securityKey);
                Assert.AreEqual(0, successfulCompletions);

                acknowledgement.SetResult();

                Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.AreEqual(string.Empty, pcName);
                Assert.AreEqual(string.Empty, securityKey);
                Assert.AreEqual(1, successfulCompletions);
                Assert.AreEqual(1, invocationCount);
                Assert.IsFalse(errorVisible);
            }
            finally
            {
                acknowledgement.TrySetResult();
            }
        }

        [TestMethod]
        public async Task LostAcknowledgementRetainsInputsWithoutReplay()
        {
            var acknowledgement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var pcName = "fixture-peer";
            var securityKey = "fixture-value";
            var errorVisible = false;
            var invocationCount = 0;
            var operation = MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                () =>
                {
                    invocationCount++;
                    return acknowledgement.Task;
                },
                visible => errorVisible = visible,
                () =>
                {
                    pcName = string.Empty;
                    securityKey = string.Empty;
                });
            try
            {
                Assert.IsFalse(operation.IsCompleted);
                acknowledgement.SetException(new ConnectionLostException("Fixture lost acknowledgement"));

                Assert.IsFalse(await operation.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.IsTrue(errorVisible);
                Assert.AreEqual("fixture-peer", pcName);
                Assert.AreEqual("fixture-value", securityKey);
                Assert.AreEqual(1, invocationCount);
            }
            finally
            {
                acknowledgement.TrySetResult();
            }
        }

        [TestMethod]
        public async Task SuccessfulCommandClearsPreviousError()
        {
            var errorVisible = false;
            Assert.IsFalse(await MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                () => Task.FromException(new TimeoutException("Fixture IPC timeout")),
                visible => errorVisible = visible));
            Assert.IsTrue(errorVisible);

            Assert.IsTrue(await MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                () => Task.CompletedTask,
                visible => errorVisible = visible));
            Assert.IsFalse(errorVisible);
        }

        [TestMethod]
        public async Task UnexpectedCommandFailureShowsErrorWithoutEscapingAsyncCommand()
        {
            var errorVisible = true;
            var failure = new InvalidOperationException("Fixture unexpected failure");
            var succeeded = await MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                () => Task.FromException(failure),
                visible => errorVisible = visible);

            Assert.IsFalse(succeeded);
            Assert.IsTrue(errorVisible);
        }

        [TestMethod]
        public async Task UnexpectedCancellationShowsError()
        {
            var errorVisible = true;
            Assert.IsFalse(await MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                () => Task.FromException(new OperationCanceledException("Fixture unexpected cancellation")),
                visible => errorVisible = visible));

            Assert.IsTrue(errorVisible);
        }

        [TestMethod]
        public async Task UnresponsiveCommandTimesOutDisposesConnectionAndReleasesSemaphore()
        {
            var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var connection = new TestConnection();
            var invocationCount = 0;
            var errorVisible = false;
            try
            {
                Assert.AreEqual(TimeSpan.FromSeconds(15), MouseWithoutBordersViewModel.SettingsSyncRequests.Timeout);
                Assert.IsFalse(await MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                    () => MouseWithoutBordersViewModel.SettingsSyncRequests.ExecuteAsync(
                        () => Task.FromResult(connection),
                        _ =>
                        {
                            invocationCount++;
                            return pending.Task;
                        },
                        TimeSpan.FromMilliseconds(50)),
                    visible => errorVisible = visible).WaitAsync(TimeSpan.FromSeconds(5)));

                Assert.IsTrue(errorVisible);
                Assert.IsTrue(connection.Disposed);
                Assert.AreEqual(1, invocationCount);
                Assert.IsFalse(pending.Task.IsCompleted, "A timeout must not pretend the server acknowledged the command.");

                var nextConnection = new TestConnection();
                await MouseWithoutBordersViewModel.SettingsSyncRequests.ExecuteAsync(
                    () => Task.FromResult(nextConnection),
                    _ => Task.CompletedTask,
                    TimeSpan.FromSeconds(1)).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.IsTrue(nextConnection.Disposed, "The next request must acquire the released semaphore and complete.");
            }
            finally
            {
                pending.TrySetResult();
            }
        }

        private sealed class TestConnection : IDisposable
        {
            public bool Disposed { get; private set; }

            public void Dispose() => Disposed = true;
        }

        [TestMethod]
        public async Task UiSuccessCallbackFailureIsNotHandledAsIpcFailure()
        {
            var errorVisible = true;
            await Assert.ThrowsExceptionAsync<IOException>(() => MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                () => Task.CompletedTask,
                visible => errorVisible = visible,
                () => throw new IOException("Fixture UI callback failure")));

            Assert.IsFalse(errorVisible);
        }
    }
}
