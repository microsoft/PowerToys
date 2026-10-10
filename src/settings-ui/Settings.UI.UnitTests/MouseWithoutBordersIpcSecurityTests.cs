// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using StreamJsonRpc;

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
        public async Task LegitimateSameSessionClientConnectionIsAccepted()
        {
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;

            var result = NamedPipePeerVerification.TryVerifyClient(
                server,
                Path.GetFileName(GetCurrentExecutablePath()),
                GetCurrentUserSid(),
                Process.GetCurrentProcess().SessionId,
                out var rejectionReason);

            Assert.IsTrue(result, rejectionReason);
        }

        [TestMethod]
        public async Task LegitimateSameSessionServerConnectionIsAccepted()
        {
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;

            var result = NamedPipePeerVerification.TryVerifyServer(
                client,
                Path.GetFileName(GetCurrentExecutablePath()),
                GetCurrentUserSid(),
                Process.GetCurrentProcess().SessionId,
                allowLocalSystem: false,
                out var rejectionReason);

            Assert.IsTrue(result, rejectionReason);
        }

        [TestMethod]
        public async Task UnexpectedClientPathIsRejected()
        {
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;

            var accepted = NamedPipePeerVerification.TryVerifyClient(
                server,
                "unexpected-settings.exe",
                GetCurrentUserSid(),
                Process.GetCurrentProcess().SessionId,
                out var rejectionReason);

            Assert.IsFalse(accepted);
            Assert.AreEqual("wrong-image", rejectionReason);
        }

        [TestMethod]
        public async Task UnexpectedClientUserIsRejected()
        {
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;

            var accepted = NamedPipePeerVerification.TryVerifyClient(
                server,
                Path.GetFileName(GetCurrentExecutablePath()),
                new SecurityIdentifier(WellKnownSidType.AnonymousSid, null).Value,
                Process.GetCurrentProcess().SessionId,
                out var rejectionReason);

            Assert.IsFalse(accepted);
            Assert.AreEqual("wrong-user", rejectionReason);
        }

        [TestMethod]
        public async Task UnexpectedClientSessionIsRejected()
        {
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;

            var accepted = NamedPipePeerVerification.TryVerifyClient(
                server,
                Path.GetFileName(GetCurrentExecutablePath()),
                GetCurrentUserSid(),
                Process.GetCurrentProcess().SessionId + 1,
                out var rejectionReason);

            Assert.IsFalse(accepted);
            Assert.AreEqual("wrong-session", rejectionReason);
        }

        [TestMethod]
        public void DisconnectedPipeIsRejected()
        {
            using var server = new NamedPipeServerStream(UniquePipeName(), PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            var accepted = NamedPipePeerVerification.TryVerifyClient(
                server,
                Path.GetFileName(GetCurrentExecutablePath()),
                GetCurrentUserSid(),
                Process.GetCurrentProcess().SessionId,
                out var rejectionReason);

            Assert.IsFalse(accepted);
            Assert.AreEqual("pipe-not-connected", rejectionReason);
        }

        [TestMethod]
        public void InvalidPeerIdentityFailsClosed()
        {
            var method = typeof(NamedPipePeerVerification).GetMethod("TryVerifyPeerProcess", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            var arguments = new object[]
            {
                uint.MaxValue,
                Path.GetFileName(GetCurrentExecutablePath()),
                GetCurrentUserSid(),
                Process.GetCurrentProcess().SessionId,
                false,
                null,
            };

            var accepted = (bool)method!.Invoke(null, arguments)!;

            Assert.IsFalse(accepted);
            Assert.AreEqual("identity-unavailable", arguments[^1]);
        }

        [TestMethod]
        public async Task FakeServerAndPipeSquattingAreRejected()
        {
            var pipeName = UniquePipeName();
            using var currentIdentity = WindowsIdentity.GetCurrent();

            await using (var fakeServer = RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!))
            {
                Assert.ThrowsException<Win32Exception>(() => RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!));

                var waitTask = fakeServer.WaitForConnectionAsync();
                await using var fakeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await fakeClient.ConnectAsync(5000);
                await waitTask;

                var accepted = NamedPipePeerVerification.TryVerifyServer(
                    fakeClient,
                    MouseWithoutBordersIpc.MouseWithoutBordersExecutableFileName,
                    GetCurrentUserSid(),
                    Process.GetCurrentProcess().SessionId,
                    allowLocalSystem: false,
                    out var rejectionReason);

                Assert.IsFalse(accepted);
                Assert.AreEqual("wrong-image", rejectionReason);
            }

            await using var legitimateServer = RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!);
            var legitimateWaitTask = legitimateServer.WaitForConnectionAsync();
            await using var legitimateClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await legitimateClient.ConnectAsync(5000);
            await legitimateWaitTask;

            var legitimateAccepted = NamedPipePeerVerification.TryVerifyServer(
                legitimateClient,
                Path.GetFileName(GetCurrentExecutablePath()),
                GetCurrentUserSid(),
                Process.GetCurrentProcess().SessionId,
                allowLocalSystem: false,
                out var legitimateRejectionReason);

            Assert.IsTrue(legitimateAccepted, legitimateRejectionReason);
        }

        [TestMethod]
        public void PipeDaclAllowsOnlyExpectedUserAndLocalSystem()
        {
            using var currentIdentity = WindowsIdentity.GetCurrent();
            using var server = RestrictedNamedPipeServer.Create(UniquePipeName(), currentIdentity.User!);
            var expectedIdentities = new[]
            {
                currentIdentity.User!,
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            };
            var accessRules = server.GetAccessControl().GetAccessRules(true, false, typeof(SecurityIdentifier));

            Assert.AreEqual(expectedIdentities.Length, accessRules.Count);
            foreach (AuthorizationRule rule in accessRules)
            {
                var pipeRule = (PipeAccessRule)rule;
                Assert.AreEqual(AccessControlType.Allow, pipeRule.AccessControlType);
                Assert.AreEqual(
                    PipeAccessRights.FullControl,
                    pipeRule.PipeAccessRights & PipeAccessRights.FullControl);
                Assert.IsTrue(Array.Exists(expectedIdentities, sid => sid.Equals(pipeRule.IdentityReference)));
            }
        }

        [TestMethod]
        public async Task ServerRestartAllowsVerifiedReconnect()
        {
            var pipeName = UniquePipeName();
            using var currentIdentity = WindowsIdentity.GetCurrent();
            var executablePath = GetCurrentExecutablePath();
            var userSid = GetCurrentUserSid();
            var sessionId = Process.GetCurrentProcess().SessionId;

            for (var attempt = 0; attempt < 2; attempt++)
            {
                await using var server = RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!);
                var waitTask = server.WaitForConnectionAsync();
                await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await client.ConnectAsync(5000);
                await waitTask;

                var accepted = NamedPipePeerVerification.TryVerifyServer(
                    client,
                    Path.GetFileName(executablePath),
                    userSid,
                    sessionId,
                    allowLocalSystem: false,
                    out var rejectionReason);

                Assert.IsTrue(accepted, rejectionReason);
            }
        }

        [TestMethod]
        public void IntactAuthenticodeSignatureAcceptsEmbeddedMicrosoftSignedDependency()
        {
            var signedBinary = GetKnownEmbeddedMicrosoftSignedDependencyPath();

            Assert.IsTrue(HasIntactAuthenticodeSignature(signedBinary));
        }

        [TestMethod]
        public void RealVerifierAcceptsEmbeddedMicrosoftSignedDependency()
        {
            var signedBinary = GetKnownEmbeddedMicrosoftSignedDependencyPath();

            Assert.IsTrue(HasTrustedMicrosoftSignature(signedBinary));
        }

        [TestMethod]
        public void RealVerifierRejectsUnsignedTestAssembly()
        {
            Assert.IsFalse(HasTrustedMicrosoftSignature(typeof(MouseWithoutBordersIpcSecurityTests).Assembly.Location));
        }

        [TestMethod]
        public void RealVerifierRejectsTamperedSignedBinary()
        {
            var signedBinary = GetKnownEmbeddedMicrosoftSignedDependencyPath();
            var artifactDirectory = CreateTestArtifactDirectory();
            var tamperedBinary = Path.Combine(artifactDirectory, Path.GetFileName(signedBinary));

            try
            {
                File.Copy(signedBinary, tamperedBinary, overwrite: true);
                TamperFile(tamperedBinary);

                Assert.IsFalse(HasTrustedMicrosoftSignature(tamperedBinary));
            }
            finally
            {
                if (Directory.Exists(artifactDirectory))
                {
                    Directory.Delete(artifactDirectory, recursive: true);
                }
            }
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(7)]
        public void CustomRootTrustChainAcceptsIntermediateFromExtraStore(int validityDays)
        {
            using var rootKey = RSA.Create(2048);
            using var root = CreateRootCertificate(rootKey, validityDays);
            using var intermediateKey = RSA.Create(2048);
            using var intermediate = CreateIntermediateCertificate(root, intermediateKey);
            using var leaf = CreateCodeSigningLeafCertificate(intermediate);

            Assert.AreEqual(root.NotBefore, intermediate.NotBefore);
            Assert.AreEqual(root.NotAfter, intermediate.NotAfter);
            Assert.AreEqual(intermediate.NotBefore, leaf.NotBefore);
            Assert.AreEqual(intermediate.NotAfter, leaf.NotAfter);

            using var chainWithoutIntermediate = CreateCodeSigningChain(root);
            Assert.IsFalse(chainWithoutIntermediate.Build(leaf));

            using var chainWithIntermediate = CreateCodeSigningChain(root);
            chainWithIntermediate.ChainPolicy.ExtraStore.Add(intermediate);
            Assert.IsTrue(chainWithIntermediate.Build(leaf));
        }

        [TestMethod]
        public void GeneratedCertificateChainUsesIssuerValidity()
        {
            using var rootKey = RSA.Create(2048);
            using var root = CreateRootCertificate(rootKey, validityDays: 1);
            using var intermediateKey = RSA.Create(2048);
            using var intermediate = CreateIntermediateCertificate(root, intermediateKey);
            using var leaf = CreateCodeSigningLeafCertificate(intermediate);

            Assert.AreEqual(root.NotBefore, intermediate.NotBefore);
            Assert.AreEqual(root.NotAfter, intermediate.NotAfter);
            Assert.AreEqual(intermediate.NotBefore, leaf.NotBefore);
            Assert.AreEqual(intermediate.NotAfter, leaf.NotAfter);
        }

        [TestMethod]
        public void SignerCertificateEqualityRejectsDistinctCertificatesWithSameSubject_FallbackWithoutSecondTrustedFixture()
        {
            using var firstKey = RSA.Create(2048);
            using var secondKey = RSA.Create(2048);
            using var first = CreateSubjectCertificate("CN=Microsoft Corporation Unit Test", firstKey);
            using var second = CreateSubjectCertificate("CN=Microsoft Corporation Unit Test", secondKey);

            Assert.IsFalse(HaveMatchingSignerCertificate(first, second));
        }

        [TestMethod]
        public void DirectoryRelationshipAcceptsEqualDirectories()
        {
            Assert.IsTrue(HaveEqualOrNestedDirectories(
                @"C:\Program Files\PowerToys",
                @"C:\Program Files\PowerToys"));
        }

        [TestMethod]
        public void DirectoryRelationshipAcceptsChildDirectory()
        {
            Assert.IsTrue(HaveEqualOrNestedDirectories(
                @"C:\Program Files\PowerToys",
                @"C:\Program Files\PowerToys\WinUI3Apps"));
        }

        [TestMethod]
        public void DirectoryRelationshipAcceptsParentDirectory()
        {
            Assert.IsTrue(HaveEqualOrNestedDirectories(
                @"C:\Program Files\PowerToys\WinUI3Apps",
                @"C:\Program Files\PowerToys"));
        }

        [TestMethod]
        public void DirectoryRelationshipRejectsPrefixSibling()
        {
            Assert.IsFalse(HaveEqualOrNestedDirectories(
                @"C:\Program Files\PowerToys",
                @"C:\Program Files\PowerToysOther"));
        }

        [TestMethod]
        public void DirectoryRelationshipRejectsSiblingDirectories()
        {
            Assert.IsFalse(HaveEqualOrNestedDirectories(
                @"C:\Program Files\PowerToys\WinUI3Apps",
                @"C:\Program Files\PowerToys\Modules"));
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

        [DataTestMethod]
        [DataRow("GenerateNewKey")]
        [DataRow("ConnectToMachine")]
        [DataRow("Reconnect")]
        public async Task StateChangingSettingsRequestsWaitForServerCompletion(string methodName)
        {
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;
            var target = new DelayedSettingsCommandTarget();
            using var serverRpc = JsonRpc.Attach(server, target);
            var helperType = typeof(MouseWithoutBordersViewModel).GetNestedType("SyncHelper", BindingFlags.NonPublic);
            var contract = typeof(MouseWithoutBordersViewModel).GetNestedType("ISettingsSyncHelper", BindingFlags.NonPublic);
            using var helper = (IDisposable)Activator.CreateInstance(helperType!, new object[] { client })!;
            var endpoint = helperType!.GetProperty("Endpoint")!.GetValue(helper);
            try
            {
                var arguments = methodName == "ConnectToMachine" ? new object[] { "fixture-peer", "fixture-value" } : Array.Empty<object>();
                var invocation = contract!.GetMethod(methodName)!.Invoke(endpoint, arguments);
                Assert.IsInstanceOfType<Task>(invocation, "State-changing requests need an acknowledgement before their channel is disposed.");
                var operation = (Task)invocation!;
                await target.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.IsFalse(operation.IsCompleted, "Sending the request is not completion of the remote operation.");
                target.Release.SetResult();
                await operation.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                target.Release.TrySetResult();
            }
        }

        [TestMethod]
        public async Task DisposingSettingsSyncHelperClosesItsPipe()
        {
            var helperType = typeof(MouseWithoutBordersViewModel).GetNestedType("SyncHelper", BindingFlags.NonPublic);
            var contract = typeof(MouseWithoutBordersViewModel).GetNestedType("ISettingsSyncHelper", BindingFlags.NonPublic);
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var pair = await CreateConnectedPairAsync();
                await using var server = pair.Server;
                await using var client = pair.Client;
                var pipeHandle = client.SafePipeHandle;
                using var serverRpc = JsonRpc.Attach(server, new SettingsCommandTarget());
                using var helper = (IDisposable)Activator.CreateInstance(helperType!, new object[] { client })!;
                var endpoint = helperType!.GetProperty("Endpoint")!.GetValue(helper);
                var polling = (Task)contract!.GetMethod("RequestMachineSocketStateAsync")!.Invoke(endpoint, null)!;
                await polling.WaitAsync(TimeSpan.FromSeconds(5));

                helper.Dispose();

                // Pending RPC reads can finish disposal asynchronously; do not race the pipe's property getter.
                await serverRpc.Completion.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.IsTrue(pipeHandle.IsClosed, "The owned handle must close once pending pipe I/O has completed.");
            }
        }

        [DataTestMethod]
        [DataRow("ConnectToMachine")]
        [DataRow("GenerateNewKey")]
        [DataRow("Reconnect")]
        public async Task SettingsRequestsAfterPollingUseFreshVerifiedRpcConnections(string methodName)
        {
            const int attempts = 16;
            var pipeName = UniquePipeName();
            var target = new SettingsCommandTarget();
            var expectedCalls = new List<string>();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var serverTask = RunSettingsServerAsync(pipeName, target, attempts * 2, timeout.Token);
            NamedPipeClientStream previousStream = null;
            try
            {
                for (var attempt = 0; attempt < attempts; attempt++)
                {
                    foreach (var request in new[] { "RequestMachineSocketStateAsync", methodName })
                    {
                        using (var helper = await ConnectSettingsSyncHelperAsync(pipeName))
                        {
                            var stream = GetSettingsSyncHelperStream(helper);
                            Assert.AreNotSame(previousStream, stream, "Every RPC helper must have its own verified pipe.");
                            previousStream = stream;
                            await InvokeSettingsSyncHelperAsync(helper, request).WaitAsync(TimeSpan.FromSeconds(5));
                            expectedCalls.Add(request);
                        }

                        if (attempt == 0 && request == "RequestMachineSocketStateAsync")
                        {
                            await Task.Delay(100, timeout.Token);
                        }
                    }
                }

                await serverTask;
                CollectionAssert.AreEqual(expectedCalls, target.Calls.ToArray());
                if (methodName == "ConnectToMachine")
                {
                    Assert.AreEqual("fixture-peer", target.MachineName);
                    Assert.AreEqual("fixture-value", target.SecurityKey);
                }
            }
            finally
            {
                await StopSettingsServerAsync(timeout, serverTask);
            }
        }

        [DataTestMethod]
        [DataRow("ConnectToMachine")]
        [DataRow("GenerateNewKey")]
        [DataRow("Reconnect")]
        public async Task SettingsRequestDisconnectIsReportedWithoutReplayAndNextConnectionRecovers(string methodName)
        {
            var pipeName = UniquePipeName();
            using var currentIdentity = WindowsIdentity.GetCurrent();
            var target = new DelayedSettingsCommandTarget();
            await using (var server = RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!))
            {
                var waitTask = server.WaitForConnectionAsync();
                using var helper = await ConnectSettingsSyncHelperAsync(pipeName);
                await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
                using var serverRpc = JsonRpc.Attach(server, target);
                try
                {
                    var operation = InvokeSettingsSyncHelperAsync(helper, methodName);
                    var errorVisible = false;
                    var successfulCompletions = 0;
                    var handledOperation = MouseWithoutBordersPage.TryExecuteSettingsCommandAsync(
                        () => operation,
                        visible => errorVisible = visible,
                        () => successfulCompletions++);
                    await target.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    serverRpc.Dispose();
                    server.Dispose();

                    await Assert.ThrowsExceptionAsync<ConnectionLostException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.IsFalse(await handledOperation.WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.IsTrue(errorVisible);
                    Assert.AreEqual(0, successfulCompletions);
                    Assert.AreEqual(1, target.InvocationCount, "State-changing requests must not be retried after an ambiguous disconnect.");
                }
                finally
                {
                    target.Release.TrySetResult();
                }

                await serverRpc.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            }

            var recoveredTarget = new SettingsCommandTarget();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var serverTask = RunSettingsServerAsync(pipeName, recoveredTarget, 2, timeout.Token);
            try
            {
                foreach (var request in new[] { "RequestMachineSocketStateAsync", methodName })
                {
                    using var helper = await ConnectSettingsSyncHelperAsync(pipeName);
                    await InvokeSettingsSyncHelperAsync(helper, request).WaitAsync(TimeSpan.FromSeconds(5));
                }

                await serverTask;
                CollectionAssert.AreEqual(new[] { "RequestMachineSocketStateAsync", methodName }, recoveredTarget.Calls.ToArray());
                Assert.AreEqual(1, target.InvocationCount);
            }
            finally
            {
                await StopSettingsServerAsync(timeout, serverTask);
            }
        }

        [DataTestMethod]
        [DataRow("image", "wrong-image")]
        [DataRow("user", "wrong-user")]
        [DataRow("session", "wrong-session")]
        public async Task SettingsSyncFactoryRejectsUnexpectedPeerAndClosesCandidate(string identityMismatch, string rejectionReason)
        {
            var pipeName = UniquePipeName();
            using var currentIdentity = WindowsIdentity.GetCurrent();
            await using var server = RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!);
            var waitTask = server.WaitForConnectionAsync();
            var expectedImage = identityMismatch == "image" ? "unexpected-settings-server.exe" : Path.GetFileName(GetCurrentExecutablePath());
            var expectedUser = identityMismatch == "user" ? new SecurityIdentifier(WellKnownSidType.AnonymousSid, null).Value : GetCurrentUserSid();
            var expectedSession = Process.GetCurrentProcess().SessionId + (identityMismatch == "session" ? 1 : 0);
            var connectionTask = ConnectSettingsSyncHelperAsync(pipeName, expectedImage, expectedUser, expectedSession);
            await waitTask.WaitAsync(TimeSpan.FromSeconds(5));

            var exception = await Assert.ThrowsExceptionAsync<UnauthorizedAccessException>(() => connectionTask);

            StringAssert.Contains(exception.Message, rejectionReason);
            var bytesRead = await server.ReadAsync(new byte[1]).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, bytesRead, "Rejected candidate pipes must be closed.");
        }

        [TestMethod]
        public async Task ShutdownNotificationIsDeliveredBeforeSettingsHelperDisposal()
        {
            var pipeName = UniquePipeName();
            var target = new SettingsCommandTarget();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var serverTask = RunSettingsServerAsync(pipeName, target, 1, timeout.Token, target.ShutdownReceived.Task);
            try
            {
                using (var helper = await ConnectSettingsSyncHelperAsync(pipeName))
                {
                    var shutdown = (Task)helper.GetType().GetMethod("ShutdownAsync")!.Invoke(helper, null)!;
                    await shutdown.WaitAsync(TimeSpan.FromSeconds(5));
                }

                await serverTask;
                Assert.AreEqual(1, target.Calls.Count);
                Assert.IsTrue(target.Calls.TryPeek(out var methodName));
                Assert.AreEqual("Shutdown", methodName);
            }
            finally
            {
                await StopSettingsServerAsync(timeout, serverTask);
            }
        }

        private static async Task StopSettingsServerAsync(CancellationTokenSource cancellation, Task serverTask)
        {
            await cancellation.CancelAsync();
            try
            {
                await serverTask;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
        }

        private static async Task RunSettingsServerAsync(string pipeName, object target, int connections, CancellationToken cancellationToken, Task notificationDelivery = null)
        {
            using var currentIdentity = WindowsIdentity.GetCurrent();
            for (var connection = 0; connection < connections; connection++)
            {
                await using var server = RestrictedNamedPipeServer.Create(pipeName, currentIdentity.User!);
                await server.WaitForConnectionAsync(cancellationToken);
                using var serverRpc = JsonRpc.Attach(server, target);
                await serverRpc.Completion.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                if (notificationDelivery != null)
                {
                    // Pipe EOF does not mean the queued notification handler has completed.
                    await notificationDelivery.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }

        private static async Task<IDisposable> ConnectSettingsSyncHelperAsync(string pipeName, string expectedImage = null, string expectedUser = null, int? expectedSession = null)
        {
            var helperType = typeof(MouseWithoutBordersViewModel).GetNestedType("SyncHelper", BindingFlags.NonPublic);
            var connection = (Task)helperType!.GetMethod("ConnectAsync")!.Invoke(
                null,
                new object[]
                {
                    pipeName,
                    expectedImage ?? Path.GetFileName(GetCurrentExecutablePath()),
                    expectedUser ?? GetCurrentUserSid(),
                    expectedSession ?? Process.GetCurrentProcess().SessionId,
                })!;
            await connection.WaitAsync(TimeSpan.FromSeconds(5));
            return (IDisposable)connection.GetType().GetProperty("Result")!.GetValue(connection)!;
        }

        private static NamedPipeClientStream GetSettingsSyncHelperStream(IDisposable helper)
        {
            return (NamedPipeClientStream)helper.GetType().GetProperty("Stream")!.GetValue(helper)!;
        }

        private static Task InvokeSettingsSyncHelperAsync(IDisposable helper, string methodName)
        {
            var contract = typeof(MouseWithoutBordersViewModel).GetNestedType("ISettingsSyncHelper", BindingFlags.NonPublic);
            var endpoint = helper.GetType().GetProperty("Endpoint")!.GetValue(helper);
            var arguments = methodName == "ConnectToMachine" ? new object[] { "fixture-peer", "fixture-value" } : Array.Empty<object>();
            return (Task)contract!.GetMethod(methodName)!.Invoke(endpoint, arguments)!;
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

        private static string GetCurrentExecutablePath()
        {
            return Process.GetCurrentProcess().MainModule?.FileName
                ?? Environment.ProcessPath
                ?? throw new InvalidOperationException("The current process has no executable path.");
        }

        private static string GetCurrentUserSid()
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? throw new InvalidOperationException("The current process has no user SID.");
        }

        private static string UniquePipeName()
        {
            return $"PowerToys.MWB.v2.UnitTest.{Environment.ProcessId}.{Guid.NewGuid():N}";
        }

        private static string GetKnownEmbeddedMicrosoftSignedDependencyPath()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Microsoft.WindowsAppRuntime.dll");
            Assert.IsTrue(File.Exists(path), $"Expected Microsoft-signed dependency was not found: {path}");
            return path;
        }

        private static string CreateTestArtifactDirectory()
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "MouseWithoutBordersIpcSecurityTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void TamperFile(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var offset = stream.Length > 4096 ? 4096 : 0;
            stream.Position = offset;
            var original = stream.ReadByte();
            Assert.AreNotEqual(-1, original);
            stream.Position = offset;
            stream.WriteByte(unchecked((byte)(original ^ 0x5A)));
        }

        private static void CleanupArtifactDirectory(string artifactDirectory)
        {
            if (Directory.Exists(artifactDirectory))
            {
                Directory.Delete(artifactDirectory, recursive: true);
            }
        }

        private static bool HasIntactAuthenticodeSignature(string path)
        {
            var method = typeof(NamedPipePeerVerification).GetMethod("HasIntactAuthenticodeSignature", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (bool)method!.Invoke(null, new object[] { path })!;
        }

        private static bool HasTrustedMicrosoftSignature(string path)
        {
            var method = typeof(NamedPipePeerVerification).GetMethod("HasTrustedMicrosoftSignature", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (bool)method!.Invoke(null, new object[] { path })!;
        }

        private static bool HaveMatchingSignerCertificate(X509Certificate2 firstSigner, X509Certificate2 secondSigner)
        {
            var method = typeof(NamedPipePeerVerification).GetMethod("HaveMatchingSignerCertificate", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (bool)method!.Invoke(null, new object[] { firstSigner, secondSigner })!;
        }

        private static bool HaveEqualOrNestedDirectories(string firstDirectory, string secondDirectory)
        {
            var method = typeof(NamedPipePeerVerification).GetMethod("HaveEqualOrNestedDirectories", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (bool)method!.Invoke(null, new object[] { firstDirectory, secondDirectory })!;
        }

        private static X509Chain CreateCodeSigningChain(X509Certificate2 root)
        {
            var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.3"));
            return chain;
        }

        private static X509Certificate2 CreateRootCertificate(RSA rootKey, int validityDays = 7)
        {
            var request = new CertificateRequest(
                "CN=MWB IPC Test Root",
                rootKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 1, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(validityDays));
        }

        private static X509Certificate2 CreateIntermediateCertificate(X509Certificate2 root, RSA intermediateKey)
        {
            var request = new CertificateRequest(
                "CN=MWB IPC Test Intermediate",
                intermediateKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            // Use the issuer's encoded interval, not a later clock sample that can exceed its expiry.
            using var intermediate = request.Create(root, root.NotBefore.ToUniversalTime(), root.NotAfter.ToUniversalTime(), RandomNumberGenerator.GetBytes(16));
            return intermediate.CopyWithPrivateKey(intermediateKey);
        }

        private static X509Certificate2 CreateCodeSigningLeafCertificate(X509Certificate2 intermediate)
        {
            using var leafKey = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=Microsoft Corporation Unit Test",
                leafKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

            var enhancedKeyUsage = new OidCollection
            {
                new Oid("1.3.6.1.5.5.7.3.3"),
            };
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(enhancedKeyUsage, true));

            return request.Create(intermediate, intermediate.NotBefore.ToUniversalTime(), intermediate.NotAfter.ToUniversalTime(), RandomNumberGenerator.GetBytes(16));
        }

        private static X509Certificate2 CreateSubjectCertificate(string subject, RSA key)
        {
            var request = new CertificateRequest(
                subject,
                key,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
        }

        private sealed class DelayedSettingsCommandTarget
        {
            private int _invocationCount;

            public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public int InvocationCount => _invocationCount;

            public Task GenerateNewKey() => WaitForRelease();

            public Task ConnectToMachine(string machineName, string securityKey) => WaitForRelease();

            public Task Reconnect() => WaitForRelease();

            private Task WaitForRelease()
            {
                Interlocked.Increment(ref _invocationCount);
                Entered.TrySetResult();
                return Release.Task;
            }
        }

        private sealed class SettingsCommandTarget
        {
            public ConcurrentQueue<string> Calls { get; } = new();

            public TaskCompletionSource ShutdownReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public string MachineName { get; private set; }

            public string SecurityKey { get; private set; }

            public Task<object[]> RequestMachineSocketStateAsync()
            {
                Calls.Enqueue(nameof(RequestMachineSocketStateAsync));
                return Task.FromResult(Array.Empty<object>());
            }

            public Task ConnectToMachine(string machineName, string securityKey)
            {
                MachineName = machineName;
                SecurityKey = securityKey;
                Calls.Enqueue(nameof(ConnectToMachine));
                return Task.CompletedTask;
            }

            public Task GenerateNewKey()
            {
                Calls.Enqueue(nameof(GenerateNewKey));
                return Task.CompletedTask;
            }

            public Task Reconnect()
            {
                Calls.Enqueue(nameof(Reconnect));
                return Task.CompletedTask;
            }

            public void Shutdown()
            {
                Calls.Enqueue(nameof(Shutdown));
                ShutdownReceived.TrySetResult();
            }
        }
    }
}
