// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace PowerScripts.Core.Security;

/// <summary>
/// Launches a child process under the <b>interactive user's</b> token instead of the current
/// process' token. When PowerToys runs elevated, this drops a PowerScript back down to the ordinary
/// (non-administrator) user by borrowing the token of the shell (<c>explorer.exe</c>) — the standard
/// Windows technique for de-elevation. Standard output / error / input are redirected through
/// anonymous pipes so the caller keeps the same capture behavior it has for a normal launch.
///
/// Every failure path returns <c>null</c>: the caller must then <em>refuse to run</em> (fail closed)
/// rather than fall back to an elevated launch. This class is only ever taken when the host is
/// elevated; a non-elevated host launches scripts directly.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class ShellTokenProcessLauncher
{
    /// <summary>
    /// Runs <paramref name="startInfo"/> under the shell user's token. Returns the exit code and
    /// captured streams, or <c>null</c> when the process could not be de-elevated and launched (in
    /// which case the caller must not run the script at all).
    /// </summary>
    public static ProcessRunResult? TryRun(ProcessStartInfo startInfo, string? standardInput, int timeoutMs)
    {
        IntPtr shellToken = IntPtr.Zero;
        IntPtr primaryToken = IntPtr.Zero;
        AnonymousPipeServerStream? outPipe = null;
        AnonymousPipeServerStream? errPipe = null;
        AnonymousPipeServerStream? inPipe = null;

        try
        {
            shellToken = OpenShellToken();
            if (shellToken == IntPtr.Zero)
            {
                return null;
            }

            if (!DuplicateTokenEx(shellToken, MAXIMUM_ALLOWED, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out primaryToken))
            {
                return null;
            }

            outPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            errPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            inPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);

            var si = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFO>(),
                dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW,
                wShowWindow = SW_HIDE,
                hStdInput = inPipe.ClientSafePipeHandle.DangerousGetHandle(),
                hStdOutput = outPipe.ClientSafePipeHandle.DangerousGetHandle(),
                hStdError = errPipe.ClientSafePipeHandle.DangerousGetHandle(),
            };

            var commandLine = new StringBuilder(BuildCommandLine(startInfo));
            var environment = BuildEnvironmentBlock(startInfo);
            var workingDirectory = string.IsNullOrEmpty(startInfo.WorkingDirectory) ? null : startInfo.WorkingDirectory;

            IntPtr environmentPtr = IntPtr.Zero;
            try
            {
                environmentPtr = Marshal.StringToHGlobalUni(environment);

                var created = CreateProcessWithTokenW(
                    primaryToken,
                    0,
                    null,
                    commandLine,
                    CREATE_UNICODE_ENVIRONMENT | CREATE_NO_WINDOW,
                    environmentPtr,
                    workingDirectory,
                    ref si,
                    out var pi);

                if (!created)
                {
                    return null;
                }

                // Release our inheritable copies of the child ends so the pipes report EOF once the
                // child exits.
                outPipe.DisposeLocalCopyOfClientHandle();
                errPipe.DisposeLocalCopyOfClientHandle();
                inPipe.DisposeLocalCopyOfClientHandle();

                using var stdoutReader = new StreamReader(outPipe, Encoding.UTF8);
                using var stderrReader = new StreamReader(errPipe, Encoding.UTF8);
                var stdoutTask = stdoutReader.ReadToEndAsync();
                var stderrTask = stderrReader.ReadToEndAsync();

                if (!string.IsNullOrEmpty(standardInput))
                {
                    using var stdinWriter = new StreamWriter(inPipe, new UTF8Encoding(false)) { AutoFlush = true };
                    stdinWriter.Write(standardInput);
                }

                inPipe.Dispose();

                var timedOut = false;
                if (timeoutMs > 0)
                {
                    if (WaitForSingleObject(pi.hProcess, (uint)timeoutMs) != WAIT_OBJECT_0)
                    {
                        timedOut = true;
                        TerminateProcess(pi.hProcess, 124);
                    }
                }
                else
                {
                    WaitForSingleObject(pi.hProcess, INFINITE);
                }

                var stdout = stdoutTask.GetAwaiter().GetResult();
                var stderr = stderrTask.GetAwaiter().GetResult();

                int exitCode = 124;
                if (!timedOut)
                {
                    GetExitCodeProcess(pi.hProcess, out exitCode);
                }

                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);

                return new ProcessRunResult(exitCode, stdout, timedOut ? "Script timed out." : stderr);
            }
            finally
            {
                if (environmentPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(environmentPtr);
                }
            }
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            outPipe?.Dispose();
            errPipe?.Dispose();
            inPipe?.Dispose();
            if (primaryToken != IntPtr.Zero)
            {
                CloseHandle(primaryToken);
            }

            if (shellToken != IntPtr.Zero)
            {
                CloseHandle(shellToken);
            }
        }
    }

    private static IntPtr OpenShellToken()
    {
        var shellWindow = GetShellWindow();
        if (shellWindow == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        _ = GetWindowThreadProcessId(shellWindow, out var shellPid);
        if (shellPid == 0)
        {
            return IntPtr.Zero;
        }

        var shellProcess = OpenProcess(PROCESS_QUERY_INFORMATION, false, shellPid);
        if (shellProcess == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        try
        {
            return OpenProcessToken(shellProcess, TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID, out var token)
                ? token
                : IntPtr.Zero;
        }
        finally
        {
            CloseHandle(shellProcess);
        }
    }

    /// <summary>Composes a Windows command line from the start info, quoting each argument.</summary>
    private static string BuildCommandLine(ProcessStartInfo startInfo)
    {
        var builder = new StringBuilder();
        AppendArgument(builder, startInfo.FileName);
        foreach (var argument in startInfo.ArgumentList)
        {
            builder.Append(' ');
            AppendArgument(builder, argument);
        }

        return builder.ToString();
    }

    /// <summary>Applies the standard Windows CommandLineToArgvW quoting rules to a single argument.</summary>
    private static void AppendArgument(StringBuilder builder, string argument)
    {
        if (argument.Length > 0 && !argument.AsSpan().ContainsAny(" \t\n\v\""))
        {
            builder.Append(argument);
            return;
        }

        builder.Append('"');
        var backslashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                builder.Append('\\', (backslashes * 2) + 1);
                backslashes = 0;
                builder.Append('"');
                continue;
            }

            builder.Append('\\', backslashes);
            backslashes = 0;
            builder.Append(c);
        }

        builder.Append('\\', backslashes * 2);
        builder.Append('"');
    }

    /// <summary>Builds a double-null-terminated Unicode environment block from the start info.</summary>
    private static string BuildEnvironmentBlock(ProcessStartInfo startInfo)
    {
        var builder = new StringBuilder();
        foreach (var entry in startInfo.Environment)
        {
            builder.Append(entry.Key).Append('=').Append(entry.Value).Append('\0');
        }

        builder.Append('\0');
        return builder.ToString();
    }

    private const uint MAXIMUM_ALLOWED = 0x02000000;
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
    private const uint TOKEN_ADJUST_DEFAULT = 0x0080;
    private const uint TOKEN_ADJUST_SESSIONID = 0x0100;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const int STARTF_USESTDHANDLES = 0x00000100;
    private const int STARTF_USESHOWWINDOW = 0x00000001;
    private const short SW_HIDE = 0;
    private const uint INFINITE = 0xFFFFFFFF;
    private const uint WAIT_OBJECT_0 = 0x00000000;

    private enum SECURITY_IMPERSONATION_LEVEL
    {
        SecurityImpersonation = 2,
    }

    private const SECURITY_IMPERSONATION_LEVEL SecurityImpersonation = SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation;

    private enum TOKEN_TYPE
    {
        TokenPrimary = 1,
    }

    private const TOKEN_TYPE TokenPrimary = TOKEN_TYPE.TokenPrimary;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        uint desiredAccess,
        IntPtr tokenAttributes,
        SECURITY_IMPERSONATION_LEVEL impersonationLevel,
        TOKEN_TYPE tokenType,
        out IntPtr newToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessWithTokenW(
        IntPtr hToken,
        uint dwLogonFlags,
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out int lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}
