// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.PowerToys.Settings.UI.Library;
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

        [TestMethod]
        public void CustomRootTrustChainAcceptsIntermediateFromExtraStore()
        {
            using var rootKey = RSA.Create(2048);
            using var root = CreateRootCertificate(rootKey);
            using var intermediateKey = RSA.Create(2048);
            using var intermediate = CreateIntermediateCertificate(root, intermediateKey);
            using var leaf = CreateCodeSigningLeafCertificate(intermediate);

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

        [TestMethod]
        public void PairingRpcContractMatchesApplicationContract()
        {
            var contract = typeof(MouseWithoutBordersViewModel).GetNestedType("ISettingsSyncHelper", BindingFlags.NonPublic);
            var generate = contract!.GetMethod("GenerateNewKey")!;
            var connect = contract.GetMethod("ConnectToMachine")!;

            Assert.AreEqual(typeof(Task<string>), generate.ReturnType);
            Assert.AreEqual(0, generate.GetParameters().Length);
            Assert.AreEqual(typeof(Task), connect.ReturnType);
            CollectionAssert.AreEqual(
                new[] { typeof(string), typeof(string) },
                connect.GetParameters().Select(parameter => parameter.ParameterType).ToArray());
        }

        [TestMethod]
        public void PairingInputsAreTrimmedBeforeValidation()
        {
            var state = new MouseWithoutBordersPairingState
            {
                ConnectPCName = " machine ",
                ConnectSecurityKey = " key ",
            };
            Assert.IsTrue(state.TryGetConnectionInputs(out var pcName, out var key));
            Assert.AreEqual("machine", pcName);
            Assert.AreEqual("key", key);

            foreach (var inputs in new[] { ("   ", "key"), ("machine", " \t "), ("machine", "mwb-dpapi:v1:TEST"), ("machine", "********") })
            {
                state.ConnectPCName = inputs.Item1;
                state.ConnectSecurityKey = inputs.Item2;
                Assert.IsFalse(state.TryGetConnectionInputs(out _, out _));
                Assert.IsTrue(state.HasError);
                Assert.IsFalse(state.IsUpdating);
                Assert.AreEqual(inputs.Item1, state.ConnectPCName);
                Assert.AreEqual(inputs.Item2, state.ConnectSecurityKey);
            }
        }

        [TestMethod]
        public async Task PairingFailuresPreserveDisplayedKeyAndConnectionInputs()
        {
            var state = new MouseWithoutBordersPairingState
            {
                TransientSecurityKey = "displayed",
                ConnectPCName = "machine",
                ConnectSecurityKey = "entered",
                ConnectFieldsVisible = true,
            };
            var successCount = 0;
            var failureCount = 0;
            foreach (var failure in new Exception[]
            {
                new InvalidOperationException("Pairing endpoint is unavailable."),
                new IOException("Connection closed."),
                new ObjectDisposedException("Pairing connection"),
                new OperationCanceledException(),
            })
            {
                Assert.IsFalse(await state.TryUpdateAsync(
                    () => Task.FromException(failure),
                    () =>
                    {
                        successCount++;
                        state.Clear();
                    },
                    _ => failureCount++));
                Assert.IsTrue(state.HasError);
                Assert.AreEqual("displayed", state.TransientSecurityKey);
                Assert.AreEqual("machine", state.ConnectPCName);
                Assert.AreEqual("entered", state.ConnectSecurityKey);
                Assert.IsTrue(state.ConnectFieldsVisible);
            }

            Assert.AreEqual(0, successCount);
            Assert.AreEqual(4, failureCount);
            Assert.IsTrue(await state.TryUpdateAsync(
                () => Task.CompletedTask,
                () => successCount++,
                _ => failureCount++));
            Assert.IsFalse(state.HasError);
            Assert.AreEqual(1, successCount);
            Assert.AreEqual(4, failureCount);
            state.TryGetConnectionInputs(out _, out _);
            state.ConnectPCName = " ";
            Assert.IsFalse(state.TryGetConnectionInputs(out _, out _));
            state.Clear();
            Assert.IsFalse(state.HasError);
        }

        [TestMethod]
        public void NewKeyUsesReactiveParentStateAndChecksCurrentPermission()
        {
            var page = File.ReadAllText(FindRepositoryFile(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml.cs"));
            StringAssert.Contains(page, "new AsyncCommand(ViewModel.SubmitNewKeyRequestAsync)");
            var viewModel = File.ReadAllText(FindRepositoryFile(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs"));
            StringAssert.Contains(viewModel.Replace("\r\n", "\n"), "public async Task SubmitNewKeyRequestAsync()\n        {\n            if (!CanManagePairing)");
            StringAssert.Contains(viewModel, "public bool CanManagePairing => !_pairingState.IsUpdating &&");
            foreach (var method in new[] { "public async Task SubmitNewKeyRequestAsync()", "public async Task<bool> SubmitConnectionRequestAsync()" })
            {
                var start = viewModel.IndexOf(method, StringComparison.Ordinal);
                var admission = viewModel.IndexOf("var updateTask = _pairingState.TryUpdateAsync(", start, StringComparison.Ordinal);
                var awaiting = viewModel.IndexOf("await updateTask", admission, StringComparison.Ordinal);
                var beforeAwait = viewModel.Substring(admission, awaiting - admission);
                StringAssert.Contains(beforeAwait, "OnPropertyChanged(nameof(CanManagePairing));");
                StringAssert.Contains(beforeAwait, "OnPropertyChanged(nameof(CanConnectPairing));");
                StringAssert.Contains(beforeAwait, "savedSettings = SettingsUtils.GetSettings<MouseWithoutBordersSettings>");
                StringAssert.Contains(beforeAwait, "LoadViewModelFromSettings(savedSettings);");
                StringAssert.Contains(beforeAwait, "(savedSettings == null || savedSettings.Properties.UseService == useService)");
                var end = viewModel.IndexOf("return succeeded;", awaiting, StringComparison.Ordinal);
                if (method == "public async Task SubmitNewKeyRequestAsync()")
                {
                    end = viewModel.IndexOf("public bool HasPairingError", awaiting, StringComparison.Ordinal);
                }

                var afterAwait = viewModel.Substring(awaiting, end - awaiting);
                StringAssert.Contains(afterAwait, "OnPropertyChanged(nameof(CanManagePairing));");
                StringAssert.Contains(afterAwait, "OnPropertyChanged(nameof(CanConnectPairing));");
            }

            var document = XDocument.Load(FindRepositoryFile(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml"));
            var expander = document.Descendants().Single(element => (string)element.Attribute("Name") == "MouseWithoutBordersSecurityKey");
            Assert.AreEqual("{x:Bind ViewModel.CanManagePairing, Mode=OneWay}", (string)expander.Attribute("IsEnabled"));
        }

        [TestMethod]
        public async Task PairingDoubleClickAdmitsOnlyOneUpdateIncludingConnection()
        {
            var state = new MouseWithoutBordersPairingState();
            var acknowledgement = new TaskCompletionSource<bool>();
            var calls = 0;
            var update = state.TryUpdateAsync(
                () =>
                {
                    calls++;
                    return acknowledgement.Task;
                },
                () => state.TransientSecurityKey = "acknowledged-key",
                _ => Assert.Fail("The operation should complete."));

            Assert.IsTrue(state.IsUpdating);
            Assert.IsFalse(await state.TryUpdateAsync(
                () =>
                {
                    calls++;
                    return Task.CompletedTask;
                },
                () => Assert.Fail("A second update must not change the display."),
                _ => Assert.Fail("A rejected update must not report an error.")));
            Assert.AreEqual(1, calls);
            acknowledgement.SetResult(true);
            Assert.IsTrue(await update);
            Assert.AreEqual("acknowledged-key", state.TransientSecurityKey);
            Assert.IsFalse(state.IsUpdating);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task SequentialPairingUpdatesSettleOwnedReadBeforeNextAdmission(bool useService, bool watcherBeforeReply)
        {
            var state = new MouseWithoutBordersPairingState();
            var cached = new MouseWithoutBordersSettings();
            cached.Properties.UseService = useService;
            cached.Properties.SecurityKey.Value = useService ? "mwb-dpapi:v1:TEST" : "old-key";
            foreach (var suffix in new[] { "AAAA", "BBBB" })
            {
                var committed = new MouseWithoutBordersSettings();
                committed.Properties.UseService = useService;
                committed.Properties.SecurityKey.Value = useService ? "mwb-dpapi:v1:" + suffix : suffix;
                committed.Properties.MachinePool.Value = suffix + ":1";
                committed.Properties.MachineMatrixString = new() { suffix, string.Empty, string.Empty, string.Empty };
                committed.Properties.EasyMouse.Value = 2;
                var acknowledgement = new TaskCompletionSource<bool>();
                var read = new TaskCompletionSource<MouseWithoutBordersSettings>();
                MouseWithoutBordersSettings saved = null;
                var update = state.TryUpdateAsync(
                    async () =>
                    {
                        await acknowledgement.Task;
                        saved = await read.Task;
                    },
                    () =>
                    {
                        cached = saved;
                        state.TransientSecurityKey = useService ? suffix : null;
                    },
                    _ => Assert.Fail("The operation should complete."));

                void Reload()
                {
                    if (state.ShouldClearForSettingsReload(useService, cached.Properties.SecurityKey.Value, useService, committed.Properties.SecurityKey.Value))
                    {
                        state.Clear();
                    }

                    cached = committed;
                }

                if (watcherBeforeReply)
                {
                    Reload();
                }

                acknowledgement.SetResult(true);
                Assert.IsTrue(state.IsUpdating, "Admission includes the committed-settings read.");
                Assert.IsFalse(await state.TryUpdateAsync(
                    () => Task.CompletedTask,
                    () => Assert.Fail("The owned read has not finished."),
                    _ => Assert.Fail("A duplicate must not run.")));
                read.SetResult(committed);
                Assert.IsTrue(await update);
                Assert.IsFalse(state.IsUpdating);
                if (!watcherBeforeReply)
                {
                    Reload();
                }

                Assert.AreEqual(suffix, state.GetDisplayValue(true, useService, cached.Properties.SecurityKey.Value));
                cached.Properties.ShowOriginalUI = !cached.Properties.ShowOriginalUI;
                var serialized = cached.ToJsonString();
                var persisted = System.Text.Json.JsonSerializer.Deserialize<MouseWithoutBordersSettings>(serialized);
                Assert.AreEqual(committed.Properties.SecurityKey.Value, persisted.Properties.SecurityKey.Value);
                Assert.AreEqual(suffix + ":1", persisted.Properties.MachinePool.Value);
                CollectionAssert.AreEqual(committed.Properties.MachineMatrixString, persisted.Properties.MachineMatrixString);
                Assert.AreEqual(2, persisted.Properties.EasyMouse.Value);
                Assert.AreEqual(cached.Properties.ShowOriginalUI, persisted.Properties.ShowOriginalUI);
            }
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task PairingSettlesWithoutWatcherAndReleasesAdmissionAfterReadFailure(bool readFails)
        {
            var state = new MouseWithoutBordersPairingState();
            var acknowledgement = new TaskCompletionSource<bool>();
            var read = new TaskCompletionSource<string>();
            string committed = null;
            var update = state.TryUpdateAsync(
                async () =>
                {
                    await acknowledgement.Task;
                    committed = await read.Task;
                },
                () => state.TransientSecurityKey = committed,
                _ => { });
            acknowledgement.SetResult(true);
            Assert.IsTrue(state.IsUpdating);
            if (readFails)
            {
                read.SetException(new IOException("Settings read failed."));
            }
            else
            {
                read.SetResult("acknowledged-key");
            }

            Assert.AreEqual(!readFails, await update);
            Assert.IsFalse(state.IsUpdating);
            Assert.AreEqual(readFails, state.HasError);
            Assert.AreEqual(readFails ? null : "acknowledged-key", state.TransientSecurityKey);
            Assert.IsTrue(await state.TryUpdateAsync(() => Task.CompletedTask, () => { }, _ => Assert.Fail()));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ClearedOperationCannotReleaseNewerPairingAdmission(bool oldFails)
        {
            var state = new MouseWithoutBordersPairingState();
            var oldReply = new TaskCompletionSource<bool>();
            var oldUpdate = state.TryUpdateAsync(
                () => oldReply.Task,
                () => Assert.Fail("An old context cannot publish a key."),
                _ => Assert.Fail("An old context cannot publish an error."));
            state.Clear();
            var newReply = new TaskCompletionSource<bool>();
            var newUpdate = state.TryUpdateAsync(
                () => newReply.Task,
                () => state.TransientSecurityKey = "new-key",
                _ => Assert.Fail("The current operation should complete."));
            if (oldFails)
            {
                oldReply.SetException(new IOException("Old connection closed."));
            }
            else
            {
                oldReply.SetResult(true);
            }

            Assert.IsFalse(await oldUpdate);
            Assert.IsTrue(state.IsUpdating);
            Assert.IsFalse(await state.TryUpdateAsync(
                () => Task.CompletedTask,
                () => Assert.Fail("The newer operation still owns admission."),
                _ => Assert.Fail()));
            newReply.SetResult(true);
            Assert.IsTrue(await newUpdate);
            Assert.IsFalse(state.IsUpdating);
            Assert.AreEqual("new-key", state.TransientSecurityKey);
        }

        [TestMethod]
        public async Task PairingSuccessWaitsForOperationAcknowledgement()
        {
            var state = new MouseWithoutBordersPairingState { TransientSecurityKey = "displayed" };
            var acknowledgement = new TaskCompletionSource<bool>();
            var update = state.TryUpdateAsync(
                () => acknowledgement.Task,
                () => state.TransientSecurityKey = "updated",
                _ => Assert.Fail("The operation should complete."));
            Assert.AreEqual("displayed", state.TransientSecurityKey);
            Assert.IsFalse(update.IsCompleted);
            Assert.IsTrue(state.IsUpdating);
            acknowledgement.SetResult(true);
            Assert.IsTrue(await update);
            Assert.AreEqual("updated", state.TransientSecurityKey);
            Assert.IsFalse(state.IsUpdating);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ClearedPairingContextIgnoresPendingCompletion(bool fails)
        {
            var state = new MouseWithoutBordersPairingState { TransientSecurityKey = "displayed" };
            var acknowledgement = new TaskCompletionSource<bool>();
            var update = state.TryUpdateAsync(
                () => acknowledgement.Task,
                () => state.TransientSecurityKey = "updated",
                _ => Assert.Fail("A cleared context must not receive errors."));

            state.Clear();
            if (fails)
            {
                acknowledgement.SetException(new IOException("Connection closed."));
            }
            else
            {
                acknowledgement.SetResult(true);
            }

            Assert.IsFalse(await update);
            Assert.IsNull(state.TransientSecurityKey);
            Assert.IsFalse(state.HasError);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ServicePairingCompletionIsIgnoredAfterModeChange(bool fails)
        {
            var state = new MouseWithoutBordersPairingState();
            var useService = true;
            var startingUseService = useService;
            var savedValue = "normal-key";
            var acknowledgement = new TaskCompletionSource<bool>();
            var update = state.TryUpdateAsync(
                () => acknowledgement.Task,
                () => state.TransientSecurityKey = "generated",
                _ => Assert.Fail("A different mode must not receive errors."),
                () => useService == startingUseService);

            useService = false;
            if (fails)
            {
                acknowledgement.SetException(new IOException("Connection closed."));
            }
            else
            {
                acknowledgement.SetResult(true);
            }

            Assert.IsFalse(await update);
            Assert.AreEqual("normal-key", savedValue);
            Assert.IsNull(state.TransientSecurityKey);
            Assert.IsFalse(state.HasError);
            Assert.IsFalse(state.IsUpdating);
        }

        [DataTestMethod]
        [DataRow("legacy-plaintext")]
        [DataRow("mwb-dpapi:v1:DATA")]
        public async Task PendingServiceKeyAcceptsEncryptedSettingsReload(string savedValue)
        {
            var state = new MouseWithoutBordersPairingState();
            var acknowledgement = new TaskCompletionSource<bool>();
            var update = state.TryUpdateAsync(
                () => acknowledgement.Task,
                () =>
                {
                    state.TransientSecurityKey = "generated";
                },
                _ => Assert.Fail("The operation should complete."));

            var configuredValue = "mwb-dpapi:v1:TEST";
            if (state.ShouldClearForSettingsReload(true, savedValue, true, configuredValue))
            {
                state.Clear();
            }

            savedValue = configuredValue;
            acknowledgement.SetResult(true);

            Assert.IsTrue(await update);
            Assert.AreEqual(configuredValue, savedValue);
            Assert.AreEqual("generated", state.GetDisplayValue(true, true, savedValue));
            Assert.IsFalse(state.ShouldClearForSettingsReload(true, savedValue, true, configuredValue));
            Assert.IsTrue(state.ShouldClearForSettingsReload(true, savedValue, true, MouseWithoutBordersIpc.PairingSetupRequiredIndicator));
        }

        [TestMethod]
        public async Task PendingServiceKeyDoesNotHideOtherSettingsContextChanges()
        {
            var state = new MouseWithoutBordersPairingState();
            var setupValue = MouseWithoutBordersIpc.PairingSetupRequiredIndicator;
            var configuredValue = "mwb-dpapi:v1:TEST";
            Assert.IsTrue(state.ShouldClearForSettingsReload(true, setupValue, true, configuredValue));

            var acknowledgement = new TaskCompletionSource<bool>();
            var update = state.TryUpdateAsync(
                () => acknowledgement.Task,
                () => Assert.Fail("A cleared context must not receive a generated key."),
                _ => Assert.Fail("The operation should complete."));

            Assert.IsTrue(state.ShouldClearForSettingsReload(true, setupValue, false, configuredValue));
            Assert.IsTrue(state.ShouldClearForSettingsReload(true, setupValue, true, "other-key"));
            state.Clear();
            Assert.IsTrue(state.ShouldClearForSettingsReload(true, setupValue, true, configuredValue));
            acknowledgement.SetResult(true);
            Assert.IsFalse(await update);
        }

        [TestMethod]
        public async Task GeneratedServiceKeySurvivesLateEncryptedWatcherUpdate()
        {
            var state = new MouseWithoutBordersPairingState();
            var cachedValue = "mwb-dpapi:v1:TEST";
            var committedValue = "mwb-dpapi:v1:DATA";
            Assert.IsTrue(await state.TryUpdateAsync(
                () => Task.CompletedTask,
                () =>
                {
                    cachedValue = committedValue;
                    state.TransientSecurityKey = "fresh-rpc-key";
                },
                _ => Assert.Fail("The operation should complete.")));

            Assert.IsFalse(state.ShouldClearForSettingsReload(true, cachedValue, true, committedValue));
            Assert.AreEqual("fresh-rpc-key", state.GetDisplayValue(true, true, "mwb-dpapi:v1:DATA"));
            Assert.IsTrue(state.ShouldClearForSettingsReload(true, "mwb-dpapi:v1:DATA", true, "mwb-dpapi:v1:KEYS"));
        }

        [TestMethod]
        public async Task FailedServiceKeyReplyAfterSavedValueChangeClearsPreviousDisplay()
        {
            var state = new MouseWithoutBordersPairingState
            {
                TransientSecurityKey = "previous-key",
                ConnectPCName = "machine",
                ConnectSecurityKey = "entered-key",
                ConnectFieldsVisible = true,
            };
            var acknowledgement = new TaskCompletionSource<bool>();
            var update = state.TryUpdateAsync(
                () => acknowledgement.Task,
                () => Assert.Fail("A failed reply must not apply a generated key."),
                _ => { });

            Assert.IsFalse(state.ShouldClearForSettingsReload(true, "mwb-dpapi:v1:TEST", true, "mwb-dpapi:v1:DATA"));
            Assert.IsNull(state.TransientSecurityKey);
            acknowledgement.SetException(new InvalidOperationException("Pairing reply unavailable."));

            Assert.IsFalse(await update);
            Assert.IsTrue(state.HasError);
            Assert.IsNull(state.TransientSecurityKey);
            Assert.AreEqual(MouseWithoutBordersIpc.PairingConfiguredIndicator, state.GetDisplayValue(true, true, "mwb-dpapi:v1:DATA"));
            Assert.AreEqual("machine", state.ConnectPCName);
            Assert.AreEqual("entered-key", state.ConnectSecurityKey);
            Assert.IsTrue(state.ConnectFieldsVisible);
        }

        [TestMethod]
        public void UnrelatedNormalReloadPreservesConnectionInputs()
        {
            var state = new MouseWithoutBordersPairingState
            {
                ConnectPCName = "machine",
                ConnectSecurityKey = "entered",
                ConnectFieldsVisible = true,
            };

            Assert.IsFalse(state.ShouldClearForSettingsReload(false, "ordinary", false, "ordinary"));
            Assert.AreEqual("machine", state.ConnectPCName);
            Assert.AreEqual("entered", state.ConnectSecurityKey);
            Assert.IsTrue(state.ConnectFieldsVisible);
        }

        [TestMethod]
        public void NewKeyReplyLoadsCommittedSettingsWithoutSavingCachedState()
        {
            var source = File.ReadAllText(FindRepositoryFile(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs"));
            var start = source.IndexOf("public async Task SubmitNewKeyRequestAsync()", StringComparison.Ordinal);
            var end = source.IndexOf("public bool HasPairingError", start, StringComparison.Ordinal);
            var request = source.Substring(start, end - start);
            StringAssert.Contains(request, "_pairingState.TransientSecurityKey = generatedKey;");
            StringAssert.Contains(request, "OnPropertyChanged(nameof(SecurityKey));");
            Assert.IsFalse(request.Contains("NotifyPropertyChanged(", StringComparison.Ordinal));
            Assert.IsFalse(request.Contains("Settings.Properties.SecurityKey.Value =", StringComparison.Ordinal));
            StringAssert.Contains(request, "savedSettings = SettingsUtils.GetSettings<MouseWithoutBordersSettings>");
            StringAssert.Contains(request, "LoadViewModelFromSettings(savedSettings);");

            var notifyStart = source.IndexOf("public void NotifyPropertyChanged(", StringComparison.Ordinal);
            var save = source.IndexOf("SettingsUtils.SaveSettings(", notifyStart, StringComparison.Ordinal);
            var beforeSave = source.Substring(notifyStart, save - notifyStart).Replace("\r\n", "\n");
            Assert.IsFalse(beforeSave.Contains("_pairingState.TransientSecurityKey != null", StringComparison.Ordinal));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task NormalKeyReplyAndWatcherNeverReplaceApplicationPairingSnapshot(bool watcherBeforeReply)
        {
            var state = new MouseWithoutBordersPairingState();
            var cached = new MouseWithoutBordersSettings();
            cached.Properties.SecurityKey.Value = "old-key";
            cached.Properties.MachinePool.Value = "old-pool";
            cached.Properties.MachineMatrixString = new() { "old-machine" };
            cached.Properties.EasyMouse.Value = 1;
            var application = new MouseWithoutBordersSettings();
            application.Properties.SecurityKey.Value = "generated-key";
            application.Properties.MachinePool.Value = "new-pool";
            application.Properties.MachineMatrixString = new() { "new-machine" };
            application.Properties.EasyMouse.Value = 2;
            var savedSnapshot = application.ToJsonString();
            var acknowledgement = new TaskCompletionSource<bool>();
            MouseWithoutBordersSettings committedSettings = null;
            var update = state.TryUpdateAsync(
                async () =>
                {
                    await acknowledgement.Task;
                    committedSettings = System.Text.Json.JsonSerializer.Deserialize<MouseWithoutBordersSettings>(application.ToJsonString());
                },
                () => cached = committedSettings,
                _ => Assert.Fail("The operation should complete."));

            void Reload()
            {
                if (state.ShouldClearForSettingsReload(false, cached.Properties.SecurityKey.Value, false, application.Properties.SecurityKey.Value))
                {
                    state.Clear();
                }

                cached = application;
            }

            if (watcherBeforeReply)
            {
                Reload();
            }

            acknowledgement.SetResult(true);
            Assert.IsTrue(await update);
            Assert.AreEqual("generated-key", cached.Properties.SecurityKey.Value);

            cached.Properties.ShowOriginalUI = !cached.Properties.ShowOriginalUI;
            savedSnapshot = cached.ToJsonString();

            StringAssert.Contains(savedSnapshot, "generated-key");
            StringAssert.Contains(savedSnapshot, "new-pool");
            StringAssert.Contains(savedSnapshot, "new-machine");
            Assert.IsFalse(savedSnapshot.Contains("old-key", StringComparison.Ordinal));
            Assert.AreEqual(cached.Properties.ShowOriginalUI, System.Text.Json.JsonSerializer.Deserialize<MouseWithoutBordersSettings>(savedSnapshot).Properties.ShowOriginalUI);
            Reload();
            Assert.IsNull(state.TransientSecurityKey);
            Assert.AreEqual("generated-key", cached.Properties.SecurityKey.Value);
            Assert.AreEqual(2, cached.Properties.EasyMouse.Value);
        }

        [TestMethod]
        public void ServiceKeyDisplayUsesTransientResultWithoutChangingSavedValue()
        {
            var state = new MouseWithoutBordersPairingState
            {
                TransientSecurityKey = "transient",
            };

            Assert.AreEqual("saved", state.GetDisplayValue(false, false, "saved"));
            Assert.AreEqual("transient", state.GetDisplayValue(true, true, "mwb-dpapi:v1:TEST"));
            Assert.AreEqual("********", state.GetDisplayValue(false, true, "mwb-dpapi:v1:TEST"));
            state.TransientSecurityKey = null;
            Assert.AreEqual("********", state.GetDisplayValue(true, true, "mwb-dpapi:v1:TEST"));
            Assert.AreEqual(string.Empty, state.GetDisplayValue(true, true, "legacy-plaintext"));
            Assert.AreEqual(string.Empty, state.GetDisplayValue(true, false, "mwb-dpapi:v1:TEST"));
        }

        [TestMethod]
        public void ConfiguredSettingsReloadKeepsGeneratedServiceKeyVisible()
        {
            var state = new MouseWithoutBordersPairingState
            {
                TransientSecurityKey = "transient",
            };

            var savedValue = "mwb-dpapi:v1:TEST";
            Assert.IsFalse(MouseWithoutBordersPairingState.HasPairingContextChanged(true, savedValue, true, savedValue));
            Assert.AreEqual("transient", state.GetDisplayValue(true, true, savedValue));
            Assert.IsTrue(MouseWithoutBordersPairingState.HasPairingContextChanged(true, savedValue, true, MouseWithoutBordersIpc.PairingSetupRequiredIndicator));
            Assert.IsTrue(MouseWithoutBordersPairingState.HasPairingContextChanged(true, savedValue, false, savedValue));
        }

        [DataTestMethod]
        [DataRow(false, "key", false, "key", false)]
        [DataRow(true, "key", true, "key", false)]
        [DataRow(false, null, false, null, false)]
        [DataRow(false, "key", true, "key", true)]
        [DataRow(true, "key", false, "key", true)]
        [DataRow(false, "key", false, "new-key", true)]
        [DataRow(true, "key", true, "new-key", true)]
        [DataRow(false, "key", false, "KEY", true)]
        [DataRow(false, null, false, "", true)]
        public void SettingsReloadClearsOnlyChangedPairingContext(bool previousUseService, string previousKey, bool useService, string key, bool expected)
        {
            Assert.AreEqual(expected, MouseWithoutBordersPairingState.HasPairingContextChanged(previousUseService, previousKey, useService, key));
        }

        [TestMethod]
        public void ConnectTextFieldsBindToTransientPairingState()
        {
            var document = XDocument.Load(FindRepositoryFile(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml"));
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            foreach (var property in new[] { "ConnectSecurityKey", "ConnectPCName" })
            {
                var textBox = document.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == property + "TextBox");
                Assert.AreEqual(
                    "{x:Bind ViewModel." + property + ", Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}",
                    (string)textBox.Attribute("Text"));
            }
        }

        [TestMethod]
        public void TransientServiceKeyIsNeverPartOfSerializedSettings()
        {
            var settings = new MouseWithoutBordersSettings();
            settings.Properties.SecurityKey.Value = "mwb-dpapi:v1:TEST";
            var state = new MouseWithoutBordersPairingState { TransientSecurityKey = "fresh-rpc-key" };
            var serialized = settings.ToJsonString();
            StringAssert.Contains(serialized, "mwb-dpapi:v1:TEST");
            Assert.IsFalse(serialized.Contains("fresh-rpc-key", StringComparison.Ordinal));
            Assert.AreEqual("fresh-rpc-key", state.GetDisplayValue(true, true, settings.Properties.SecurityKey.Value));
            state.Clear();
            Assert.AreEqual("********", state.GetDisplayValue(true, true, settings.Properties.SecurityKey.Value));
            Assert.AreEqual(serialized, settings.ToJsonString());
        }

        [TestMethod]
        public void PairingPageLifecycleClearsTransientValues()
        {
            var state = new MouseWithoutBordersPairingState
            {
                TransientSecurityKey = "transient",
                ConnectSecurityKey = "key",
                ConnectPCName = "machine",
                ConnectFieldsVisible = true,
            };

            state.Clear();

            Assert.IsNull(state.TransientSecurityKey);
            Assert.AreEqual(string.Empty, state.ConnectSecurityKey);
            Assert.AreEqual(string.Empty, state.ConnectPCName);
            Assert.IsFalse(state.ConnectFieldsVisible);
        }

        [TestMethod]
        public void PairingSetupStateClearsAfterProvisioning()
        {
            Assert.IsTrue(MouseWithoutBordersPairingHelper.PairingSetupRequired("legacy-plaintext", true));
            Assert.IsFalse(MouseWithoutBordersPairingHelper.PairingSetupRequired("mwb-dpapi:v1:TEST", true));
            Assert.IsTrue(MouseWithoutBordersPairingHelper.PairingSetupRequired("mwb-dpapi:v1:TEST", false));
            Assert.IsFalse(MouseWithoutBordersPairingHelper.PairingSetupRequired("ordinary-key", false));
        }

        [TestMethod]
        public void PairingSetupResourceExplainsExplicitRepairInEitherMode()
        {
            var resourcePath = FindRepositoryFile(@"src\settings-ui\Settings.UI\Strings\en-us\Resources.resw");
            var document = XDocument.Load(resourcePath);
            var value = document
                .Descendants("data")
                .Single(element => (string)element.Attribute("name") == "MouseWithoutBorders_PairingSetupRequired.Title")
                .Element("value")!
                .Value;

            Assert.AreEqual(
                "The saved pairing key cannot be used in the current mode. Generate a new key or connect using a key from another computer.",
                value);
        }

        [TestMethod]
        public void PairingSetupWarningAndConnectDoNotRequireServiceModeOrAnExistingKey()
        {
            var viewModel = File.ReadAllText(FindRepositoryFile(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs"));
            StringAssert.Contains(viewModel, "public bool ShowPairingSetupRequired => PairingSetupRequired;");
            StringAssert.Contains(viewModel, "public bool CanConnectPairing => CanManagePairing;");
            StringAssert.Contains(viewModel, "public bool CanManagePairing => !_pairingState.IsUpdating && MouseWithoutBordersPairingHelper.CanManagePairing(IsElevated, UseService);");

            var program = File.ReadAllText(FindRepositoryFile(@"src\modules\MouseWithoutBorders\App\Class\Program.cs"));
            var start = program.IndexOf("public async Task ConnectToMachine(", StringComparison.Ordinal);
            var end = program.IndexOf("public async Task<string> GenerateNewKey(", start, StringComparison.Ordinal);
            var connect = program.Substring(start, end - start);
            StringAssert.Contains(connect, "EnsurePairingOperationAuthorized(Settings.IsProtectedMode, _authorization);");
            StringAssert.Contains(connect, "_settings.SavePairingSettingsAsync(securityKey,");
            Assert.IsFalse(connect.Contains("_settings.HasPairingKey", StringComparison.Ordinal));
            Assert.IsFalse(program.Contains("EnsureConnectOperationAvailable", StringComparison.Ordinal));
        }

        [TestMethod]
        public void StartupDoesNotReplaceUnavailableSavedPairingKey()
        {
            var source = File.ReadAllText(FindRepositoryFile(@"src\modules\MouseWithoutBorders\App\Core\InitAndCleanup.cs"));
            var start = source.IndexOf("catch (Exception e) when (e is FormatException or CryptographicException or ArgumentException)", StringComparison.Ordinal);
            var end = source.IndexOf("Logger.Log(e.Message);", start, StringComparison.Ordinal);
            var failure = source.Substring(start, end - start);
            StringAssert.Contains(failure, "Encryption.MyKey = string.Empty;");
            StringAssert.Contains(failure, "Encryption.GeneratedKey = false;");
            Assert.IsFalse(failure.Contains("Setting.Values.MyKey =", StringComparison.Ordinal));
            Assert.IsFalse(failure.Contains("CreateRandomKey", StringComparison.Ordinal));
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

        private static string FindRepositoryFile(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            Assert.Fail($"Could not locate {relativePath}.");
            return string.Empty;
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
    }
}
