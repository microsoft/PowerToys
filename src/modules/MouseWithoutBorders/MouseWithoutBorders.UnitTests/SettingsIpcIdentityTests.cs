// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Security.Principal;

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
    public async Task ProductionAuthenticatedServerAcceptsReconnect()
    {
        var pipeName = $"PowerToys.MWB.v2.UnitTest.{Environment.ProcessId}.{Guid.NewGuid():N}";
        using var identity = WindowsIdentity.GetCurrent();
        var peerIdentity = new WindowsNamedPipePeerIdentityProvider(new AcceptSignatureVerifier()).GetIdentity(Environment.ProcessId);
        var policy = new NamedPipePeerPolicy
        {
            ExpectedSessionId = Process.GetCurrentProcess().SessionId,
            ExpectedUserSid = identity.User!.Value,
            ExpectedImagePath = peerIdentity.ImagePath,
            ExpectedFileVersion = peerIdentity.FileVersion,
            RequireMicrosoftSignature = false,
        };
        using var cancellation = new CancellationTokenSource();

        IpcChannel<TestRpcTarget>.StartAuthenticatedIpcServer(pipeName, identity.User, policy, cancellation.Token);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var client = await AuthenticatedNamedPipeClient.ConnectAsync(
                pipeName,
                policy,
                new NamedPipePeerAuthenticator(new WindowsNamedPipePeerIdentityProvider(new AcceptSignatureVerifier())),
                5000);
            Assert.IsTrue(client.IsConnected);
        }

        cancellation.Cancel();
    }

    private sealed class AcceptSignatureVerifier : IProcessSignatureVerifier
    {
        public bool HasTrustedMicrosoftSignature(string imagePath) => true;
    }

    private sealed class TestRpcTarget
    {
        public TestRpcTarget()
        {
        }

        public void Ping()
        {
        }
    }
}
