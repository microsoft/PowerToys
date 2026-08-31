// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.AlwaysOnTop.UITests;

/// <summary>
/// Uses a level-1 NTFS oplock to observe the product opening its sound file without requiring an
/// audio endpoint. The signal is machine-wide, so callers verify it has not fired before triggering
/// the product action.
/// </summary>
internal sealed class SoundPlaybackWatcher : IDisposable
{
    private const uint FsctlRequestOplockLevel1 = 0x00090000;
    private const int ErrorIoPending = 997;
    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagOverlapped = 0x40000000;

    private readonly string targetPath;
    private readonly Action<string>? log;
    private readonly SafeFileHandle fileHandle;
    private readonly EventWaitHandle oplockBroken = new(false, EventResetMode.ManualReset);
    private readonly IntPtr overlapped;
    private bool disposed;

    private SoundPlaybackWatcher(string targetPath, Action<string>? log)
    {
        this.targetPath = targetPath;
        this.log = log;

        fileHandle = CreateFileW(
            targetPath,
            GenericRead,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagOverlapped,
            IntPtr.Zero);
        if (fileHandle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Opening '{targetPath}' for oplock monitoring failed.");
        }

        overlapped = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlappedData>());
        var data = new NativeOverlappedData
        {
            EventHandle = oplockBroken.SafeWaitHandle.DangerousGetHandle(),
        };
        Marshal.StructureToPtr(data, overlapped, fDeleteOld: false);
        try
        {
            RequestOplock();
        }
        catch
        {
            fileHandle.Dispose();
            Marshal.FreeHGlobal(overlapped);
            oplockBroken.Dispose();
            throw;
        }
    }

    internal string FilePath => targetPath;

    internal static SoundPlaybackWatcher Create(string targetPath, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        if (!File.Exists(targetPath))
        {
            throw new FileNotFoundException("The Always On Top sound file used by the playback watcher is unavailable.", targetPath);
        }

        return new SoundPlaybackWatcher(targetPath, log);
    }

    internal bool WaitForAccess(int timeoutMs)
    {
        return oplockBroken.WaitOne(timeoutMs);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            _ = CancelIoEx(fileHandle, overlapped);
            if (!GetOverlappedResult(fileHandle, overlapped, out _, wait: true))
            {
                const int errorOperationAborted = 995;
                var error = Marshal.GetLastWin32Error();
                if (error != errorOperationAborted)
                {
                    log?.Invoke($"Completing the sound-file oplock for '{targetPath}' failed: {new Win32Exception(error).Message}");
                }
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"Disposing the sound-file oplock for '{targetPath}' failed: {ex.Message}");
        }
        finally
        {
            fileHandle.Dispose();
            Marshal.FreeHGlobal(overlapped);
            oplockBroken.Dispose();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        [MarshalAs(UnmanagedType.LPWStr)] string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint ioControlCode,
        IntPtr inputBuffer,
        uint inputBufferSize,
        IntPtr outputBuffer,
        uint outputBufferSize,
        out uint bytesReturned,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(SafeFileHandle file, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetOverlappedResult(
        SafeFileHandle file,
        IntPtr overlapped,
        out uint bytesTransferred,
        [MarshalAs(UnmanagedType.Bool)] bool wait);

    private void RequestOplock()
    {
        if (DeviceIoControl(
            fileHandle,
            FsctlRequestOplockLevel1,
            IntPtr.Zero,
            0,
            IntPtr.Zero,
            0,
            out _,
            overlapped))
        {
            throw new InvalidOperationException($"The sound-file oplock on '{targetPath}' completed before playback was triggered.");
        }

        var error = Marshal.GetLastWin32Error();
        if (error != ErrorIoPending)
        {
            throw new Win32Exception(error, $"Requesting an oplock on '{targetPath}' failed.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeOverlappedData
    {
        internal IntPtr Internal;
        internal IntPtr InternalHigh;
        internal uint Offset;
        internal uint OffsetHigh;
        internal IntPtr EventHandle;
    }
}
