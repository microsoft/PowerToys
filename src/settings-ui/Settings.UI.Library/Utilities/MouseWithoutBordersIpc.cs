// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading.Tasks;

using Microsoft.Win32.SafeHandles;

#pragma warning disable SA1402 // The IPC security types form one intentionally cohesive implementation unit.

namespace Microsoft.PowerToys.Settings.UI.Library.Utilities
{
    public static class MouseWithoutBordersIpc
    {
        public const string SettingsSyncProtocol = "PowerToys.MouseWithoutBorders.v2.SettingsSync";

        public static string GetSettingsSyncPipeName(int sessionId)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sessionId);

            return $"{SettingsSyncProtocol}.Session.{sessionId}";
        }

        public static string GetCurrentSettingsSyncPipeName()
        {
            return GetSettingsSyncPipeName(Process.GetCurrentProcess().SessionId);
        }

        public static string GetSettingsExecutablePath(string installDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(installDirectory);

            return Path.Combine(Path.GetFullPath(installDirectory), "WinUI3Apps", "PowerToys.Settings.exe");
        }

        public static string GetMouseWithoutBordersExecutablePath(string settingsDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settingsDirectory);

            var fullSettingsDirectory = Path.GetFullPath(settingsDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetFileName(fullSettingsDirectory), "WinUI3Apps", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The Settings directory must be the WinUI3Apps directory.", nameof(settingsDirectory));
            }

            var installDirectory = Directory.GetParent(fullSettingsDirectory)?.FullName
                ?? throw new ArgumentException("The Settings directory must have a parent directory.", nameof(settingsDirectory));
            return Path.Combine(installDirectory, "PowerToys.MouseWithoutBorders.exe");
        }

        public static void GrantCurrentProcessQueryAccess(SecurityIdentifier allowedUser)
        {
            ArgumentNullException.ThrowIfNull(allowedUser);

            using var processToken = OpenCurrentProcessTokenForDaclUpdate();
            SetKernelObjectDacl(
                processToken.DangerousGetHandle(),
                $"D:P(A;;0x{MwbIpcNativeMethods.TokenQuery:X};;;{allowedUser.Value})(A;;GA;;;SY)(A;;GA;;;BA)");
            SetKernelObjectDacl(
                MwbIpcNativeMethods.GetCurrentProcess(),
                $"D:P(A;;0x{MwbIpcNativeMethods.ProcessQueryLimitedInformation:X};;;{allowedUser.Value})(A;;GA;;;SY)(A;;GA;;;BA)");
        }

        private static SafeAccessTokenHandle OpenCurrentProcessTokenForDaclUpdate()
        {
            if (!MwbIpcNativeMethods.OpenCurrentProcessToken(
                    MwbIpcNativeMethods.GetCurrentProcess(),
                    MwbIpcNativeMethods.TokenQuery | MwbIpcNativeMethods.ReadControl | MwbIpcNativeMethods.WriteDac,
                    out var processToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return processToken;
        }

        private static void SetKernelObjectDacl(IntPtr handle, string sddl)
        {
            if (!MwbIpcNativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptor(
                    sddl,
                    MwbIpcNativeMethods.SddlRevision1,
                    out var securityDescriptor,
                    out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                if (!MwbIpcNativeMethods.SetKernelObjectSecurity(
                        handle,
                        MwbIpcNativeMethods.DaclSecurityInformation,
                        securityDescriptor))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                MwbIpcNativeMethods.LocalFree(securityDescriptor);
            }
        }
    }

    public sealed class NamedPipePeerIdentity
    {
        public int ProcessId { get; init; }

        public long CreationTimeUtcTicks { get; init; }

        public int SessionId { get; init; }

        public string UserSid { get; init; }

        public string ImagePath { get; init; }

        public string FileVersion { get; init; }

        public bool HasTrustedMicrosoftSignature { get; init; }
    }

    public interface INamedPipePeerIdentityProvider
    {
        NamedPipePeerIdentity GetIdentity(int processId);
    }

    public interface IProcessSignatureVerifier
    {
        bool HasTrustedMicrosoftSignature(string imagePath);
    }

    public interface IDeferredProcessSignatureVerifier
    {
        bool HasTrustedMicrosoftSignature(NamedPipePeerIdentity identity);
    }

    public sealed class NamedPipePeerPolicy
    {
        public int ExpectedSessionId { get; init; }

        public string ExpectedUserSid { get; init; }

        public string ExpectedImagePath { get; init; }

        public string ExpectedFileVersion { get; init; }

        public bool AllowLocalSystem { get; init; }

        public bool RequireMicrosoftSignature { get; init; } = true;
    }

    public sealed class NamedPipePeerAuthenticationResult
    {
        private NamedPipePeerAuthenticationResult(bool accepted, string reasonCode, NamedPipePeerIdentity identity)
        {
            Accepted = accepted;
            ReasonCode = reasonCode;
            Identity = identity;
        }

        public bool Accepted { get; }

        public string ReasonCode { get; }

        public NamedPipePeerIdentity Identity { get; }

        public static NamedPipePeerAuthenticationResult Accept(NamedPipePeerIdentity identity)
        {
            return new NamedPipePeerAuthenticationResult(true, string.Empty, identity);
        }

        public static NamedPipePeerAuthenticationResult Reject(string reasonCode, NamedPipePeerIdentity identity = null)
        {
            return new NamedPipePeerAuthenticationResult(false, reasonCode, identity);
        }
    }

    public sealed class NamedPipePeerAuthenticator
    {
        private readonly INamedPipePeerIdentityProvider identityProvider;
        private readonly Dictionary<CacheKey, NamedPipePeerAuthenticationResult> cache = new();
        private readonly object cacheLock = new();

        public NamedPipePeerAuthenticator(INamedPipePeerIdentityProvider identityProvider)
        {
            this.identityProvider = identityProvider ?? throw new ArgumentNullException(nameof(identityProvider));
        }

        public NamedPipePeerAuthenticationResult AuthenticateClient(NamedPipeServerStream stream, NamedPipePeerPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (!MwbIpcNativeMethods.GetNamedPipeClientProcessId(stream.SafePipeHandle, out var processId))
            {
                return NamedPipePeerAuthenticationResult.Reject("client-pid-unavailable");
            }

            return Authenticate(unchecked((int)processId), policy);
        }

        public NamedPipePeerAuthenticationResult AuthenticateClientAndExecute(
            NamedPipeServerStream stream,
            NamedPipePeerPolicy policy,
            Action authenticatedAction)
        {
            ArgumentNullException.ThrowIfNull(authenticatedAction);

            var result = AuthenticateClient(stream, policy);
            if (result.Accepted)
            {
                authenticatedAction();
            }

            return result;
        }

        public NamedPipePeerAuthenticationResult AuthenticateServer(NamedPipeClientStream stream, NamedPipePeerPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (!MwbIpcNativeMethods.GetNamedPipeServerProcessId(stream.SafePipeHandle, out var processId))
            {
                return NamedPipePeerAuthenticationResult.Reject("server-pid-unavailable");
            }

            return Authenticate(unchecked((int)processId), policy);
        }

        public NamedPipePeerAuthenticationResult Authenticate(int processId, NamedPipePeerPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

            NamedPipePeerIdentity identity;
            try
            {
                identity = identityProvider.GetIdentity(processId);
            }
            catch
            {
                return NamedPipePeerAuthenticationResult.Reject("identity-unavailable");
            }

            if (identity == null || identity.ProcessId != processId || identity.CreationTimeUtcTicks == 0)
            {
                return NamedPipePeerAuthenticationResult.Reject("invalid-process-instance", identity);
            }

            var key = new CacheKey(identity.ProcessId, identity.CreationTimeUtcTicks, PolicyFingerprint(policy));
            lock (cacheLock)
            {
                if (cache.TryGetValue(key, out var cached))
                {
                    return cached;
                }
            }

            var result = Evaluate(identity, policy);
            lock (cacheLock)
            {
                if (cache.Count >= 64)
                {
                    cache.Clear();
                }

                cache[key] = result;
            }

            return result;
        }

        private NamedPipePeerAuthenticationResult Evaluate(NamedPipePeerIdentity identity, NamedPipePeerPolicy policy)
        {
            if (identity.SessionId != policy.ExpectedSessionId)
            {
                return NamedPipePeerAuthenticationResult.Reject("wrong-session", identity);
            }

            var isSystem = string.Equals(identity.UserSid, WellKnownSidType.LocalSystemSid.GetSid(), StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(identity.UserSid, policy.ExpectedUserSid, StringComparison.OrdinalIgnoreCase) &&
                !(policy.AllowLocalSystem && isSystem))
            {
                return NamedPipePeerAuthenticationResult.Reject("wrong-user", identity);
            }

            if (!string.Equals(
                    Path.GetFullPath(identity.ImagePath),
                    Path.GetFullPath(policy.ExpectedImagePath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return NamedPipePeerAuthenticationResult.Reject("wrong-image", identity);
            }

            if (!string.IsNullOrEmpty(policy.ExpectedFileVersion) &&
                !string.Equals(identity.FileVersion, policy.ExpectedFileVersion, StringComparison.Ordinal))
            {
                return NamedPipePeerAuthenticationResult.Reject("wrong-version", identity);
            }

            if (policy.RequireMicrosoftSignature &&
                !(identityProvider is IDeferredProcessSignatureVerifier deferredSignatureVerifier
                    ? deferredSignatureVerifier.HasTrustedMicrosoftSignature(identity)
                    : identity.HasTrustedMicrosoftSignature))
            {
                return NamedPipePeerAuthenticationResult.Reject("untrusted-signature", identity);
            }

            return NamedPipePeerAuthenticationResult.Accept(identity);
        }

        private static string PolicyFingerprint(NamedPipePeerPolicy policy)
        {
            return string.Join(
                "|",
                policy.ExpectedSessionId,
                policy.ExpectedUserSid,
                policy.ExpectedImagePath,
                policy.ExpectedFileVersion,
                policy.AllowLocalSystem,
                policy.RequireMicrosoftSignature);
        }

        private readonly record struct CacheKey(int ProcessId, long CreationTimeUtcTicks, string PolicyFingerprint);
    }

    public sealed class WindowsNamedPipePeerIdentityProvider : INamedPipePeerIdentityProvider, IDeferredProcessSignatureVerifier
    {
        private readonly IProcessSignatureVerifier signatureVerifier;

        public WindowsNamedPipePeerIdentityProvider(IProcessSignatureVerifier signatureVerifier)
        {
            this.signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        }

        public NamedPipePeerIdentity GetIdentity(int processId)
        {
            using var processHandle = MwbIpcNativeMethods.OpenProcess(MwbIpcNativeMethods.ProcessQueryLimitedInformation, false, unchecked((uint)processId));
            if (processHandle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!MwbIpcNativeMethods.GetProcessTimes(processHandle, out var creationTime, out _, out _, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!MwbIpcNativeMethods.ProcessIdToSessionId(unchecked((uint)processId), out var sessionId))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var imagePath = MwbIpcNativeMethods.GetProcessImagePath(processHandle);
            var fileVersion = FileVersionInfo.GetVersionInfo(imagePath).FileVersion ?? string.Empty;

            if (!MwbIpcNativeMethods.OpenProcessToken(processHandle, MwbIpcNativeMethods.TokenQuery, out var tokenHandle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            string userSid;
            using (tokenHandle)
            using (var identity = new WindowsIdentity(tokenHandle.DangerousGetHandle()))
            {
                userSid = identity.User?.Value ?? throw new InvalidOperationException("The process token has no user SID.");
            }

            return new NamedPipePeerIdentity
            {
                ProcessId = processId,
                CreationTimeUtcTicks = creationTime.ToLong(),
                SessionId = unchecked((int)sessionId),
                UserSid = userSid,
                ImagePath = imagePath,
                FileVersion = fileVersion,
            };
        }

        public bool HasTrustedMicrosoftSignature(NamedPipePeerIdentity identity)
        {
            ArgumentNullException.ThrowIfNull(identity);

            return signatureVerifier.HasTrustedMicrosoftSignature(identity.ImagePath);
        }
    }

    public sealed class MicrosoftMachineRootSignatureVerifier : IProcessSignatureVerifier
    {
        public bool HasTrustedMicrosoftSignature(string imagePath)
        {
            try
            {
                if (!MwbIpcNativeMethods.HasIntactAuthenticodeSignature(imagePath))
                {
                    return false;
                }

#pragma warning disable SYSLIB0057 // Embedded Authenticode signer extraction has no X509CertificateLoader equivalent.
                using var signer = new X509Certificate2(X509Certificate.CreateFromSignedFile(imagePath));
#pragma warning restore SYSLIB0057
                if (!signer.Subject.Contains("Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                using var roots = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                roots.Open(OpenFlags.ReadOnly);

                using var chain = new X509Chain();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.AddRange(roots.Certificates);
                chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.3"));
                return chain.Build(signer);
            }
            catch
            {
                return false;
            }
        }
    }

    public static class MouseWithoutBordersIpcPolicy
    {
        public static NamedPipePeerPolicy CreateSettingsClientPolicy(string expectedSettingsPath, int sessionId, string userSid)
        {
            return CreatePolicy(expectedSettingsPath, sessionId, userSid, false);
        }

        public static NamedPipePeerPolicy CreateMwbServerPolicy(string expectedMwbPath, int sessionId, string userSid, bool allowLocalSystem)
        {
            return CreatePolicy(expectedMwbPath, sessionId, userSid, allowLocalSystem);
        }

        private static NamedPipePeerPolicy CreatePolicy(string expectedPath, int sessionId, string userSid, bool allowLocalSystem)
        {
            var fullPath = Path.GetFullPath(expectedPath);
            return new NamedPipePeerPolicy
            {
                ExpectedSessionId = sessionId,
                ExpectedUserSid = userSid,
                ExpectedImagePath = fullPath,
                ExpectedFileVersion = File.Exists(fullPath) ? FileVersionInfo.GetVersionInfo(fullPath).FileVersion : string.Empty,
                AllowLocalSystem = allowLocalSystem,
#if DEBUG
                RequireMicrosoftSignature = false,
#else
                RequireMicrosoftSignature = true,
#endif
            };
        }
    }

    public static class RestrictedNamedPipeServer
    {
        public static NamedPipeServerStream Create(string pipeName, SecurityIdentifier allowedUser)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
            ArgumentNullException.ThrowIfNull(allowedUser);

            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var sddl = $"D:P(A;;GA;;;{allowedUser.Value})";
            if (!allowedUser.Equals(systemSid))
            {
                sddl += $"(A;;GA;;;{systemSid.Value})";
            }

            if (!MwbIpcNativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptor(
                    sddl,
                    MwbIpcNativeMethods.SddlRevision1,
                    out var securityDescriptor,
                    out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                var securityAttributes = new MwbIpcNativeMethods.SecurityAttributes
                {
                    Length = Marshal.SizeOf<MwbIpcNativeMethods.SecurityAttributes>(),
                    SecurityDescriptor = securityDescriptor,
                    InheritHandle = false,
                };

                var handle = MwbIpcNativeMethods.CreateNamedPipe(
                    $@"\\.\pipe\{pipeName}",
                    MwbIpcNativeMethods.PipeAccessDuplex | MwbIpcNativeMethods.FileFlagOverlapped | MwbIpcNativeMethods.FileFlagFirstPipeInstance,
                    MwbIpcNativeMethods.PipeTypeByte | MwbIpcNativeMethods.PipeReadModeByte | MwbIpcNativeMethods.PipeWait | MwbIpcNativeMethods.PipeRejectRemoteClients,
                    1,
                    4096,
                    4096,
                    0,
                    ref securityAttributes);

                if (handle.IsInvalid)
                {
                    var error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    throw new Win32Exception(error);
                }

                return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle);
            }
            finally
            {
                MwbIpcNativeMethods.LocalFree(securityDescriptor);
            }
        }
    }

    public static class AuthenticatedNamedPipeClient
    {
        public static async Task<NamedPipeClientStream> ConnectAsync(
            string pipeName,
            NamedPipePeerPolicy serverPolicy,
            NamedPipePeerAuthenticator authenticator,
            int timeoutMilliseconds)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
            ArgumentNullException.ThrowIfNull(serverPolicy);
            ArgumentNullException.ThrowIfNull(authenticator);

            var stream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                await stream.ConnectAsync(timeoutMilliseconds);
                var authentication = authenticator.AuthenticateServer(stream, serverPolicy);
                if (!authentication.Accepted)
                {
                    throw new UnauthorizedAccessException($"Rejected named pipe server: {authentication.ReasonCode}");
                }

                return stream;
            }
            catch
            {
                await stream.DisposeAsync();
                throw;
            }
        }
    }

    internal static class WellKnownSidTypeExtensions
    {
        public static string GetSid(this WellKnownSidType sidType)
        {
            return new SecurityIdentifier(sidType, null).Value;
        }
    }

    internal static class MwbIpcNativeMethods
    {
        internal const uint ProcessQueryLimitedInformation = 0x1000;
        internal const uint TokenQuery = 0x0008;
        internal const uint ReadControl = 0x00020000;
        internal const uint WriteDac = 0x00040000;
        internal const uint PipeAccessDuplex = 0x00000003;
        internal const uint FileFlagFirstPipeInstance = 0x00080000;
        internal const uint FileFlagOverlapped = 0x40000000;
        internal const uint PipeTypeByte = 0x00000000;
        internal const uint PipeReadModeByte = 0x00000000;
        internal const uint PipeWait = 0x00000000;
        internal const uint PipeRejectRemoteClients = 0x00000008;
        internal const uint SddlRevision1 = 1;
        internal const uint DaclSecurityInformation = 0x00000004;
        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0;
        private const uint WtdChoiceFile = 1;
        private const uint WtdStateActionVerify = 1;
        private const uint WtdStateActionClose = 2;
        private const uint WtdSaferFlag = 0x100;
        private const uint WtdCacheOnlyUrlRetrieval = 0x1000;
        private static readonly Guid WinTrustActionGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [StructLayout(LayoutKind.Sequential)]
        internal struct SecurityAttributes
        {
            internal int Length;
            internal IntPtr SecurityDescriptor;

            [MarshalAs(UnmanagedType.Bool)]
            internal bool InheritHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FileTime
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

        [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafePipeHandle CreateNamedPipe(
            string name,
            uint openMode,
            uint pipeMode,
            uint maxInstances,
            uint outBufferSize,
            uint inBufferSize,
            uint defaultTimeout,
            ref SecurityAttributes securityAttributes);

        [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
            string stringSecurityDescriptor,
            uint stringSdRevision,
            out IntPtr securityDescriptor,
            out uint securityDescriptorSize);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr LocalFree(IntPtr memory);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetKernelObjectSecurity(
            IntPtr handle,
            uint securityInformation,
            IntPtr securityDescriptor);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint clientProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern SafeProcessHandle OpenProcess(uint processAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

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
        internal static extern bool OpenProcessToken(SafeProcessHandle processHandle, uint desiredAccess, out SafeAccessTokenHandle tokenHandle);

        [DllImport("advapi32.dll", EntryPoint = "OpenProcessToken", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenCurrentProcessToken(IntPtr processHandle, uint desiredAccess, out SafeAccessTokenHandle tokenHandle);

        [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, char[] exeName, ref uint size);

        [DllImport("wintrust.dll", ExactSpelling = true, SetLastError = true)]
        private static extern int WinVerifyTrust(IntPtr windowHandle, [In] ref Guid actionId, ref WinTrustData trustData);

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

        internal static bool HasIntactAuthenticodeSignature(string imagePath)
        {
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
                    UiChoice = WtdUiNone,
                    RevocationChecks = WtdRevokeNone,
                    UnionChoice = WtdChoiceFile,
                    FileInfo = fileInfoPointer,
                    StateAction = WtdStateActionVerify,
                    ProviderFlags = WtdSaferFlag | WtdCacheOnlyUrlRetrieval,
                };

                var action = WinTrustActionGenericVerifyV2;
                var status = WinVerifyTrust(new IntPtr(-1), ref action, ref trustData);
                trustData.StateAction = WtdStateActionClose;
                _ = WinVerifyTrust(new IntPtr(-1), ref action, ref trustData);
                return status == 0;
            }
            finally
            {
                Marshal.DestroyStructure<WinTrustFileInfo>(fileInfoPointer);
                Marshal.FreeHGlobal(fileInfoPointer);
            }
        }
    }
}
