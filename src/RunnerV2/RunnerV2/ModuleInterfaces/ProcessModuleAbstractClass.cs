// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PowerToys.GPOWrapper;
using RunnerV2.Helpers;

namespace RunnerV2.ModuleInterfaces
{
    /// <summary>
    /// Base abstract class for modules that launch and manage external processes.
    /// </summary>
    internal abstract class ProcessModuleAbstractClass
    {
        /// <summary>
        /// Options for launching a process.
        /// </summary>
        [Flags]
        public enum ProcessLaunchOptions
        {
            /// <summary>
            /// Only a single instance of the process should be running.
            /// </summary>
            SingletonProcess = 1,

            /// <summary>
            /// Elevate the process if the current process is elevated.
            /// </summary>
            ElevateIfApplicable = 2,

            /// <summary>
            /// Provide the runner process ID as the first argument to the launched process.
            /// </summary>
            RunnerProcessIdAsFirstArgument = 4,

            /// <summary>
            /// Indicates that the application should not launch automatically when the specified module is enabled.
            /// </summary>
            SupressLaunchOnModuleEnabled = 8,

            /// <summary>
            /// Specifies that the process should be started using the operating system shell.
            /// </summary>
            UseShellExecute = 16,

            /// <summary>
            /// Indicates that the process should never exit automatically.
            /// </summary>
            /// <remarks>Use this value when using a helper process to launch an application like explorer.exe.</remarks>
            NeverExit = 32,
        }

        /// <summary>
        /// Gets the relative or absolute path to the process executable.
        /// </summary>
        public abstract string ProcessPath { get; }

        /// <summary>
        /// Gets the name of the process without the .exe extension.
        /// </summary>
        /// <remarks>
        /// Has no effect if the <see cref="LaunchOptions"/> has the <see cref="ProcessLaunchOptions.UseShellExecute"/> flag set.
        /// </remarks>
        public abstract string ProcessName { get; }

        /// <summary>
        /// Gets the arguments to pass to the process on launch.
        /// </summary>
        /// <remarks>
        /// If not overridden, no arguments are passed.
        /// If the <see cref="LaunchOptions"/> has the <see cref="ProcessLaunchOptions.RunnerProcessIdAsFirstArgument"/> flag is set, the runner process ID is prepended to these arguments.
        /// </remarks>
        public virtual string ProcessArguments { get; } = string.Empty;

        /// <summary>
        /// Gets the options used to configure how the process is launched.
        /// </summary>
        public abstract ProcessLaunchOptions LaunchOptions { get; }

        /// <summary>
        /// Ensures that atleast one process is launched. If the process is already running, does nothing.
        /// </summary>
        public void EnsureLaunched()
        {
            Process[] processes = Process.GetProcessesByName(ProcessName);
            if (processes.Length > 0)
            {
                return;
            }

            LaunchProcess();
        }

        /// <summary>
        /// Launches the process with the specified options.
        /// </summary>
        /// <param name="isModuleEnableProcess">Specifies if the <see cref="Runner"/> class is currently calling this function as part of a module startup</param>
        public void LaunchProcess(bool isModuleEnableProcess = false)
        {
            if (isModuleEnableProcess && LaunchOptions.HasFlag(ProcessLaunchOptions.SupressLaunchOnModuleEnabled))
            {
                return;
            }

            if (LaunchOptions.HasFlag(ProcessLaunchOptions.SingletonProcess))
            {
                Process[] processes = Process.GetProcessesByName(ProcessName);
                if (processes.Length > 0)
                {
                    return;
                }
            }

            string arguments = (LaunchOptions.HasFlag(ProcessLaunchOptions.RunnerProcessIdAsFirstArgument) ? Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(ProcessArguments) ? string.Empty : " ") : string.Empty) + ProcessArguments;

            Process.Start(new ProcessStartInfo()
            {
                UseShellExecute = LaunchOptions.HasFlag(ProcessLaunchOptions.UseShellExecute),
                FileName = ProcessPath,
                Arguments = arguments,
                Verb = LaunchOptions.HasFlag(ProcessLaunchOptions.ElevateIfApplicable) && ElevationHelper.IsProcessElevated() ? "runas" : "open",
            });
        }

        /// <summary>
        /// Schedules all processes with the specified <see cref="ProcessName"/>.
        /// </summary>
        /// <remarks>
        /// If the <see cref="LaunchOptions"/> has the <see cref="ProcessLaunchOptions.NeverExit"/> flag set, this function does nothing.
        /// </remarks>
        public void ProcessExit()
        {
            if (LaunchOptions.HasFlag(ProcessLaunchOptions.NeverExit))
            {
                return;
            }

            ProcessHelper.ScheudleProcessKill(ProcessName);
        }
    }
}
