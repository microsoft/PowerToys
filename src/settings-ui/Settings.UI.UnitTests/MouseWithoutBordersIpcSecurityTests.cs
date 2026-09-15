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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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

        private static X509Certificate2 CreateRootCertificate(RSA rootKey)
        {
            var request = new CertificateRequest(
                "CN=MWB IPC Test Root",
                rootKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 1, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7));
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

            using var intermediate = request.Create(root, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7), RandomNumberGenerator.GetBytes(16));
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

            return request.Create(intermediate, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(7), RandomNumberGenerator.GetBytes(16));
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
