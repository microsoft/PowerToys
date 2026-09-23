// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using PowerToys.ProtectedStorage;

namespace WorkspacesCsharpLibrary.Data;

/// <summary>Only the fixed legacy file is eligible for automatic migration or cleanup.</summary>
public sealed class LegacyWorkspaceSource : ILegacyWorkspaceSource
{
    private const string Origin = "workspaces.localappdata.v1";
    private readonly string path;

    public LegacyWorkspaceSource()
        : this(WorkspacesStorage.GetLegacyFilePath())
    {
    }

    public LegacyWorkspaceSource(string path)
    {
        this.path = Path.GetFullPath(path);
    }

    public LegacyWorkspaceSnapshot OpenStableSnapshot(Guid migrationId)
    {
        return RunAsOwner(() => OpenSnapshotCore(migrationId));
    }

    public bool DeleteIfUnchanged(string receipt)
    {
        return RunAsOwner(() => DeleteCore(receipt));
    }

    private LegacyWorkspaceSnapshot OpenSnapshotCore(Guid migrationId)
    {
        using var handle = Open(false);
        if (handle == null)
        {
            return null;
        }

        using var stream = new FileStream(handle, FileAccess.Read);
        var identity = Identify(handle);
        var bytes = ReadBounded(stream);
        var receipt = new SourceReceipt(Origin, migrationId, identity, bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)));
        return new LegacyWorkspaceSnapshot(bytes, JsonSerializer.Serialize(receipt, SourceReceiptContext.Default.SourceReceipt));
    }

    private bool DeleteCore(string receipt)
    {
        var expected = JsonSerializer.Deserialize(receipt, SourceReceiptContext.Default.SourceReceipt);
        if (expected == null || expected.Origin != Origin || expected.MigrationId == Guid.Empty)
        {
            throw new InvalidDataException("The migration cleanup receipt is invalid.");
        }

        using var handle = Open(true);
        if (handle == null)
        {
            return true;
        }

        using var stream = new FileStream(handle, FileAccess.Read);
        if (Identify(handle) != expected.Identity || stream.Length != expected.Length)
        {
            return false;
        }

        var hash = Convert.ToHexString(SHA256.HashData(ReadBounded(stream)));
        if (!string.Equals(hash, expected.Hash, StringComparison.Ordinal))
        {
            return false;
        }

        // Delete the verified object, not a path that could have been replaced after validation.
        var disposition = new FileDisposition { DeleteFile = true };
        if (!SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf<FileDisposition>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return true;
    }

    private SafeFileHandle Open(bool delete)
    {
        var handle = CreateFile(path, 0x80000000u | (delete ? 0x10000u : 0), 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is 2 or 3)
            {
                return null;
            }

            throw new Win32Exception(error);
        }

        try
        {
            if (!GetFileInformationByHandle(handle, out var information))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if ((information.Attributes & (0x400u | 0x10u)) != 0 || information.Links != 1)
            {
                throw new InvalidDataException("Legacy Workspaces data must be a regular, unlinked file.");
            }

            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static string Identify(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out var information))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return FormattableString.Invariant($"{information.Volume:X8}:{information.IndexHigh:X8}{information.IndexLow:X8}:{information.CreationTime:X16}");
    }

    private static byte[] ReadBounded(FileStream stream)
    {
        if (stream.Length <= 0 || stream.Length > WorkspacesValidator.MaximumBytes)
        {
            throw new InvalidDataException("Legacy Workspaces data is empty or too large.");
        }

        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static T RunAsOwner<T>(Func<T> operation)
    {
        using var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.Duplicate);
        if (identity.IsSystem || identity.ImpersonationLevel is not (TokenImpersonationLevel.None or TokenImpersonationLevel.Impersonation) ||
            !GetElevation(identity.AccessToken, 20, out uint elevated, sizeof(uint), out _))
        {
            throw new ProtectedStorageException("OwnerContextRequired");
        }

        if (elevated == 0)
        {
            return operation();
        }

        if (!GetLinkedToken(identity.AccessToken, 19, out var raw, IntPtr.Size, out _))
        {
            throw new ProtectedStorageException("OwnerContextRequired");
        }

        using var linked = new SafeAccessTokenHandle(raw);
        using var owner = new WindowsIdentity(linked.DangerousGetHandle());
        if (owner.User != identity.User || !GetElevation(linked, 20, out elevated, sizeof(uint), out _) || elevated != 0 ||
            owner.ImpersonationLevel is not (TokenImpersonationLevel.None or TokenImpersonationLevel.Impersonation))
        {
            throw new ProtectedStorageException("OwnerContextRequired");
        }

        // Automatic migration/cleanup must not acquire the elevated Editor's file rights.
        return WindowsIdentity.RunImpersonated(linked, operation);
    }

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetElevation(SafeAccessTokenHandle token, int informationClass, out uint value, int length, out int required);

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLinkedToken(SafeAccessTokenHandle token, int informationClass, out IntPtr value, int length, out int required);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int informationClass, ref FileDisposition information, uint length);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct FileInformation
    {
        public uint Attributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint Volume;
        public uint SizeHigh;
        public uint SizeLow;
        public uint Links;
        public uint IndexHigh;
        public uint IndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileDisposition
    {
        [MarshalAs(UnmanagedType.U1)]
        public bool DeleteFile;
    }
}
