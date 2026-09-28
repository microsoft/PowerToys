// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

using Microsoft.Win32.SafeHandles;

namespace Microsoft.PowerToys.Settings.UI.Library.Utilities
{
    public static class NamedPipePeerVerification
    {
        public static bool TryVerifyClient(
            NamedPipeServerStream stream,
            string expectedExeName,
            string intendedUserSid,
            int intendedSessionId,
            out string rejectionReason)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ValidateExpectedPeer(expectedExeName, intendedUserSid);

            if (!stream.IsConnected)
            {
                rejectionReason = "pipe-not-connected";
                return false;
            }

            if (!NativeMethods.GetNamedPipeClientProcessId(stream.SafePipeHandle, out var processId))
            {
                rejectionReason = "client-pid-unavailable";
                return false;
            }

            return TryVerifyPeerProcess(
                processId,
                expectedExeName,
                intendedUserSid,
                intendedSessionId,
                allowLocalSystem: false,
                out rejectionReason);
        }

        public static bool TryVerifyServer(
            NamedPipeClientStream stream,
            string expectedExeName,
            string intendedUserSid,
            int intendedSessionId,
            bool allowLocalSystem,
            out string rejectionReason)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ValidateExpectedPeer(expectedExeName, intendedUserSid);

            if (!stream.IsConnected)
            {
                rejectionReason = "pipe-not-connected";
                return false;
            }

            if (!NativeMethods.GetNamedPipeServerProcessId(stream.SafePipeHandle, out var processId))
            {
                rejectionReason = "server-pid-unavailable";
                return false;
            }

