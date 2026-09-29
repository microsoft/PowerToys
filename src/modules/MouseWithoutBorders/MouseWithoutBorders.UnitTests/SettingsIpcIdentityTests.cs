// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class SettingsIpcIdentityTests
{
    [TestMethod]
    public void CurrentUserProcessUsesItsOwnSid()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var processSid = identity.User!;
        var resolverCalled = false;

        var result = Program.ResolveSettingsIpcUserSid(
            processSid,
            42,
            _ =>
            {
                resolverCalled = true;
                return processSid;
            });

        Assert.AreEqual(processSid, result);
        Assert.IsFalse(resolverCalled);
    }

    [TestMethod]
    public void SystemProcessUsesInteractiveSessionSid()
    {
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var interactiveSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var resolvedSessionId = -1;

        var result = Program.ResolveSettingsIpcUserSid(
            systemSid,
            42,
            sessionId =>
            {
                resolvedSessionId = sessionId;
                return interactiveSid;
            });

        Assert.AreEqual(interactiveSid, result);
        Assert.AreEqual(42, resolvedSessionId);
    }

    [TestMethod]
    public void SystemProcessGrantsInteractiveUserQueryAccess()
    {
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var interactiveSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        SecurityIdentifier? grantedSid = null;

        Program.GrantSettingsIpcProcessQueryAccessIfNeeded(systemSid, interactiveSid, sid => grantedSid = sid);

        Assert.AreEqual(interactiveSid, grantedSid);
    }

    [TestMethod]
    public void UserProcessKeepsItsExistingProcessDacl()
    {
        var userSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var grantCount = 0;

        Program.GrantSettingsIpcProcessQueryAccessIfNeeded(userSid, userSid, _ => grantCount++);

        Assert.AreEqual(0, grantCount);
    }

    [TestMethod]
    public void MissingVerifierIsRejectedBeforeListenerStarts()
    {
        using var identity = WindowsIdentity.GetCurrent();

        Assert.ThrowsException<ArgumentNullException>(() =>
            IpcChannel<TestRpcTarget>.StartVerifiedIpcServer(
                $"PowerToys.MWB.v2.UnitTest.{Guid.NewGuid():N}",
                identity.User!,
                null!,
                CancellationToken.None));
    }

    [TestMethod]
    public async Task ProductionVerifiedServerAcceptsReconnect()
    {
        var pipeName = $"PowerToys.MWB.v2.UnitTest.{Environment.ProcessId}.{Guid.NewGuid():N}";
        using var identity = WindowsIdentity.GetCurrent();
        using var cancellation = new CancellationTokenSource();
        var executablePath = GetCurrentExecutablePath();
        var sessionId = Process.GetCurrentProcess().SessionId;

        TestRpcTarget.Reset();
        IpcChannel<TestRpcTarget>.StartVerifiedIpcServer(
            pipeName,
            identity.User!,
            stream => VerifyCurrentProcessClient(stream, Path.GetFileName(executablePath), identity.User!.Value, sessionId),
            cancellation.Token);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var client = await ConnectVerifiedClientAsync(
                pipeName,
                Path.GetFileName(executablePath),
                identity.User!.Value,
                sessionId);
            Assert.IsTrue(client.IsConnected);
            Assert.IsTrue(SpinWait.SpinUntil(() => TestRpcTarget.InstanceCount == attempt + 1, TimeSpan.FromSeconds(5)));
        }

        cancellation.Cancel();
    }

    [TestMethod]
    public async Task RejectedClientDoesNotDispatchBeforeVerification()
    {
        var pipeName = $"PowerToys.MWB.v2.UnitTest.{Environment.ProcessId}.{Guid.NewGuid():N}";
        using var identity = WindowsIdentity.GetCurrent();
        using var cancellation = new CancellationTokenSource();
        var executablePath = GetCurrentExecutablePath();
        var sessionId = Process.GetCurrentProcess().SessionId;
        var verifyCallCount = 0;

        TestRpcTarget.Reset();
        IpcChannel<TestRpcTarget>.StartVerifiedIpcServer(
            pipeName,
            identity.User!,
            stream =>
            {
                var verifyAttempt = Interlocked.Increment(ref verifyCallCount);
                return verifyAttempt == 1
                    ? "blocked-for-test"
                    : VerifyCurrentProcessClient(stream, Path.GetFileName(executablePath), identity.User!.Value, sessionId);
            },
            cancellation.Token);

        await using (var rejectedClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await rejectedClient.ConnectAsync(5000);
            Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref verifyCallCount) == 1, TimeSpan.FromSeconds(5)));
            Assert.AreEqual(0, TestRpcTarget.InstanceCount);
        }

        await using var acceptedClient = await ConnectVerifiedClientAsync(
            pipeName,
            Path.GetFileName(executablePath),
            identity.User!.Value,
            sessionId);
        Assert.IsTrue(SpinWait.SpinUntil(() => TestRpcTarget.InstanceCount == 1, TimeSpan.FromSeconds(5)));
        Assert.AreEqual(2, Volatile.Read(ref verifyCallCount));
        cancellation.Cancel();
    }

    [TestMethod]
    public async Task ProductionServerRecoversAfterPipeNameBecomesAvailable()
    {
        var pipeName = $"PowerToys.MWB.v2.UnitTest.{Environment.ProcessId}.{Guid.NewGuid():N}";
        using var identity = WindowsIdentity.GetCurrent();
        using var cancellation = new CancellationTokenSource();
        var executablePath = GetCurrentExecutablePath();
        var sessionId = Process.GetCurrentProcess().SessionId;
        var initialCreationFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using (var occupyingServer = RestrictedNamedPipeServer.Create(pipeName, identity.User!))
        {
            IpcChannel<TestRpcTarget>.StartVerifiedIpcServer(
                pipeName,
                identity.User!,
                stream => VerifyCurrentProcessClient(stream, Path.GetFileName(executablePath), identity.User!.Value, sessionId),
                _ => initialCreationFailure.TrySetResult(),
                cancellation.Token);
            await initialCreationFailure.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        await using var client = await ConnectVerifiedClientAsync(
            pipeName,
            Path.GetFileName(executablePath),
            identity.User!.Value,
            sessionId);

        Assert.IsTrue(client.IsConnected);
        cancellation.Cancel();
    }

    private static async Task<NamedPipeClientStream> ConnectVerifiedClientAsync(
        string pipeName,
        string expectedServerFileName,
        string expectedUserSid,
        int expectedSessionId)
    {
        var stream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await stream.ConnectAsync(5000);
            if (!NamedPipePeerVerification.TryVerifyServer(
                    stream,
                    expectedServerFileName,
                    expectedUserSid,
                    expectedSessionId,
                    allowLocalSystem: false,
                    out var rejectionReason))
            {
                throw new UnauthorizedAccessException($"Rejected named pipe server: {rejectionReason}");
            }

            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private static string VerifyCurrentProcessClient(
        NamedPipeServerStream stream,
        string expectedClientFileName,
        string expectedUserSid,
        int expectedSessionId)
    {
        return NamedPipePeerVerification.TryVerifyClient(
            stream,
            expectedClientFileName,
            expectedUserSid,
            expectedSessionId,
            out var rejectionReason)
            ? string.Empty
            : rejectionReason;
    }

    private static string GetCurrentExecutablePath()
    {
        return Process.GetCurrentProcess().MainModule?.FileName
            ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("The current process has no executable path.");
    }

    private sealed class TestRpcTarget
    {
        private static int _instanceCount;

        public TestRpcTarget()
        {
            Interlocked.Increment(ref _instanceCount);
        }

        public static int InstanceCount => Volatile.Read(ref _instanceCount);

        public static void Reset()
        {
            Interlocked.Exchange(ref _instanceCount, 0);
        }

        public void Ping()
        {
        }
    }
}
