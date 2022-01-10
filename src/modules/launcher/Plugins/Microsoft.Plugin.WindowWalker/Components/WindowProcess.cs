// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Code forked from Betsegaw Tadele's https://github.com/betsegaw/windowwalker/
using System;
using System.Linq;
using System.Text;

namespace Microsoft.Plugin.WindowWalker.Components
{
    /// <summary>
    /// Represents the process data of an open window. This class is used in the process cache and for the process object of the open window
    /// </summary>
    public class WindowProcess
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
        /// The process Id of the window process
        /// </summary>
        private uint processID;

        /// <summary>
        /// The thread Id of the window within the process object
        /// </summary>
        private uint threadID;

        /// <summary>
        /// The process file name of the window process
        /// </summary>
        private string processName;

        /// <summary>
        /// An indicator if the process of the window is running elevated
        /// </summary>
        private bool isElevated;

        /// <summary>
        /// Gets the id of the process
        /// </summary>
        public uint ProcessID
        {
            get { return processID; }
        }

        /// <summary>
        /// Gets the id of the thread
        /// </summary>
        public uint ThreadID
        {
            get { return threadID; }
        }

        /// <summary>
        /// Gets the name of the process
        /// </summary>
        public string Name
        {
            get { return processName; }
        }

        /// <summary>
        /// Gets a value indicating whether the process runs elevated or not
        /// </summary>
        public bool IsRunningElevated
        {
            get { return isElevated; }
        }

        /// <summary>
        /// Gets a value indicating whether the window belongs to an 'Universal Windows Platform (UWP)' process
        /// </summary>
        public bool IsUwpApp
        {
            get { return _isUwpApp; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowProcess"/> class.
        /// </summary>
         /// <param name="pid">New process id.</param>
        /// <param name="tid">New thread id.</param>
        /// <param name="name">New process name.</param>
        public WindowProcess(uint pid, uint tid, string name)
        {
            // TODO: Add verification as to wether the process id and thread id is valid
            processID = pid;
            threadID = tid;
            processName = name;
            _isUwpApp = processName.ToUpperInvariant().Equals("APPLICATIONFRAMEHOST.EXE", StringComparison.Ordinal);

            // Process can be elevated only if process id is not 0. (Dummy value on error.) Please have in mind here that pid=0 is the idle process.
            isElevated = (pid != 0) && GetProcessElevationStateFromProcessID(pid);
        }

        /// <summary>
        /// Updates the process information of the <see cref="WindowProcess"/> instance.
        /// </summary>
        /// <param name="pid">New process id.</param>
        /// <param name="tid">New thread id.</param>
        /// <param name="name">New process name.</param>
        public void UpdateProcessInfo(uint pid, uint tid, string name)
        {
            processID = pid;
            threadID = tid;
            processName = name;

            // Process can be elevated only if process id is not 0 (Dummy value on error)
            isElevated = (pid != 0) && GetProcessElevationStateFromProcessID(pid);
        }

        /// <summary>
        /// Gets the process ID for the window handle
        /// </summary>
        /// <param name="hwnd">The handle to the window</param>
        /// <returns>The process ID</returns>
        public static uint GetProcessIDFromWindowHandle(IntPtr hwnd)
        {
            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out _, out uint processId);
            return processId;
        }

        /// <summary>
        /// Gets the thread ID for the window handle
        /// </summary>
        /// <param name="hwnd">The handle to the window</param>
        /// <returns>The thread ID</returns>
        public static uint GetThreadIDFromWindowHandle(IntPtr hwnd)
        {
            _ = NativeMethods.GetWindowThreadProcessId(hwnd, out uint threadId);
            return threadId;
        }

        /// <summary>
        /// Gets the process name for the process ID
        /// </summary>
        /// <param name="pid">The id of the process/param>
        /// <returns>A string representing the process name or an empty string if the function fails</returns>
        public static string GetProcessNameFromProcessID(uint pid)
        {
            IntPtr processHandle = NativeMethods.OpenProcess(NativeMethods.ProcessAccessFlags.QueryLimitedInformation, true, (int)pid);
            StringBuilder processName = new StringBuilder(MaximumFileNameLength);

            if (NativeMethods.GetProcessImageFileName(processHandle, processName, MaximumFileNameLength) != 0)
            {
                return processName.ToString().Split('\\').Reverse().ToArray()[0];
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets a boolean value indicating whether a process runs elevated. (Note: This not a nice way. But the hack works.)
        /// </summary>
        /// <param name="pid">The process ID of the process</param>
        /// <returns>True if elevated and false if not.</returns>
        private static bool GetProcessElevationStateFromProcessID(uint pid)
        {
            IntPtr processHandle = NativeMethods.OpenProcess(NativeMethods.ProcessAccessFlags.AllAccess, true, (int)pid);
            StringBuilder processName = new StringBuilder(1);

            return NativeMethods.GetProcessImageFileName(processHandle, processName, 1) == 0;
        }
    }
}