            return TryVerifyPeerProcess(
                processId,
                expectedExeName,
                intendedUserSid,
                intendedSessionId,
                allowLocalSystem,
                out rejectionReason);
        }

        private static void ValidateExpectedPeer(string expectedExeName, string intendedUserSid)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedExeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(intendedUserSid);

            if (!string.Equals(Path.GetFileName(expectedExeName), expectedExeName, StringComparison.Ordinal))
            {
                throw new ArgumentException("The expected executable name must not include a directory path.", nameof(expectedExeName));
            }
        }

        private static bool TryVerifyPeerProcess(
            uint processId,
            string expectedExeName,
            string intendedUserSid,
            int intendedSessionId,
            bool allowLocalSystem,
            out string rejectionReason)
        {
            try
            {
                using var processHandle = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
                if (processHandle.IsInvalid)
                {
                    rejectionReason = "identity-unavailable";
                    return false;
                }

                // Hold the process handle through the full check so the PID cannot be reused mid-verification.
                if (!NativeMethods.GetProcessTimes(processHandle, out var creationTime, out _, out _, out _))
                {
                    rejectionReason = "identity-unavailable";
                    return false;
                }

                if (creationTime.ToLong() == 0)
                {
                    rejectionReason = "invalid-process-instance";
                    return false;
                }

                if (!NativeMethods.ProcessIdToSessionId(processId, out var actualSessionId))
                {
                    rejectionReason = "identity-unavailable";
                    return false;
                }

                var actualImagePath = NativeMethods.GetProcessImagePath(processHandle);
                var ownImagePath = Environment.ProcessPath ?? throw new InvalidOperationException("The current process has no executable path.");

                if (!NativeMethods.OpenProcessToken(processHandle, NativeMethods.TokenQuery, out var tokenHandle))
                {
                    rejectionReason = "identity-unavailable";
                    return false;
                }

                string actualUserSid;
                using (tokenHandle)
                using (var identity = new WindowsIdentity(tokenHandle.DangerousGetHandle()))
                {
                    actualUserSid = identity.User?.Value ?? throw new InvalidOperationException("The process token has no user SID.");
                }

                if (unchecked((int)actualSessionId) != intendedSessionId)
                {
                    rejectionReason = "wrong-session";
                    return false;
                }

                var isLocalSystem = string.Equals(
                    actualUserSid,
                    new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null).Value,
                    StringComparison.OrdinalIgnoreCase);
                if (!string.Equals(actualUserSid, intendedUserSid, StringComparison.OrdinalIgnoreCase) &&
                    !(allowLocalSystem && isLocalSystem))
                {
                    rejectionReason = "wrong-user";
                    return false;
                }

                if (!TryVerifyPeerExecutableIdentity(
                        actualImagePath,
                        expectedExeName,
                        out var actualFullPath,
                        out rejectionReason))
                {
                    return false;
                }

                if (!MeetsSigningPolicy(ownImagePath, actualFullPath))
                {
                    rejectionReason = "untrusted-signature";
                    return false;
                }

                rejectionReason = string.Empty;
                return true;
            }
            catch (Win32Exception)
            {
                rejectionReason = "identity-unavailable";
                return false;
            }
            catch (InvalidOperationException)
            {
                rejectionReason = "identity-unavailable";
                return false;
            }
        }

        private static bool TryVerifyPeerExecutableIdentity(
            string peerImagePath,
            string expectedExeName,
            out string resolvedPeerImagePath,
            out string rejectionReason)
        {
            var ownImagePath = Environment.ProcessPath ?? throw new InvalidOperationException("The current process has no executable path.");
            return TryVerifyPeerExecutableIdentity(peerImagePath, ownImagePath, expectedExeName, out resolvedPeerImagePath, out rejectionReason);
        }

        private static bool TryVerifyPeerExecutableIdentity(
            string peerImagePath,
            string ownImagePath,
            string expectedExeName,
            out string resolvedPeerImagePath,
            out string rejectionReason)
        {
            resolvedPeerImagePath = string.Empty;
            var normalizedPeerImagePath = Path.GetFullPath(peerImagePath);
            var normalizedOwnImagePath = Path.GetFullPath(ownImagePath);

            if (!string.Equals(Path.GetFileName(normalizedPeerImagePath), expectedExeName, StringComparison.OrdinalIgnoreCase))
            {
                rejectionReason = "wrong-image";
                return false;
            }

            if (!HaveEqualOrNestedDirectories(
                    Path.GetDirectoryName(normalizedOwnImagePath) ?? throw new InvalidOperationException("The current process has no executable directory."),
                    Path.GetDirectoryName(normalizedPeerImagePath) ?? throw new InvalidOperationException("The peer process has no executable directory.")))
            {
                rejectionReason = "wrong-image";
                return false;
            }

            resolvedPeerImagePath = normalizedPeerImagePath;
            rejectionReason = string.Empty;
            return true;
        }

        private static bool HaveEqualOrNestedDirectories(string firstDirectory, string secondDirectory)
        {
            var normalizedFirst = NormalizeDirectoryPath(firstDirectory);
            var normalizedSecond = NormalizeDirectoryPath(secondDirectory);

            return string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase) ||
                IsNestedDirectory(normalizedFirst, normalizedSecond) ||
                IsNestedDirectory(normalizedSecond, normalizedFirst);
        }

        private static string NormalizeDirectoryPath(string directoryPath)
        {
            return Path.GetFullPath(directoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsNestedDirectory(string candidateDirectory, string parentDirectory)
        {
            var normalizedParent = parentDirectory + Path.DirectorySeparatorChar;
            return candidateDirectory.Length > normalizedParent.Length &&
                candidateDirectory.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MeetsSigningPolicy(string ownImagePath, string peerImagePath)
        {
            if (!TryHasEmbeddedAuthenticodeSignature(ownImagePath, out var ownHasEmbeddedSignature))
            {
                return false;
            }

            return !ownHasEmbeddedSignature || HaveSameTrustedMicrosoftSigner(ownImagePath, peerImagePath);
        }

        private static bool TryHasEmbeddedAuthenticodeSignature(string imagePath, out bool hasEmbeddedSignature)
        {
            hasEmbeddedSignature = false;
            try
            {
                using var image = File.OpenRead(imagePath);
                using var reader = new PEReader(image);
                var header = reader.PEHeaders.PEHeader;
                if (header == null)
                {
                    return false;
                }

                // A malformed signature must not be mistaken for an unsigned development binary.
                var certificateTable = header.CertificateTableDirectory;
                hasEmbeddedSignature = certificateTable.RelativeVirtualAddress != 0 || certificateTable.Size != 0;
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
        }

        private static bool HaveSameTrustedMicrosoftSigner(string firstImagePath, string secondImagePath)
        {
            X509Certificate2 firstSigner = null;
            X509Certificate2 secondSigner = null;

            try
            {
                if (!TryGetTrustedMicrosoftSignerCertificate(firstImagePath, out firstSigner) ||
                    !TryGetTrustedMicrosoftSignerCertificate(secondImagePath, out secondSigner))
                {
                    return false;
                }

                return HaveMatchingSignerCertificate(firstSigner, secondSigner);
            }
            finally
            {
                firstSigner?.Dispose();
                secondSigner?.Dispose();
            }
        }

        private static bool HaveMatchingSignerCertificate(X509Certificate2 firstSigner, X509Certificate2 secondSigner)
        {
            ArgumentNullException.ThrowIfNull(firstSigner);
            ArgumentNullException.ThrowIfNull(secondSigner);

            return CryptographicOperations.FixedTimeEquals(firstSigner.RawData, secondSigner.RawData);
        }

        private static bool HasTrustedMicrosoftSignature(string imagePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

            X509Certificate2 signer = null;
            try
            {
                return TryGetTrustedMicrosoftSignerCertificate(imagePath, out signer);
            }
            finally
            {
                signer?.Dispose();
            }
        }

        private static bool TryGetTrustedMicrosoftSignerCertificate(string imagePath, out X509Certificate2 signer)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

            signer = null;

            try
            {
                if (!TryVerifyAuthenticodeSignature(imagePath, out var verificationTime))
                {
                    return false;
                }

#pragma warning disable SYSLIB0057 // Embedded Authenticode signer extraction has no X509CertificateLoader equivalent.
                using var imageSigner = new X509Certificate2(X509Certificate.CreateFromSignedFile(imagePath));
#pragma warning restore SYSLIB0057
                if (!imageSigner.Subject.Contains("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                using var roots = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                roots.Open(OpenFlags.ReadOnly);

                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Offline;
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.IgnoreEndRevocationUnknown | X509VerificationFlags.IgnoreCertificateAuthorityRevocationUnknown | X509VerificationFlags.IgnoreRootRevocationUnknown;

                // Check lifetime at the same current time or validated timestamp that Windows used.
                chain.ChainPolicy.VerificationTime = verificationTime;
                chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.3"));

                var rootCertificates = roots.Certificates;
                try
                {
                    chain.ChainPolicy.CustomTrustStore.AddRange(rootCertificates);

                    if (TryGetEmbeddedPkcs7Store(imagePath, out var store, out var message))
                    {
                        using (store)
                        using (message)
                        using (var embeddedStore = new X509Store(store.DangerousGetHandle()))
                        {
                            var extraCertificates = embeddedStore.Certificates;
                            try
                            {
                                chain.ChainPolicy.ExtraStore.AddRange(extraCertificates);
                                if (!chain.Build(imageSigner))
                                {
                                    return false;
                                }

                                signer = X509CertificateLoader.LoadCertificate(imageSigner.RawData);
                                return true;
                            }
                            finally
                            {
                                DisposeCertificates(extraCertificates);
                            }
                        }
                    }

                    if (!chain.Build(imageSigner))
                    {
                        return false;
                    }

                    signer = X509CertificateLoader.LoadCertificate(imageSigner.RawData);
                    return true;
                }
                finally
                {
                    DisposeCertificates(rootCertificates);
                }
            }
            catch (CryptographicException)
            {
                signer?.Dispose();
                signer = null;
                return false;
            }
        }

        private static void DisposeCertificates(X509Certificate2Collection certificates)
        {
            foreach (var certificate in certificates)
            {
                certificate.Dispose();
            }
        }

        private static bool HasIntactAuthenticodeSignature(string imagePath)
        {
            return TryVerifyAuthenticodeSignature(imagePath, out _);
        }

        private static bool TryVerifyAuthenticodeSignature(string imagePath, out DateTime verificationTime)
        {
            verificationTime = default;
            var fileInfo = new WinTrustFileInfo
            {
                StructSize = unchecked((uint)Marshal.SizeOf<WinTrustFileInfo>()),
                FilePath = imagePath,
            };
            var fileInfoPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
            try
            {
                Marshal.StructureToPtr(fileInfo, fileInfoPointer, false);
                var trustData = new WinTrustData
                {
                    StructSize = unchecked((uint)Marshal.SizeOf<WinTrustData>()),
                    UiChoice = NativeMethods.WinTrustUiNone,
                    RevocationChecks = NativeMethods.WinTrustRevokeNone,
                    UnionChoice = NativeMethods.WinTrustChoiceFile,
                    FileInfo = fileInfoPointer,
                    StateAction = NativeMethods.WinTrustStateActionVerify,
                    ProviderFlags = NativeMethods.WinTrustSaferFlag | NativeMethods.WinTrustCacheOnlyUrlRetrieval,
                };

                var action = NativeMethods.WinTrustActionGenericVerifyV2;
                var status = NativeMethods.WinVerifyTrust(new IntPtr(-1), ref action, ref trustData);
                try
                {
                    if (status != 0)
                    {
                        return false;
                    }

                    var provider = NativeMethods.WTHelperProvDataFromStateData(trustData.StateData);
                    if (provider == IntPtr.Zero)
                    {
                        return false;
                    }

                    var signer = NativeMethods.WTHelperGetProvSignerFromChain(provider, 0, false, 0);
                    if (signer == IntPtr.Zero)
                    {
                        return false;
                    }

                    var signerTime = Marshal.PtrToStructure<WinTrustSignerTime>(signer);
                    if (signerTime.StructSize < Marshal.SizeOf<WinTrustSignerTime>())
                    {
                        return false;
                    }

                    verificationTime = DateTime.FromFileTimeUtc(signerTime.VerificationTime.ToLong());
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }
                finally
                {
                    trustData.StateAction = NativeMethods.WinTrustStateActionClose;
                    _ = NativeMethods.WinVerifyTrust(new IntPtr(-1), ref action, ref trustData);
                }
            }
            finally
            {
                Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
                Marshal.FreeHGlobal(fileInfoPointer);
            }
        }

        private static bool TryGetEmbeddedPkcs7Store(
            string imagePath,
            out SafeCertStoreHandle store,
            out SafeCryptMsgHandle message)
        {
            if (NativeMethods.CryptQueryObject(
                    NativeMethods.CertQueryObjectFile,
                    imagePath,
                    NativeMethods.CertQueryContentFlagPkcs7SignedEmbed,
                    NativeMethods.CertQueryFormatFlagBinary,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    out store,
                    out message,
                    IntPtr.Zero))
            {
                return true;
            }

            store = new SafeCertStoreHandle();
            message = new SafeCryptMsgHandle();
            return false;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            internal uint LowDateTime;
            internal uint HighDateTime;

            internal long ToLong()
            {
                return unchecked((long)(((ulong)HighDateTime << 32) | LowDateTime));
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo
        {
            internal uint StructSize;
            internal string FilePath;
            internal IntPtr FileHandle;
            internal IntPtr KnownSubject;
        }

        // Only the fixed prefix of CRYPT_PROVIDER_SGNR is needed.
        [StructLayout(LayoutKind.Sequential)]
        private struct WinTrustSignerTime
        {
            internal uint StructSize;
            internal FileTime VerificationTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustData
        {
            internal uint StructSize;
            internal IntPtr PolicyCallbackData;
            internal IntPtr SipClientData;
            internal uint UiChoice;
            internal uint RevocationChecks;
            internal uint UnionChoice;
            internal IntPtr FileInfo;
            internal uint StateAction;
            internal IntPtr StateData;
            internal string UrlReference;
            internal uint ProviderFlags;
            internal uint UiContext;
        }

        private sealed class SafeCertStoreHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeCertStoreHandle()
                : base(true)
            {
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.CertCloseStore(handle, 0);
            }
        }

        private sealed class SafeCryptMsgHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeCryptMsgHandle()
                : base(true)
            {
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.CryptMsgClose(handle);
            }
        }

        private static class NativeMethods
        {
            internal const uint ProcessQueryLimitedInformation = 0x1000;
            internal const uint TokenQuery = 0x0008;
            internal const uint WinTrustUiNone = 2;
            internal const uint WinTrustRevokeNone = 0;
            internal const uint WinTrustChoiceFile = 1;
            internal const uint WinTrustStateActionVerify = 1;
            internal const uint WinTrustStateActionClose = 2;
            internal const uint WinTrustSaferFlag = 0x100;
            internal const uint WinTrustCacheOnlyUrlRetrieval = 0x1000;
            internal const uint CertQueryObjectFile = 0x00000001;
            internal const uint CertQueryContentFlagPkcs7SignedEmbed = 0x00000400;
            internal const uint CertQueryFormatFlagBinary = 0x00000002;
            internal static readonly Guid WinTrustActionGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern SafeProcessHandle OpenProcess(
                uint processAccess,
                [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
                uint processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetProcessTimes(
                SafeProcessHandle process,
                out FileTime creationTime,
                out FileTime exitTime,
                out FileTime kernelTime,
                out FileTime userTime);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool ProcessIdToSessionId(uint processId, out uint sessionId);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool OpenProcessToken(
                SafeProcessHandle processHandle,
                uint desiredAccess,
                out SafeAccessTokenHandle tokenHandle);

            [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool QueryFullProcessImageName(
                SafeProcessHandle process,
                uint flags,
                char[] exeName,
                ref uint size);

            [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
            internal static extern int WinVerifyTrust(
                IntPtr windowHandle,
                [In] ref Guid actionId,
                ref WinTrustData trustData);

            [DllImport("wintrust.dll", ExactSpelling = true)]
            internal static extern IntPtr WTHelperProvDataFromStateData(IntPtr stateData);

            [DllImport("wintrust.dll", ExactSpelling = true)]
            internal static extern IntPtr WTHelperGetProvSignerFromChain(
                IntPtr providerData,
                uint signerIndex,
                [MarshalAs(UnmanagedType.Bool)] bool counterSigner,
                uint counterSignerIndex);

            [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptQueryObject(
                uint objectType,
                string @object,
                uint expectedContentTypeFlags,
                uint expectedFormatTypeFlags,
                uint flags,
                IntPtr messageAndCertEncodingType,
                IntPtr contentType,
                IntPtr formatType,
                out SafeCertStoreHandle certStore,
                out SafeCryptMsgHandle message,
                IntPtr context);

            [DllImport("crypt32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CertCloseStore(IntPtr certStore, uint flags);

            [DllImport("crypt32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CryptMsgClose(IntPtr cryptMsg);

            internal static string GetProcessImagePath(SafeProcessHandle process)
            {
                var buffer = new char[32768];
                var length = unchecked((uint)buffer.Length);
                if (!QueryFullProcessImageName(process, 0, buffer, ref length))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return new string(buffer, 0, unchecked((int)length));
            }
        }
    }
}
