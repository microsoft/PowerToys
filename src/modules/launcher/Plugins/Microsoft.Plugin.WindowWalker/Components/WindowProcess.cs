// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wox.Infrastructure;
using Wox.Plugin.Common.Win32;

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Represents the process data of an open window. This class is used in the process cache and for the process object of the open window
    /// </summary>
    internal class WindowProcess
    {
        /// <summary>
        /// Maximum size of a file name
        /// </summary>
        private const int MaximumFileNameLength = 1000;

        /// <summary>
        /// An indicator if the window belongs to an 'Universal Windows Platform (UWP)' process
        /// </summary>
        private readonly bool _isUwpApp;

        /// <summary>
        /// Gets the id of the process
        /// </summary>
        internal uint ProcessID
        {
            get; private set;
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
        internal string Name
        {
            get; private set;
        }

        /// <summary>
        /// Gets a value indicating whether the window belongs to an 'Universal Windows Platform (UWP)' process
        /// </summary>
        internal bool IsUwpApp
        {
            get { return _isUwpApp; }
        }

        /// <summary>
        /// Gets a value indicating whether this is the shell process or not
        /// The shell process (like explorer.exe) hosts parts of the user interface (like taskbar, start menu, ...)
        /// </summary>
        internal bool IsShellProcess
        {
            get
            {
                IntPtr hShellWindow = NativeMethods.GetShellWindow();
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
        /// A static cache for process icons
        /// </summary>
        private static readonly Dictionary<uint, ImageSource> _processIdsToIconsCache = new();

        /// <summary>
        /// A static instance of the fallback icon used for a process if an icon could not be retrieved
        /// </summary>
        private static readonly ImageSource FallbackIcon = Imaging.CreateBitmapSourceFromHIcon(
            SystemIcons.Application.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());

        /// <summary>
        /// Gets the icon associated with the process
        /// </summary>
        internal ImageSource ProcessIcon
        {
            get
            {
                lock (_processIdsToIconsCache)
                {
                    if (!_processIdsToIconsCache.ContainsKey(ProcessID))
                    {
                        if (_processIdsToIconsCache.Count > 7000)
                        {
                            Debug.Print("Clearing Process Icon Cache because it's size is " + _processIdsToIconsCache.Count);
                            _processIdsToIconsCache.Clear();
                        }

                        try
                        {
                            var processFileName = GetProcessFilePathFromID(ProcessID);
                            var tmpIcon = Icon.ExtractAssociatedIcon(processFileName);
                            _processIdsToIconsCache.Add(ProcessID, Imaging.CreateBitmapSourceFromHIcon(
                                 tmpIcon.Handle,
                                 Int32Rect.Empty,
                                 BitmapSizeOptions.FromEmptyOptions()));
                        }
                        catch
                        {
                            _processIdsToIconsCache.Add(ProcessID, FallbackIcon);
                        }
                    }

                    return _processIdsToIconsCache[ProcessID];
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowProcess"/> class.
        /// </summary>
        /// <param name="pid">New process id.</param>
        /// <param name="tid">New thread id.</param>
        /// <param name="name">New process name.</param>
        internal WindowProcess(uint pid, uint tid, string name)
        {
            UpdateProcessInfo(pid, tid, name);
            _isUwpApp = Name.ToUpperInvariant().Equals("APPLICATIONFRAMEHOST.EXE", StringComparison.Ordinal);
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
        }

        /// <summary>
        /// Gets the process ID for the window handle
        /// </summary>
        /// <param name="hwnd">The handle to the window</param>
        /// <returns>The process ID</returns>
        internal static uint GetProcessIDFromWindowHandle(IntPtr hwnd)
        {
            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint processId);
            return processId;
        }

        /// <summary>
        /// Gets the thread ID for the window handle
        /// </summary>
        /// <param name="hwnd">The handle to the window</param>
        /// <returns>The thread ID</returns>
        internal static uint GetThreadIDFromWindowHandle(IntPtr hwnd)
        {
            uint threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out _);
            return threadId;
        }

        /// <summary>
        /// Gets the process name for the process ID
        /// </summary>
        /// <param name="pid">The id of the process/param>
        /// <returns>A string representing the process name or an empty string if the function fails</returns>
        internal static string GetProcessNameFromProcessID(uint pid)
        {
            IntPtr processHandle = NativeMethods.OpenProcess(ProcessAccessFlags.QueryLimitedInformation, true, (int)pid);
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
        /// Gets the process file name for the process ID
        /// </summary>
        /// <param name="pid">The id of the process</param>
        /// <returns>A string representing the file name or an empty string if the function fails</returns>
        internal static string GetProcessFilePathFromID(uint pid)
        {
            IntPtr processHandle = NativeMethods.OpenProcess(ProcessAccessFlags.QueryLimitedInformation, true, (int)pid);
            StringBuilder fileName = new StringBuilder(MaximumFileNameLength);
            uint capacity = MaximumFileNameLength;
            if (NativeMethods.QueryFullProcessImageName(processHandle, 0, fileName, ref capacity))
            {
                _ = Win32Helpers.CloseHandleIfNotNull(processHandle);
                return fileName.ToString(0, (int)capacity);
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
                string killTree = killProcessTree ? " /t" : string.Empty;
                Helper.OpenInShell("taskkill.exe", $"/pid {(int)ProcessID} /f{killTree}", null, Helper.ShellRunAsType.Administrator, true);
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
            IntPtr processHandle = NativeMethods.OpenProcess(ProcessAccessFlags.AllAccess, true, (int)pid);

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
}
