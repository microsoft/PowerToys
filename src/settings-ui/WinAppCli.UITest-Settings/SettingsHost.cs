// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Microsoft.Settings.UITests.WinAppCli
{
    /// <summary>
    /// Owns the lifecycle of a <c>PowerToys.Settings.exe</c> process for the duration of a single
    /// test class. Locates the executable from either:
    ///   1. an installed PowerToys (<c>%ProgramFiles%\PowerToys\WinUI3Apps\PowerToys.Settings.exe</c>), or
    ///   2. the local dev build alongside the test assembly
    ///      (<c>...\x64\Release\WinUI3Apps\PowerToys.Settings.exe</c>).
    /// </summary>
    /// <remarks>
    /// Settings is launched directly rather than through <c>PowerToys.exe</c> (the runner) so the
    /// smoke test stays focused on the shell window — the runner brings in tray/elevation/module
    /// startup paths that aren't what we're trying to cover here.
    /// </remarks>
    internal sealed class SettingsHost : IDisposable
    {
        private const string SettingsExeName = "PowerToys.Settings.exe";
        private const string SettingsSubDirectory = "WinUI3Apps";

        private Process? settingsProcess;

        public int Pid => settingsProcess?.Id ?? throw new InvalidOperationException("Settings has not been launched.");

        public void Launch()
        {
            var exePath = LocateSettingsExe()
                ?? throw new FileNotFoundException($"Could not find {SettingsExeName} in any installed or dev-build location.");

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
                UseShellExecute = false,
            };

            settingsProcess = Process.Start(psi)
                ?? throw new InvalidOperationException($"Process.Start returned null for {exePath}");

            WaitForMainWindow(settingsProcess);
        }

        public void Dispose()
        {
            if (settingsProcess is null)
            {
                return;
            }

            try
            {
                if (!settingsProcess.HasExited)
                {
                    settingsProcess.Kill(entireProcessTree: true);
                    settingsProcess.WaitForExit(5_000);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already gone — nothing to clean up.
            }
            finally
            {
                settingsProcess.Dispose();
                settingsProcess = null;
            }
        }

        private static string? LocateSettingsExe()
        {
            foreach (var root in CandidateInstallRoots())
            {
                var path = Path.Combine(root, SettingsSubDirectory, SettingsExeName);
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static System.Collections.Generic.IEnumerable<string> CandidateInstallRoots()
        {
            // 1) Installed PowerToys (machine/per-user) — preferred in pipeline runs.
            foreach (var path in new[]
            {
                @"C:\Program Files\PowerToys",
                @"C:\Program Files (x86)\PowerToys",
                Environment.ExpandEnvironmentVariables(@"%LocalAppData%\PowerToys"),
            })
            {
                if (Directory.Exists(path))
                {
                    yield return path;
                }
            }

            // 2) Dev build sitting next to (or one folder up from) the test assembly.
            //    For this csproj's <OutputPath>, the test dll lands at
            //    <repo>\<plat>\<cfg>\tests\WinAppCli.UITests-Settings\netX\... and
            //    PowerToys.Settings.exe lands at <repo>\<plat>\<cfg>\WinUI3Apps\...
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (assemblyDir is not null)
            {
                var probe = new DirectoryInfo(assemblyDir);
                for (int i = 0; i < 6 && probe is not null; i++, probe = probe.Parent)
                {
                    if (Directory.Exists(Path.Combine(probe.FullName, SettingsSubDirectory)))
                    {
                        yield return probe.FullName;
                    }
                }
            }
        }

        private static void WaitForMainWindow(Process process)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                process.Refresh();
                if (process.HasExited)
                {
                    throw new InvalidOperationException($"PowerToys.Settings.exe exited during startup with code {process.ExitCode}.");
                }

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    // Give XAML a moment to populate the visual tree.
                    Thread.Sleep(750);
                    return;
                }

                Thread.Sleep(100);
            }

            throw new TimeoutException("PowerToys.Settings.exe did not produce a main window within 30s.");
        }
    }
}
