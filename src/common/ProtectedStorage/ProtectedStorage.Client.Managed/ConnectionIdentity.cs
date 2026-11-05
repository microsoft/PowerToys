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
        using var token = GetFilteredToken();
    }

    internal static async Task ConnectAsync(NamedPipeClientStream pipe, CancellationToken cancellationToken)
    {
        using var token = GetFilteredToken();
        if (token == null)
        {
            await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        // The service may impersonate the ordinary owner only to inspect the signed
        // caller image in a per-user installation. Never expose an elevated token.
        await WindowsIdentity.RunImpersonatedAsync(token, () => pipe.ConnectAsync(cancellationToken)).ConfigureAwait(false);
    }

    private static SafeAccessTokenHandle? GetFilteredToken()
    {
        using var owner = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.Duplicate);
        if (owner.IsSystem || owner.User == null || owner.ImpersonationLevel is not (TokenImpersonationLevel.None or TokenImpersonationLevel.Impersonation))
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: 1346);
        }

        if (!GetTokenValue(owner.AccessToken, 20, out uint elevated, sizeof(uint), out _))
        {
            throw new ProtectedStorageException("Unauthorized");
        }

        if (elevated == 0)
        {
            return null;
        }

        if (!GetTokenValue(owner.AccessToken, 12, out uint session, sizeof(uint), out _))
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: Marshal.GetLastWin32Error());
        }

        return OpenDesktopOwnerToken(owner.User, session);
    }

    private static SafeAccessTokenHandle OpenDesktopOwnerToken(SecurityIdentifier owner, uint session)
    {
        IntPtr shell = GetShellWindow();
        if (session == 0 || shell == IntPtr.Zero || GetWindowThreadProcessId(shell, out uint pid) == 0 || pid == 0)
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: 1312);
        }

        using var process = OpenProcess(0x101000, false, pid);
        if (process.IsInvalid)
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: Marshal.GetLastWin32Error());
        }

        var image = new char[32768];
        uint length = (uint)image.Length;
        if (!QueryFullProcessImageName(process, 0, image, ref length))
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: Marshal.GetLastWin32Error());
        }

        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(windows) ||
            !string.Equals(new string(image, 0, (int)length), Path.Combine(windows, "explorer.exe"), StringComparison.OrdinalIgnoreCase))
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: 1307);
        }

        if (!OpenProcessToken(process, 0x0008 | 0x0002, out var token))
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: Marshal.GetLastWin32Error());
        }

        try
        {
            ValidateDesktopOwnerToken(token, owner, session);
            if (WaitForSingleObject(process, 0) != 258)
            {
                throw new ProtectedStorageException("OwnerContextRequired", nativeCode: 1312);
            }

            return token;
        }
        catch
        {
            token.Dispose();
            throw;
        }
    }

    internal static void ValidateDesktopOwnerToken(SafeAccessTokenHandle token, SecurityIdentifier owner, uint session)
    {
        using var identity = new WindowsIdentity(token.DangerousGetHandle());
        if (!GetTokenValue(token, 12, out uint actualSession, sizeof(uint), out _) ||
            !GetTokenValue(token, 20, out uint elevated, sizeof(uint), out _))
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: Marshal.GetLastWin32Error());
        }

        if (identity.User != owner || actualSession != session)
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: 1307);
        }

        GetTokenBuffer(token, 25, IntPtr.Zero, 0, out int size);
        if (size <= 0 || size > 65536)
        {
            throw new ProtectedStorageException("OwnerContextRequired", nativeCode: Marshal.GetLastWin32Error());
        }

        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!GetTokenBuffer(token, 25, buffer, size, out _))
            {
                throw new ProtectedStorageException("OwnerContextRequired", nativeCode: Marshal.GetLastWin32Error());
            }

            var integrity = new SecurityIdentifier(Marshal.ReadIntPtr(buffer));
            if (elevated != 0 || identity.ImpersonationLevel != TokenImpersonationLevel.None || integrity.Value != "S-1-16-8192")
            {
                throw new ProtectedStorageException("OwnerContextRequired", nativeCode: 1346);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenValue(SafeAccessTokenHandle token, int informationClass, out uint value, int length, out int required);

    [DllImport("advapi32.dll", EntryPoint = "GetTokenInformation", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenBuffer(SafeAccessTokenHandle token, int informationClass, IntPtr value, int length, out int required);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] name, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);
}
