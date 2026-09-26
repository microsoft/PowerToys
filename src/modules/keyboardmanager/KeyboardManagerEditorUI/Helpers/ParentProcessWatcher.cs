// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Closes the editor when whatever launched it goes away.
    /// </summary>
    /// <remarks>
    /// Both launchers pass their process id as the first argument - Settings in
    /// <c>KeyboardManagerViewModel.OpenNewEditor</c> and the module in <c>dllmain.cpp</c> - but the
    /// WinUI 3 editor never read it, so closing PowerToys left the editor window behind. That
    /// orphan matters: it keeps the engine-suspend event signaled, and because the event object
    /// outlives the engine process, a restarted engine opens it still set and stops remapping
    /// entirely. The classic editor ties itself to the parent through
    /// <c>ProcessWaiter::OnProcessTerminate</c>.
    /// </remarks>
    internal static class ParentProcessWatcher
    {
        public static void CloseWhenParentExits(Action closeEditor)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length < 2 ||
                !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parentProcessId) ||
                parentProcessId <= 0)
            {
                // Started standalone (debugging); nothing to attach to.
                Logger.LogInfo("No parent process id on the command line, the editor will not close with its launcher");
                return;
            }

            try
            {
                Process parent = Process.GetProcessById(parentProcessId);
                parent.EnableRaisingEvents = true;
                parent.Exited += (_, _) =>
                {
                    Logger.LogInfo($"Parent process {parentProcessId} exited, closing the editor");
                    closeEditor();
                };

                // It may have exited between GetProcessById and the subscription above.
                if (parent.HasExited)
                {
                    Logger.LogInfo($"Parent process {parentProcessId} had already exited, closing the editor");
                    closeEditor();
                    return;
                }

                Logger.LogInfo($"Watching parent process {parentProcessId}");
            }
            catch (ArgumentException)
            {
                // No process with that id - the launcher is already gone.
                Logger.LogInfo($"Parent process {parentProcessId} is not running, closing the editor");
                closeEditor();
            }
            catch (Exception ex)
            {
                // Losing the tie is better than failing to start.
                Logger.LogWarning($"Could not watch parent process {parentProcessId}: {ex.Message}");
            }
        }
    }
}
