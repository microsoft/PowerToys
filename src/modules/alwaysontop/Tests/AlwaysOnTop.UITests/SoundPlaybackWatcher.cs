// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.AlwaysOnTop.UITests;

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
    private readonly string mediaDirectory;
    private readonly bool createdTarget;
    private readonly bool createdDirectory;
    private readonly SafeFileHandle fileHandle;
    private readonly EventWaitHandle oplockBroken = new(false, EventResetMode.ManualReset);
    private readonly IntPtr overlapped;
    private bool disposed;

    private SoundPlaybackWatcher(string productRoot)
    {
        mediaDirectory = Path.Combine(productRoot, "Media");
        targetPath = Path.Combine(mediaDirectory, "Speech On.wav");
        createdDirectory = !Directory.Exists(mediaDirectory);
        Directory.CreateDirectory(mediaDirectory);

        createdTarget = !File.Exists(targetPath);
        if (createdTarget)
        {
            var sourcePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Media",
                "Speech On.wav");
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("The Windows Speech On sound used by the test fixture is unavailable.", sourcePath);
            }

            File.Copy(sourcePath, targetPath);
        }

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
            DeleteFixtureFiles();
            throw;
        }
    }

    internal string FilePath => targetPath;

    internal static SoundPlaybackWatcher Create()
    {
        var processes = Process.GetProcessesByName("PowerToys.AlwaysOnTop");
        try
        {
            var executable = processes
                .Select(process => process.MainModule?.FileName)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
                ?? throw new InvalidOperationException("The PowerToys.AlwaysOnTop executable path is unavailable.");
            return new SoundPlaybackWatcher(Path.GetDirectoryName(executable)!);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
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

        _ = CancelIoEx(fileHandle, overlapped);
        if (!GetOverlappedResult(fileHandle, overlapped, out _, wait: true))
        {
            const int errorOperationAborted = 995;
            var error = Marshal.GetLastWin32Error();
            if (error != errorOperationAborted)
            {
                throw new Win32Exception(error, $"Completing the oplock request for '{targetPath}' failed.");
            }
        }

        fileHandle.Dispose();
        Marshal.FreeHGlobal(overlapped);
        oplockBroken.Dispose();
        disposed = true;
        DeleteFixtureFiles();
    }

    private void DeleteFixtureFiles()
    {
        if (createdTarget)
        {
            DeleteFileWithRetry(targetPath);
        }

        if (createdDirectory && Directory.Exists(mediaDirectory) && !Directory.EnumerateFileSystemEntries(mediaDirectory).Any())
        {
            Directory.Delete(mediaDirectory);
        }
    }

    private static void DeleteFileWithRetry(string path)
    {
        for (var attempt = 1; attempt <= 50; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < 50)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 50)
            {
                Thread.Sleep(100);
            }
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
