// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Microsoft.PowerToys.Settings.UI.UnitTests
{
    [TestClass]
    public sealed class MouseWithoutBordersIpcSecurityTests
    {
        [TestMethod]
        public void PipeNameIsStableAndSessionQualified()
        {
            Assert.AreEqual(
                "PowerToys.MouseWithoutBorders.v2.SettingsSync.Session.42",
                MouseWithoutBordersIpc.GetSettingsSyncPipeName(42));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => MouseWithoutBordersIpc.GetSettingsSyncPipeName(-1));
        }

        [TestMethod]
        public void PackagedExecutablePathsMatchInstallerLayout()
        {
            var installDirectory = Path.GetFullPath(Path.Combine("TestInstall", $"PowerToys-{Guid.NewGuid():N}"));
            var settingsDirectory = Path.Combine(installDirectory, "WinUI3Apps");

            Assert.AreEqual(
                Path.Combine(settingsDirectory, "PowerToys.Settings.exe"),
                MouseWithoutBordersIpc.GetSettingsExecutablePath(installDirectory));
            Assert.AreEqual(
                Path.Combine(installDirectory, "PowerToys.MouseWithoutBorders.exe"),
                MouseWithoutBordersIpc.GetMouseWithoutBordersExecutablePath(settingsDirectory + Path.DirectorySeparatorChar));
            Assert.ThrowsException<ArgumentException>(
                () => MouseWithoutBordersIpc.GetMouseWithoutBordersExecutablePath(installDirectory));
        }

        [TestMethod]
        public void PolicyCapturesExpectedExecutablePathAndVersion()
        {
            var identity = GetCurrentIdentity();

            var policy = MouseWithoutBordersIpcPolicy.CreateMwbServerPolicy(
                identity.ImagePath,
                identity.SessionId,
                identity.UserSid,
                allowLocalSystem: false);

            Assert.AreEqual(Path.GetFullPath(identity.ImagePath), policy.ExpectedImagePath);
            Assert.AreEqual(identity.FileVersion, policy.ExpectedFileVersion);
        }

        [TestMethod]
        public async Task LegitimateSameSessionConnectionIsAccepted()
        {
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;

            var identity = GetCurrentIdentity();
            var result = CreateRealAuthenticator().AuthenticateClient(server, CreatePolicy(identity));

            Assert.IsTrue(result.Accepted, result.ReasonCode);
        }

        [TestMethod]
        public async Task UnauthorizedLocalClientIsRejectedBeforeDispatch()
        {
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;

            var identity = GetCurrentIdentity();
            var policy = CreatePolicy(identity);
            policy = CopyPolicy(policy, expectedImagePath: Path.Combine(Path.GetDirectoryName(identity.ImagePath)!, "not-settings.exe"));

            var result = CreateRealAuthenticator().AuthenticateClient(server, policy);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("wrong-image", result.ReasonCode);
        }

        [TestMethod]
        public async Task RealProcessTokenWithUnexpectedSidIsRejectedBeforeDispatch()
        {
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;

            var identity = GetCurrentIdentity();
            var policy = CopyPolicy(
                CreatePolicy(identity),
                expectedUserSid: new SecurityIdentifier(WellKnownSidType.AnonymousSid, null).Value);
            var dispatchCount = 0;

            var result = CreateRealAuthenticator().AuthenticateClientAndExecute(server, policy, () => dispatchCount++);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("wrong-user", result.ReasonCode);
            Assert.AreEqual(0, dispatchCount);
        }

        [TestMethod]
        public async Task FakeServerAndPipeSquattingAreRejected()
        {
            var pipeName = UniquePipeName();
            using var currentIdentity = WindowsIdentity.GetCurrent();
            var identity = GetCurrentIdentity();

            await using (var fakeServer = RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!))
            {
                Assert.ThrowsException<Win32Exception>(() => RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!));

                var waitTask = fakeServer.WaitForConnectionAsync();
                var policy = CopyPolicy(CreatePolicy(identity), expectedImagePath: Path.Combine(Path.GetDirectoryName(identity.ImagePath)!, "PowerToys.MouseWithoutBorders.exe"));
                await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(
                    () => AuthenticatedNamedPipeClient.ConnectAsync(pipeName, policy, CreateRealAuthenticator(), 5000));
                await waitTask;
            }

            await using var legitimateServer = RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!);
            var legitimateWaitTask = legitimateServer.WaitForConnectionAsync();
            await using var legitimateClient = await AuthenticatedNamedPipeClient.ConnectAsync(
                pipeName,
                CreatePolicy(identity),
                CreateRealAuthenticator(),
                5000);
            await legitimateWaitTask;
            Assert.IsTrue(legitimateClient.IsConnected);
        }

        [TestMethod]
        public void PipeDaclAllowsOnlyExpectedUserAndLocalSystem()
        {
            using var currentIdentity = WindowsIdentity.GetCurrent();
            using var server = RestrictedNamedPipeServer.Create(UniquePipeName(), currentIdentity.User!);
            var expectedSids = new[]
            {
                currentIdentity.User!,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            };
            var accessRules = server.GetAccessControl().GetAccessRules(true, false, typeof(SecurityIdentifier));

            Assert.AreEqual(expectedSids.Length, accessRules.Count);
            foreach (AuthorizationRule rule in accessRules)
            {
                var pipeRule = (PipeAccessRule)rule;
                Assert.AreEqual(AccessControlType.Allow, pipeRule.AccessControlType);
                Assert.AreEqual(
                    PipeAccessRights.FullControl,
                    pipeRule.PipeAccessRights & PipeAccessRights.FullControl);
                Assert.IsTrue(Array.Exists(expectedSids, sid => sid.Equals(pipeRule.IdentityReference)));
            }
        }

        [TestMethod]
        public void DifferentRdpSessionIdentityIsRejected()
        {
            var identity = GetCurrentIdentity();
            var provider = new FakeIdentityProvider { Identity = identity };
            var policy = CopyPolicy(CreatePolicy(identity), expectedSessionId: identity.SessionId + 1);

            var result = new NamedPipePeerAuthenticator(provider).Authenticate(identity.ProcessId, policy);

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("wrong-session", result.ReasonCode);
        }

        [TestMethod]
        public void ProcessCreationTimeSeparatesCacheEntries()
        {
            var identity = GetCurrentIdentity();
            var provider = new FakeIdentityProvider { Identity = identity };
            var authenticator = new NamedPipePeerAuthenticator(provider);
            var policy = CreatePolicy(identity);

            Assert.IsTrue(authenticator.Authenticate(identity.ProcessId, policy).Accepted);

            provider.Identity = CopyIdentity(
                identity,
                creationTimeUtcTicks: identity.CreationTimeUtcTicks + 1,
                sessionId: identity.SessionId + 1);
            var restartedProcessResult = authenticator.Authenticate(identity.ProcessId, policy);

            Assert.IsFalse(restartedProcessResult.Accepted);
            Assert.AreEqual("wrong-session", restartedProcessResult.ReasonCode);
        }

        [TestMethod]
        public async Task ServerRestartAllowsAuthenticatedReconnect()
        {
            var pipeName = UniquePipeName();
            var identity = GetCurrentIdentity();
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var server = RestrictedNamedPipeServer.Create(pipeName, new SecurityIdentifier(identity.UserSid));
                var waitTask = server.WaitForConnectionAsync();
                await using var client = await AuthenticatedNamedPipeClient.ConnectAsync(
                    pipeName,
                    CreatePolicy(identity),
                    CreateRealAuthenticator(),
                    5000);
                await waitTask;
                Assert.IsTrue(client.IsConnected);
            }
        }

        [TestMethod]
        public async Task RejectedConnectionDoesNotReplayMutation()
        {
            var identity = GetCurrentIdentity();
            var provider = new FakeIdentityProvider { Identity = identity };
            var authenticator = new NamedPipePeerAuthenticator(provider);
            var mutationCount = 0;

            var rejectedPolicy = CopyPolicy(CreatePolicy(identity), expectedSessionId: identity.SessionId + 1);
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;

            authenticator.AuthenticateClientAndExecute(server, rejectedPolicy, () => mutationCount++);
            authenticator.AuthenticateClientAndExecute(server, CreatePolicy(identity), () => mutationCount++);

            Assert.AreEqual(1, mutationCount);
        }

        [TestMethod]
        public void SystemAndSignatureCasesArePolicyControlled()
        {
            var identity = GetCurrentIdentity();
            var systemIdentity = CopyIdentity(
                identity,
                userSid: new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
                hasTrustedMicrosoftSignature: false);
            var provider = new FakeIdentityProvider { Identity = systemIdentity };
            var authenticator = new NamedPipePeerAuthenticator(provider);

            var labPolicy = CopyPolicy(CreatePolicy(identity), allowLocalSystem: true, requireMicrosoftSignature: false);
            Assert.IsTrue(authenticator.Authenticate(identity.ProcessId, labPolicy).Accepted);

            var releasePolicy = CopyPolicy(labPolicy, requireMicrosoftSignature: true);
            var releaseResult = authenticator.Authenticate(identity.ProcessId, releasePolicy);
            Assert.IsFalse(releaseResult.Accepted);
            Assert.AreEqual("untrusted-signature", releaseResult.ReasonCode);
        }

        [TestMethod]
        public void IdentityChecksRejectInCheapToExpensiveOrder()
        {
            var identity = GetCurrentIdentity();
            var policy = CreatePolicy(identity);
            var provider = new DeferredSignatureIdentityProvider
            {
                Identity = CopyIdentity(
                    identity,
                    sessionId: identity.SessionId + 1,
                    userSid: new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
                    hasTrustedMicrosoftSignature: false),
            };
            var authenticator = new NamedPipePeerAuthenticator(provider);

            var result = authenticator.Authenticate(identity.ProcessId, CopyPolicy(policy, requireMicrosoftSignature: true));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("wrong-session", result.ReasonCode);
            Assert.AreEqual(0, provider.SignatureVerificationCount);
        }

        [TestMethod]
        public void AcceptedProcessInstanceCachesDeferredSignatureResult()
        {
            var identity = CopyIdentity(GetCurrentIdentity(), hasTrustedMicrosoftSignature: true);
            var provider = new DeferredSignatureIdentityProvider { Identity = identity };
            var authenticator = new NamedPipePeerAuthenticator(provider);
            var policy = CopyPolicy(CreatePolicy(identity), requireMicrosoftSignature: true);

            Assert.IsTrue(authenticator.Authenticate(identity.ProcessId, policy).Accepted);
            Assert.IsTrue(authenticator.Authenticate(identity.ProcessId, policy).Accepted);
            Assert.AreEqual(1, provider.SignatureVerificationCount);
        }

        [TestMethod]
        public void SettingsSyncPayloadKeepsExistingJsonShape()
        {
            var contract = typeof(MouseWithoutBordersViewModel).GetNestedType("ISettingsSyncHelper", BindingFlags.NonPublic);
            var stateType = contract!.GetNestedType("MachineSocketState");
            var state = Activator.CreateInstance(stateType!);
            stateType!.GetField("Name")!.SetValue(state, "PC");
            stateType.GetField("Status")!.SetValue(state, Enum.ToObject(stateType.GetField("Status")!.FieldType, 9));

            Assert.AreEqual("""{"Name":"PC","Status":9}""", JsonConvert.SerializeObject(state));
        }

        private static NamedPipePeerAuthenticator CreateRealAuthenticator()
        {
            return new NamedPipePeerAuthenticator(new WindowsNamedPipePeerIdentityProvider(new AcceptSignatureVerifier()));
        }

        private static NamedPipePeerPolicy CreatePolicy(NamedPipePeerIdentity identity)
        {
            return new NamedPipePeerPolicy
            {
                ExpectedSessionId = identity.SessionId,
                ExpectedUserSid = identity.UserSid,
                ExpectedImagePath = identity.ImagePath,
                ExpectedFileVersion = identity.FileVersion,
                RequireMicrosoftSignature = false,
            };
        }

        private static NamedPipePeerPolicy CopyPolicy(
            NamedPipePeerPolicy policy,
            int? expectedSessionId = null,
            string expectedUserSid = null,
            string expectedImagePath = null,
            bool? allowLocalSystem = null,
            bool? requireMicrosoftSignature = null)
        {
            return new NamedPipePeerPolicy
            {
                ExpectedSessionId = expectedSessionId ?? policy.ExpectedSessionId,
                ExpectedUserSid = expectedUserSid ?? policy.ExpectedUserSid,
                ExpectedImagePath = expectedImagePath ?? policy.ExpectedImagePath,
                ExpectedFileVersion = policy.ExpectedFileVersion,
                AllowLocalSystem = allowLocalSystem ?? policy.AllowLocalSystem,
                RequireMicrosoftSignature = requireMicrosoftSignature ?? policy.RequireMicrosoftSignature,
            };
        }

        private static NamedPipePeerIdentity CopyIdentity(
            NamedPipePeerIdentity identity,
            long? creationTimeUtcTicks = null,
            int? sessionId = null,
            string userSid = null,
            bool? hasTrustedMicrosoftSignature = null)
        {
            return new NamedPipePeerIdentity
            {
                ProcessId = identity.ProcessId,
                CreationTimeUtcTicks = creationTimeUtcTicks ?? identity.CreationTimeUtcTicks,
                SessionId = sessionId ?? identity.SessionId,
                UserSid = userSid ?? identity.UserSid,
                ImagePath = identity.ImagePath,
                FileVersion = identity.FileVersion,
                HasTrustedMicrosoftSignature = hasTrustedMicrosoftSignature ?? identity.HasTrustedMicrosoftSignature,
            };
        }

        private static NamedPipePeerIdentity GetCurrentIdentity()
        {
            return new WindowsNamedPipePeerIdentityProvider(new AcceptSignatureVerifier()).GetIdentity(Environment.ProcessId);
        }

        private static async Task<(NamedPipeServerStream Server, NamedPipeClientStream Client)> CreateConnectedPairAsync(string pipeName = null)
        {
            pipeName ??= UniquePipeName();
            using var currentIdentity = WindowsIdentity.GetCurrent();
            var server = RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!);
            var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            var waitTask = server.WaitForConnectionAsync();
            await client.ConnectAsync(5000);
            await waitTask;
            return (server, client);
        }

        private static string UniquePipeName()
        {
            return $"PowerToys.MWB.v2.UnitTest.{Environment.ProcessId}.{Guid.NewGuid():N}";
        }

        private sealed class AcceptSignatureVerifier : IProcessSignatureVerifier
        {
            public bool HasTrustedMicrosoftSignature(string imagePath) => true;
        }

        private sealed class FakeIdentityProvider : INamedPipePeerIdentityProvider
        {
            public NamedPipePeerIdentity Identity { get; set; }

            public NamedPipePeerIdentity GetIdentity(int processId) => Identity;
        }

        private sealed class DeferredSignatureIdentityProvider : INamedPipePeerIdentityProvider, IDeferredProcessSignatureVerifier
        {
            public NamedPipePeerIdentity Identity { get; set; }

            public int SignatureVerificationCount { get; private set; }

            public NamedPipePeerIdentity GetIdentity(int processId) => Identity;

            public bool HasTrustedMicrosoftSignature(NamedPipePeerIdentity identity)
            {
                SignatureVerificationCount++;
                return identity.HasTrustedMicrosoftSignature;
            }
        }
    }
}
