// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.AlwaysOnTop.UITests;

/// <summary>
/// Provides the relative <c>Media\Speech On.wav</c> path used by Always On Top. The UI-test harness
/// launches PowerToys with its executable directory as the working directory, which child modules
/// inherit. The playback test's positive oplock assertion guards that launch contract.
/// </summary>
internal sealed class SoundFileFixture : IDisposable
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    private readonly string mediaDirectory;
    private readonly bool createdDirectory;
    private readonly bool createdFile;
    private readonly Action<string> log;
    private bool disposed;

    private SoundFileFixture(string executablePath, Action<string> log)
    {
        this.log = log;
        mediaDirectory = Path.Combine(Path.GetDirectoryName(executablePath)!, "Media");
        FilePath = Path.Combine(mediaDirectory, "Speech On.wav");
        createdDirectory = !Directory.Exists(mediaDirectory);

        try
        {
            Directory.CreateDirectory(mediaDirectory);
            createdFile = !File.Exists(FilePath);
            if (createdFile)
            {
                var sourcePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    "Media",
                    "Speech On.wav");
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException("The Windows Speech On sound used by the test fixture is unavailable.", sourcePath);
                }

                File.Copy(sourcePath, FilePath);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Dispose();
            throw new InvalidOperationException(
                $"The playback test requires write access to the module working directory '{mediaDirectory}'.",
                ex);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal string FilePath { get; }

    internal static SoundFileFixture Create(Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        var processes = Process.GetProcessesByName("PowerToys.AlwaysOnTop");
        var failures = new List<string>();
        try
        {
            foreach (var process in processes)
            {
                try
                {
                    return new SoundFileFixture(QueryExecutablePath(process.Id), log);
                }
                catch (Win32Exception ex)
                {
                    failures.Add($"PID {process.Id}: {ex.Message}");
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        throw new InvalidOperationException(
            $"The PowerToys.AlwaysOnTop executable path is unavailable. {string.Join("; ", failures)}");
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (createdFile)
        {
            DeleteFileWithRetry(FilePath);
        }

        if (createdDirectory && Directory.Exists(mediaDirectory) && !Directory.EnumerateFileSystemEntries(mediaDirectory).Any())
        {
            try
            {
                Directory.Delete(mediaDirectory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log($"Could not remove playback fixture directory '{mediaDirectory}': {ex.Message}");
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageNameW(
        SafeProcessHandle process,
        uint flags,
        StringBuilder executablePath,
        ref uint size);

    private static string QueryExecutablePath(int processId)
    {
        using var process = OpenProcess(ProcessQueryLimitedInformation, inheritHandle: false, processId);
        if (process.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Opening process {processId} failed.");
        }

        var capacity = 32_768;
        var path = new StringBuilder(capacity);
        var size = (uint)capacity;
        if (!QueryFullProcessImageNameW(process, 0, path, ref size))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Reading process {processId}'s executable path failed.");
        }

        return path.ToString();
    }

    private void DeleteFileWithRetry(string path)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 50; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                lastError = ex;
                Thread.Sleep(100);
            }
        }

        log($"Could not remove playback fixture file '{path}': {lastError?.Message ?? "unknown error"}");
    }
}
