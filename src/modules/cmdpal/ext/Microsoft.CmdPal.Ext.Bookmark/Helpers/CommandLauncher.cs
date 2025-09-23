// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

internal static class CommandLauncher
{
    /// <summary>
    ///     Launches the classified item.
    /// </summary>
    /// <param name="classification">Classification produced by CommandClassifier.</param>
    /// <param name="runAsAdmin">Optional: force elevation if possible.</param>
    public static bool Launch(Classification classification, bool runAsAdmin = false)
    {
        switch (classification.Launch)
        {
            case LaunchMethod.ExplorerOpen:
                // Folders and shell: URIs are best handled by explorer.exe
                return ShellHelpers.OpenInShell("explorer.exe", classification.Target);

            case LaunchMethod.UwpActivate:
                return ActivateUwp(classification.Target);

            case LaunchMethod.ShellExecute:
            default:
                return ShellHelpers.OpenInShell(classification.Target, classification.Arguments, classification.WorkingDirectory, runAsAdmin ? ShellHelpers.ShellRunAsType.Administrator : ShellHelpers.ShellRunAsType.None);
        }
    }

    private static bool ActivateUwp(string aumidOrAppsFolder)
    {
        try
        {
            // If caller already has "shell:AppsFolder\…" we can just ask explorer to open it.
            if (aumidOrAppsFolder.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
            {
                return ShellHelpers.OpenInShell("explorer.exe", aumidOrAppsFolder);
            }

            // Otherwise, activate via ApplicationActivationManager
            ApplicationActivationManager.ActivateApplication(aumidOrAppsFolder, null, 0, out _);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Can't activate UWP application " + aumidOrAppsFolder, ex);
            return false;
        }
    }

    private static class ApplicationActivationManager
    {
        public static void ActivateApplication(string aumid, string? args, int options, out uint pid)
        {
            var mgr = (IApplicationActivationManager)new _ApplicationActivationManager();
            var hr = mgr.ActivateApplication(aumid, args ?? string.Empty, options, out pid);
            if (hr < 0)
            {
                throw new Win32Exception(hr);
            }
        }

        [ComImport]
        [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "Private class")]
        private class _ApplicationActivationManager;

        [ComImport]
        [Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationActivationManager
        {
            int ActivateApplication(
                [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
                [MarshalAs(UnmanagedType.LPWStr)] string arguments,
                int options,
                out uint processId);
        }
    }
}
