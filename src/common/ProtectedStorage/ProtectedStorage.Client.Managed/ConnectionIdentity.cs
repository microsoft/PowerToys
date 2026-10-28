// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace PowerToys.ProtectedStorage;

internal static class ConnectionIdentity
{
    internal static void ValidateOwnerContext()
    {
        using var linked = GetFilteredToken();
    }

    internal static async Task ConnectAsync(NamedPipeClientStream pipe, CancellationToken cancellationToken)
    {
        using var linked = GetFilteredToken();
        if (linked == null)
        {
            await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // The service may impersonate the ordinary owner only to inspect the signed
        // caller image in a per-user installation. Never expose an elevated token.
        await WindowsIdentity.RunImpersonatedAsync(linked, () => pipe.ConnectAsync(cancellationToken)).ConfigureAwait(false);
    }

    private static SafeAccessTokenHandle? GetFilteredToken()
    {
        using var owner = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.Duplicate);
        if (owner.IsSystem || owner.ImpersonationLevel is not (TokenImpersonationLevel.None or TokenImpersonationLevel.Impersonation))
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: 1346);
        }

        if (!GetElevation(owner.AccessToken, 20, out uint elevated, sizeof(uint), out _))
        {
            throw new ProtectedStorageException("Unauthorized");
        }

        if (elevated == 0)
        {
            return null;
        }

        if (!GetLinkedToken(owner.AccessToken, 19, out var raw, IntPtr.Size, out _))
        {
            throw new ProtectedStorageException("OwnerContextRequired");
        }

        var linked = new SafeAccessTokenHandle(raw);
        try
        {
            using var identity = new WindowsIdentity(linked.DangerousGetHandle());
            if (identity.User != owner.User || !GetElevation(linked, 20, out elevated, sizeof(uint), out _) || elevated != 0 ||
                identity.ImpersonationLevel is not (TokenImpersonationLevel.None or TokenImpersonationLevel.Impersonation))
            {
                throw new ProtectedStorageException("OwnerContextRequired", nativeCode: 1346);
            }

            return linked;
        }
        catch
        {
            linked.Dispose();
            throw;
        }
    }

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetElevation(SafeAccessTokenHandle token, int informationClass, out uint value, int length, out int required);

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLinkedToken(SafeAccessTokenHandle token, int informationClass, out IntPtr value, int length, out int required);
}
