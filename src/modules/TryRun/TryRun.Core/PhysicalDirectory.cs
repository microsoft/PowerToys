// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PowerToys.TryRun.Core;

internal static partial class PhysicalDirectory
{
    public static string Resolve(string path)
    {
        // MSIX filesystem virtualization is not a reparse point. Lexical path
        // normalization alone can name a different directory in the sandbox.
        using var handle = CreateFileW(path, 0x80, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            throw new IOException("Could not open the session directory.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        return Resolve(handle);
    }

    internal static unsafe string Resolve(SafeFileHandle handle)
    {
        var buffer = new char[512];
        while (true)
        {
            uint length;
            fixed (char* destination = buffer)
            {
                length = GetFinalPathNameByHandleW(handle, destination, (uint)buffer.Length, 0);
            }

            if (length == 0)
            {
                throw new IOException("Could not resolve the physical session directory.", new Win32Exception(Marshal.GetLastPInvokeError()));
            }

            if (length >= buffer.Length)
            {
                if (length > 32768)
                {
                    throw new PathTooLongException("The physical session directory path is too long.");
                }

                buffer = new char[length + 1];
                continue;
            }

            var resolved = new string(buffer, 0, (int)length);
            if (!resolved.StartsWith("\\\\?\\", StringComparison.Ordinal) || resolved.Length < 7 || !char.IsAsciiLetter(resolved[4]) || resolved[5] != ':')
            {
                throw new IOException("Try Run requires a local drive for its session directories.");
            }

            return Path.TrimEndingDirectorySeparator(resolved[4..]);
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr securityAttributes, uint disposition, uint flags, IntPtr template);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true)]
    private static unsafe partial uint GetFinalPathNameByHandleW(SafeFileHandle handle, char* path, uint pathLength, uint flags);
}
