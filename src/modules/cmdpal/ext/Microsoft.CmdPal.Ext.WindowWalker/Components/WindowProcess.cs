// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CmdPal.Ext.WindowWalker.Commands;
using Microsoft.CmdPal.Ext.WindowWalker.Helpers;

namespace Microsoft.CmdPal.Ext.WindowWalker.Components;

/// <summary>
/// Represents the process data of an open window. This class is used in the process cache and for the process object of the open window
/// </summary>
internal sealed class WindowProcess
{
    /// <summary>
    /// Maximum size of a file name
    /// </summary>
    private const int MaximumFileNameLength = 1000;

    /// <summary>
    /// An indicator if the window belongs to an 'Universal Windows Platform (UWP)' process
    /// </summary>
    private bool _isUwpAppFrameHost;

    /// <summary>
    /// Gets the id of the process
    /// </summary>
    internal uint ProcessID
    {
        get; private set;
    }

    /// <summary>
    /// Gets a value indicating whether the process is responding or not
    /// </summary>
    internal bool IsResponding
    {
        get
        {
            try
            {
                // Process.Responding doesn't work on UWP apps
                return ProcessType.Kind == ProcessPackagingKind.UwpApp || Process.GetProcessById((int)ProcessID).Responding;
            }
            catch (InvalidOperationException)
            {
                // Thrown when process not exist.
                return true;
            }
            catch (NotSupportedException)
            {
                // Thrown when process is not running locally.
                return true;
            }
        }
    }

    /// <summary>
    /// Gets the id of the thread
    /// </summary>
    internal uint ThreadID
    {
        get; private set;
    }

    /// <summary>
    /// Gets the name of the process
    /// </summary>
    internal string? Name
    {
        get; private set;
    }

    /// <summary>
    /// Gets a value indicating whether the window belongs to an 'Universal Windows Platform (UWP)' process
    /// </summary>
    public bool IsUwpAppFrameHost => _isUwpAppFrameHost;

    /// <summary>
    /// Gets a value indicating whether this is the shell process or not
    /// The shell process (like explorer.exe) hosts parts of the user interface (like taskbar, start menu, ...)
    /// </summary>
    internal bool IsShellProcess
    {
        get
        {
            var hShellWindow = NativeMethods.GetShellWindow();
            return GetProcessIDFromWindowHandle(hShellWindow) == ProcessID;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the process exists on the machine
    /// </summary>
    internal bool DoesExist
    {
        get
        {
            try
            {
                var p = Process.GetProcessById((int)ProcessID);
                p.Dispose();
                return true;
            }
            catch (InvalidOperationException)
            {
                // Thrown when process not exist.
                return false;
            }
            catch (ArgumentException)
            {
                // Thrown when process not exist.
                return false;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether full access to the process is denied or not
    /// </summary>
    internal bool IsFullAccessDenied
    {
        get; private set;
    }

    /// <summary>
    /// Gets the type of the process (UWP app, packaged Win32 app, unpackaged Win32 app, ...).
    /// </summary>
    internal ProcessPackagingInfo ProcessType
    {
        get; private set;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowProcess"/> class.
    /// </summary>
    /// <param name="pid">New process id.</param>
    /// <param name="tid">New thread id.</param>
    /// <param name="name">New process name.</param>
    internal WindowProcess(uint pid, uint tid, string name)
    {
        ProcessType = ProcessPackagingInfo.Empty;
        UpdateProcessInfo(pid, tid, name);
    }

    /// <summary>
    /// Updates the process information of the <see cref="WindowProcess"/> instance.
    /// </summary>
    /// <param name="pid">New process id.</param>
    /// <param name="tid">New thread id.</param>
    /// <param name="name">New process name.</param>
    internal void UpdateProcessInfo(uint pid, uint tid, string name)
    {
        // TODO: Add verification as to whether the process id and thread id is valid
        ProcessID = pid;
        ThreadID = tid;
        Name = name;

        // Process can be elevated only if process id is not 0 (Dummy value on error)
        IsFullAccessDenied = (pid != 0) ? TestProcessAccessUsingAllAccessFlag(pid) : false;

        // Update process type
        ProcessType = ProcessPackagingInspector.Inspect((int)pid);
        _isUwpAppFrameHost = string.Equals(Name, "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the process ID for the window handle
    /// </summary>
    /// <param name="hwnd">The handle to the window</param>
    /// <returns>The process ID</returns>
    internal static uint GetProcessIDFromWindowHandle(IntPtr hwnd)
    {
        _ = NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        return processId;
    }

    /// <summary>
    /// Gets the thread ID for the window handle
    /// </summary>
    /// <param name="hwnd">The handle to the window</param>
    /// <returns>The thread ID</returns>
    internal static uint GetThreadIDFromWindowHandle(IntPtr hwnd)
    {
        var threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
        return threadId;
    }

    /// <summary>
    /// Gets the process name for the process ID
    /// </summary>
    /// <param name="pid">The id of the process/param>
    /// <returns>A string representing the process name or an empty string if the function fails</returns>
    internal static string GetProcessNameFromProcessID(uint pid)
    {
        var processHandle = NativeMethods.OpenProcess(ProcessAccessFlags.QueryLimitedInformation, true, (int)pid);
        StringBuilder processName = new StringBuilder(MaximumFileNameLength);

        if (NativeMethods.GetProcessImageFileName(processHandle, processName, MaximumFileNameLength) != 0)
        {
            _ = Win32Helpers.CloseHandleIfNotNull(processHandle);
            return processName.ToString().Split('\\').Reverse().ToArray()[0];
        }
        else
        {
            _ = Win32Helpers.CloseHandleIfNotNull(processHandle);
            return string.Empty;
        }
    }

    /// <summary>
    /// Kills the process by it's id. If permissions are required, they will be requested.
    /// </summary>
    /// <param name="killProcessTree">Kill process and sub processes.</param>
    internal void KillThisProcess(bool killProcessTree)
    {
        if (IsFullAccessDenied)
        {
            var killTree = killProcessTree ? " /t" : string.Empty;
            ExplorerInfoResultCommand.OpenInShell("taskkill.exe", $"/pid {(int)ProcessID} /f{killTree}", null, ExplorerInfoResultCommand.ShellRunAsType.Administrator, true);
        }
        else
        {
            Process.GetProcessById((int)ProcessID).Kill(killProcessTree);
        }
    }

    /// <summary>
    /// Gets a boolean value indicating whether the access to a process using the AllAccess flag is denied or not.
    /// </summary>
    /// <param name="pid">The process ID of the process</param>
    /// <returns>True if denied and false if not.</returns>
    private static bool TestProcessAccessUsingAllAccessFlag(uint pid)
    {
        var processHandle = NativeMethods.OpenProcess(ProcessAccessFlags.AllAccess, true, (int)pid);

        if (Win32Helpers.GetLastError() == 5)
        {
            // Error 5 = ERROR_ACCESS_DENIED
            _ = Win32Helpers.CloseHandleIfNotNull(processHandle);
            return true;
        }
        else
        {
            _ = Win32Helpers.CloseHandleIfNotNull(processHandle);
            return false;
        }
    }
}
