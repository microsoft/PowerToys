// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.Settings.UITests.WinAppCli
{
    /// <summary>
    /// Thin wrapper around the Windows App Development CLI (<c>winapp</c>) UI Automation
    /// subcommands. The CLI binary is expected to be on <c>PATH</c> — install with
    /// <c>winget install Microsoft.winappcli</c> or via the <c>setup-WinAppCli</c> pipeline task.
    /// </summary>
    /// <remarks>
    /// Every public method spawns a fresh <c>winapp.exe</c> process. That overhead (~hundreds of ms
    /// per call) is acceptable for a smoke suite that runs a few dozen invocations; if a heavier
    /// suite needs lower per-call latency the wrapper can switch to a long-running session model
    /// when the CLI grows one.
    /// </remarks>
    internal static class WinAppCli
    {
        private const string ExeName = "winapp";

        /// <summary>
        /// Invokes an element by AutomationId / semantic slug. Tries InvokePattern,
        /// TogglePattern, SelectionItemPattern, then ExpandCollapsePattern in order — which
        /// means the same call works for both leaf NavigationView items (Invoke) and parent
        /// group items that only expand (ExpandCollapse).
        /// </summary>
        public static InvocationResult Invoke(int appPid, string selector)
            => Run("ui", "invoke", selector, "-a", appPid.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// Polls for an element to be present in the visual tree, up to <paramref name="timeoutMs"/>.
        /// </summary>
        public static InvocationResult WaitFor(int appPid, string selector, int timeoutMs = 3000)
            => Run("ui", "wait-for", selector, "-a", appPid.ToString(System.Globalization.CultureInfo.InvariantCulture), "-t", timeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>
        /// Captures the target window as a PNG written to <paramref name="outputPath"/>.
        /// </summary>
        public static InvocationResult Screenshot(int appPid, string selector, string outputPath)
            => Run("ui", "screenshot", selector, "-a", appPid.ToString(System.Globalization.CultureInfo.InvariantCulture), "-o", outputPath);

        /// <summary>
        /// Returns true if <c>winapp</c> is discoverable on <c>PATH</c>. Used by
        /// <c>[AssemblyInitialize]</c> to fail the suite with a useful error message
        /// instead of letting every test produce its own opaque process-launch failure.
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
                var result = Run("--version");
                return result.ExitCode == 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static InvocationResult Run(params string[] args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ExeName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Failed to launch '{ExeName}'. Is it installed and on PATH? (winget install Microsoft.winappcli)");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new InvocationResult(process.ExitCode, stdout, stderr, args);
        }

        public sealed record InvocationResult(int ExitCode, string StdOut, string StdErr, string[] Args)
        {
            public bool Succeeded => ExitCode == 0;

            public string DescribeFailure()
            {
                var sb = new StringBuilder();
                sb.Append("winapp ");
                sb.AppendJoin(' ', Args);
                sb.Append($" -> exit {ExitCode}");
                if (!string.IsNullOrWhiteSpace(StdErr))
                {
                    sb.Append("; stderr: ").Append(StdErr.Trim());
                }
                else if (!string.IsNullOrWhiteSpace(StdOut))
                {
                    sb.Append("; stdout: ").Append(StdOut.Trim());
                }

                return sb.ToString();
            }
        }
    }
}
