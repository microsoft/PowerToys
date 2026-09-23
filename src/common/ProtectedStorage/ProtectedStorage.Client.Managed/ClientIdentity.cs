// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace PowerToys.ProtectedStorage;

internal static class ClientIdentity
{
    internal const int ProcessIdentityAccess = 0x101400;
    internal const int TokenIdentityAccess = 8;
    private static readonly object Gate = new();

    internal static void RequireNormalMaintenanceOwner()
    {
        using var process = new SafeProcessHandle(new IntPtr(-1), false);
        if (!OpenProcessToken(process, 8, out var token))
        {
            throw new ProtectedStorageException("OwnerContextRequired");
        }

        using (token)
        using (var primary = new WindowsIdentity(token.DangerousGetHandle()))
        using (var current = WindowsIdentity.GetCurrent())
        {
            if (primary.IsSystem || primary.User == null || primary.User != current.User ||
                !GetTokenElevation(token, 20, out uint elevated, sizeof(uint), out _) || elevated != 0)
            {
                throw new ProtectedStorageException("OwnerContextRequired");
            }
        }
    }

    internal static void AllowServiceQuery(string owner)
    {
        SecurityIdentifier service;
        try
        {
            service = (SecurityIdentifier)new NTAccount($@"NT SERVICE\PowerToysProtectedStorage_{owner}").Translate(typeof(SecurityIdentifier));
        }
        catch (IdentityNotMappedException exception)
        {
            throw new ProtectedStorageException("NotProvisioned", innerException: exception);
        }

        lock (Gate)
        {
            using var process = new SafeProcessHandle(new IntPtr(-1), false);

            // ProcessImageFileMapping requires QUERY_INFORMATION, not QUERY_LIMITED_INFORMATION.
            Grant(process.DangerousGetHandle(), service, ProcessIdentityAccess);
            if (!OpenProcessToken(process, 8 | 0x20000 | 0x40000, out var token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            using (token)
            {
                Grant(token.DangerousGetHandle(), service, TokenIdentityAccess);
            }
        }
    }

    private static void Grant(IntPtr handle, SecurityIdentifier service, int access)
    {
        uint error = GetSecurityInfo(handle, 6, 4, IntPtr.Zero, IntPtr.Zero, out _, IntPtr.Zero, out var descriptor);
        if (error != 0)
        {
            throw new Win32Exception((int)error);
        }

        try
        {
            uint length = GetSecurityDescriptorLength(descriptor);
            if (length == 0 || length > 65536)
            {
                throw new ProtectedStorageException("Unauthorized");
            }

            var bytes = new byte[length];
            Marshal.Copy(descriptor, bytes, 0, bytes.Length);
            var security = new RawSecurityDescriptor(bytes, 0);
            var acl = security.DiscretionaryAcl ?? throw new ProtectedStorageException("Unauthorized");
            foreach (GenericAce entry in acl)
            {
                if (entry is CommonAce ace && ace.AceQualifier == AceQualifier.AccessAllowed &&
                    ace.SecurityIdentifier == service && (ace.AccessMask & access) == access)
                {
                    return;
                }
            }

            int index = 0;
            while (index < acl.Count && (acl[index].AceFlags & AceFlags.Inherited) == 0)
            {
                index++;
            }

            acl.InsertAce(index, new CommonAce(AceFlags.None, AceQualifier.AccessAllowed, access, service, false, null));
            var updated = new byte[acl.BinaryLength];
            acl.GetBinaryForm(updated, 0);
            error = SetSecurityInfo(handle, 6, 4, IntPtr.Zero, IntPtr.Zero, updated, IntPtr.Zero);
            if (error != 0)
            {
                throw new Win32Exception((int)error);
            }
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenElevation(SafeAccessTokenHandle token, int informationClass, out uint value, int length, out int required);

    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityInfo(IntPtr handle, int type, uint information, IntPtr owner, IntPtr group, out IntPtr dacl, IntPtr sacl, out IntPtr descriptor);

    [DllImport("advapi32.dll")]
    private static extern uint SetSecurityInfo(IntPtr handle, int type, uint information, IntPtr owner, IntPtr group, byte[] dacl, IntPtr sacl);

    [DllImport("advapi32.dll")]
    private static extern uint GetSecurityDescriptorLength(IntPtr descriptor);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
