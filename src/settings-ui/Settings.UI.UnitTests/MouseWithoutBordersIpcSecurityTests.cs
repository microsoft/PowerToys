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
using StreamJsonRpc;

namespace Microsoft.PowerToys.Settings.UI.UnitTests
{
    [TestClass]
    public sealed class MouseWithoutBordersIpcSecurityTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task RpcHelpersOwnFreshVerifiedPipesAcrossDelayedClosureAndServerRestart(bool failFirstRequest)
        {
            var connect = typeof(MouseWithoutBordersViewModel).GetMethod("ConnectSettingsSyncHelperAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
            var pipeName = UniquePipeName();
            NamedPipeClientStream previousStream = null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var identity = WindowsIdentity.GetCurrent();
                await using var server = RestrictedNamedPipeServer.Create(pipeName, identity.User!);
                var wait = server.WaitForConnectionAsync();
                var task = (Task)connect.Invoke(null, new object[] { pipeName, Path.GetFileName(GetCurrentExecutablePath()) })!;
                await wait.WaitAsync(TimeSpan.FromSeconds(5));
                var failRequest = failFirstRequest && attempt == 0;
                using var rpc = JsonRpc.Attach(server, new PairingRpcTarget(failRequest));
                await task.WaitAsync(TimeSpan.FromSeconds(5));
                using var helper = (IDisposable)task.GetType().GetProperty("Result")!.GetValue(task)!;
                var stream = (NamedPipeClientStream)helper.GetType().GetProperty("Stream")!.GetValue(helper)!;
                var handle = stream.SafePipeHandle;
                Assert.AreNotSame(previousStream, stream);
                var endpoint = helper.GetType().GetProperty("Endpoint")!.GetValue(helper)!;
                var contract = typeof(MouseWithoutBordersViewModel).GetNestedType("ISettingsSyncHelper", BindingFlags.NonPublic)!;
                var keyTask = (Task<string>)contract.GetMethod("GetCurrentPairingKey")!.Invoke(endpoint, null)!;
                if (failRequest)
                {
                    await Assert.ThrowsExceptionAsync<RemoteInvocationException>(() => keyTask.WaitAsync(TimeSpan.FromSeconds(5)));
                }
                else
                {
                    Assert.AreEqual("test-pairing-key", await keyTask.WaitAsync(TimeSpan.FromSeconds(5)));
                }

                helper.Dispose();
                Assert.IsTrue(handle.IsClosed);

                // Leave the server alive after client disposal, as RPC read-loop cleanup can finish later.
                await Task.Delay(50);
                previousStream = stream;
            }
        }

        public sealed class PairingRpcTarget
        {
            private readonly bool _failRequest;

            public PairingRpcTarget(bool failRequest)
            {
                _failRequest = failRequest;
            }

            public Task<string> GetCurrentPairingKey() => _failRequest
                ? Task.FromException<string>(new InvalidOperationException("Synthetic pairing request failure."))
                : Task.FromResult("test-pairing-key");
        }

        [TestMethod]
        public async Task ShutdownWaitObservesThePipeServerProcessExit()
        {
            var pipeName = UniquePipeName();
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-NonInteractive");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add($"$pipe = [System.IO.Pipes.NamedPipeServerStream]::new('{pipeName}'); try {{ $pipe.WaitForConnection(); [void]$pipe.ReadByte(); Start-Sleep -Milliseconds 200 }} finally {{ $pipe.Dispose() }}");
            using var server = Process.Start(start)!;
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await client.ConnectAsync(5000);
                await MouseWithoutBordersIpc.ShutdownAndWaitForServerExitAsync(
                    client,
                    () => client.WriteByte(1),
                    TimeSpan.FromSeconds(5));
                Assert.IsTrue(server.HasExited, "The shutdown wait must not complete before the server exits.");
            }
            finally
            {
                client.Dispose();
                if (!server.WaitForExit(3000))
                {
                    server.Kill(entireProcessTree: true);
                    Assert.Fail("The isolated pipe server did not exit within its cleanup timeout.");
                }
            }
        }

        [TestMethod]
        public async Task ShutdownWaitReportsTimeoutWhenTheServerRemainsAlive()
        {
            var pair = await CreateConnectedPairAsync();
            await using var server = pair.Server;
            await using var client = pair.Client;
            var shutdownRequested = false;

            await Assert.ThrowsExceptionAsync<TimeoutException>(() =>
                MouseWithoutBordersIpc.ShutdownAndWaitForServerExitAsync(
                    client,
                    () => shutdownRequested = true,
                    TimeSpan.FromMilliseconds(100)));

            Assert.IsTrue(shutdownRequested);
        }

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
        public void ConnectTextFieldsRemainOwnedByPageWithoutBindings()
        {
            var document = XDocument.Load(FindRepositoryFile(@"src\settings-ui\Settings.UI\SettingsXAML\Views\MouseWithoutBordersPage.xaml"));
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            foreach (var property in new[] { "ConnectSecurityKey", "ConnectPCName" })
            {
                var textBox = document.Descendants().Single(element => (string)element.Attribute(xaml + "Name") == property + "TextBox");
                Assert.IsNull(textBox.Attribute("Text"));
                Assert.AreEqual(
                    "{x:Bind ViewModel.CanConnectPairing, Mode=OneWay}",
                    (string)textBox.Ancestors().Single(element => element.Name.LocalName == "SettingsCard").Attribute("IsEnabled"));
            }
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
        public void PairingSetupWarningAndConnectSourceWiringDoesNotRequireAnExistingKey()
        {
            var viewModel = File.ReadAllText(FindRepositoryFile(@"src\settings-ui\Settings.UI\ViewModels\MouseWithoutBordersViewModel.cs"));
            StringAssert.Contains(viewModel, "public bool ShowPairingSetupRequired => PairingSetupRequired;");
            StringAssert.Contains(viewModel, "public bool CanConnectPairing => CanManagePairing;");
            StringAssert.Contains(viewModel, "public bool CanManagePairing => !_isPairingPending && !_isServiceModeChanging && !_hasServiceModeError && (!UseService || IsElevated);");

            var program = File.ReadAllText(FindRepositoryFile(@"src\modules\MouseWithoutBorders\App\Class\Program.cs"));
            var start = program.IndexOf("public Task ConnectToMachine(", StringComparison.Ordinal);
            var end = program.IndexOf("public Task<string> GenerateNewKey(", start, StringComparison.Ordinal);
            var connect = program.Substring(start, end - start);
            StringAssert.Contains(connect, "EnsurePairingOperationAuthorized(Settings.IsProtectedMode, _canManageServicePairing);");
            StringAssert.Contains(connect, "Setting.Values.MyKey = securityKey;");
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
