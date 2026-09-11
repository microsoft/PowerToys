// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace WorkspacesLauncherUI.UnitTests
{
    internal sealed class LauncherPipeChild : IAsyncDisposable
    {
        private readonly Process _process;

        private LauncherPipeChild(Process process, string pipeName)
        {
            _process = process;
            PipeName = pipeName;
        }

        internal string PipeName { get; }

        internal int ProcessId => _process.Id;

        internal static async Task<LauncherPipeChild> StartAsync()
        {
            var pipeName = "PowerToys.Workspaces.UnitTests." + Guid.NewGuid().ToString("N");
            var source = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "LauncherPipeChild.ps1"));
            var command = "& { " + source + Environment.NewLine + " } -PipeName '" + pipeName + "'";
            var start = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", Convert.ToBase64String(Encoding.Unicode.GetBytes(command)) })
            {
                start.ArgumentList.Add(argument);
            }

            var process = Process.Start(start);
            var child = new LauncherPipeChild(process, pipeName);
            try
            {
                await child.ExpectOutputAsync("ready");
                return child;
            }
            catch
            {
                await child.DisposeAsync();
                throw;
            }
        }

        internal Task WaitForConnectionAsync() => ExpectOutputAsync("connected");

        internal async Task SendShutdownAsync()
        {
            await _process.StandardInput.WriteLineAsync("shutdown").WaitAsync(LauncherPipeTestSession.Bound);
            await _process.StandardInput.FlushAsync().WaitAsync(LauncherPipeTestSession.Bound);
            await ExpectOutputAsync("sent");
        }

        internal async Task ExitAsync()
        {
            await _process.StandardInput.WriteLineAsync("exit").WaitAsync(LauncherPipeTestSession.Bound);
            await _process.StandardInput.FlushAsync().WaitAsync(LauncherPipeTestSession.Bound);
            await _process.WaitForExitAsync().WaitAsync(LauncherPipeTestSession.Bound);
            Assert.AreEqual(0, _process.ExitCode, await _process.StandardError.ReadToEndAsync());
        }

        internal async Task<byte[]> ReadReadyFrameAsync()
        {
            await _process.StandardInput.WriteLineAsync("read-ready").WaitAsync(LauncherPipeTestSession.Bound);
            await _process.StandardInput.FlushAsync().WaitAsync(LauncherPipeTestSession.Bound);
            var value = await _process.StandardOutput.ReadLineAsync().WaitAsync(LauncherPipeTestSession.Bound);
            return Convert.FromBase64String(value);
        }

        internal async Task<SafePipeHandle> DuplicateServerAsync()
        {
            await _process.StandardInput.WriteLineAsync("pipe-handle").WaitAsync(LauncherPipeTestSession.Bound);
            await _process.StandardInput.FlushAsync().WaitAsync(LauncherPipeTestSession.Bound);
            var value = await _process.StandardOutput.ReadLineAsync().WaitAsync(LauncherPipeTestSession.Bound);
            var sourceHandle = new IntPtr(long.Parse(value, CultureInfo.InvariantCulture));
            using var process = NativeMethods.OpenProcess(0x0040, false, (uint)_process.Id);
            if (process.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!NativeMethods.DuplicateHandle(process, sourceHandle, new IntPtr(-1), out var duplicate, 0, false, 2))
            {
                var error = new Win32Exception(Marshal.GetLastWin32Error());
                duplicate.Dispose();
                throw error;
            }

            // Retain, but do not rebind the child's overlapped handle to this process's completion port.
            return duplicate;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }

                await _process.WaitForExitAsync().WaitAsync(LauncherPipeTestSession.Bound);
            }
            finally
            {
                _process.Dispose();
            }
        }

        private async Task ExpectOutputAsync(string expected)
        {
            var actual = await _process.StandardOutput.ReadLineAsync().WaitAsync(LauncherPipeTestSession.Bound);
            Assert.AreEqual(expected, actual);
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool DuplicateHandle(
                SafeProcessHandle sourceProcess,
                IntPtr sourceHandle,
                IntPtr targetProcess,
                out SafePipeHandle targetHandle,
                uint access,
                [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
                uint options);
        }
    }
}
