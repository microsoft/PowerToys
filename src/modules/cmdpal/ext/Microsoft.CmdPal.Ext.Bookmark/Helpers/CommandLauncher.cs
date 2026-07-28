// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using ManagedCommon;
using ManagedCsWin32;
using Microsoft.CmdPal.Common.Helpers;

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
                // You can notice the difference with Recycle Bin for example:
                // - "explorer ::{645FF040-5081-101B-9F08-00AA002F954E}"
                // - "::{645FF040-5081-101B-9F08-00AA002F954E}"
                return ShellHelpers.OpenInShell("explorer.exe", ShellArgumentBuilder.BuildArguments(classification.Target));

            case LaunchMethod.ActivateAppId:
                return ActivateAppId(classification.Target, classification.Arguments);

            case LaunchMethod.ShellExecute:
            default:
                return ShellHelpers.OpenInShell(classification.Target, classification.Arguments, classification.WorkingDirectory, runAsAdmin ? ShellHelpers.ShellRunAsType.Administrator : ShellHelpers.ShellRunAsType.None);
        }
    }

    private static bool ActivateAppId(string aumidOrAppsFolder, string? arguments)
    {
        const string shellAppsFolder = "shell:AppsFolder\\";
        try
        {
            if (aumidOrAppsFolder.StartsWith(shellAppsFolder, StringComparison.OrdinalIgnoreCase))
            {
                aumidOrAppsFolder = aumidOrAppsFolder[shellAppsFolder.Length..];
            }

            ApplicationActivationManager.ActivateApplication(aumidOrAppsFolder, arguments, 0, out _);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Can't activate AUMID using app store '{aumidOrAppsFolder}'", ex);
        }

        try
        {
            ShellHelpers.OpenInShell(shellAppsFolder + aumidOrAppsFolder, arguments);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Can't activate AUMID using shell '{aumidOrAppsFolder}'", ex);
        }

        return false;
    }

    private static class ApplicationActivationManager
    {
        public static void ActivateApplication(string aumid, string? args, int options, out uint pid)
        {
            var mgr = ComHelper.CreateComInstance<IApplicationActivationManager>(
                ref Unsafe.AsRef(in CLSID.ApplicationActivationManager),
                CLSCTX.InProcServer);
            mgr.ActivateApplication(aumid, args ?? string.Empty, options, out pid);
        }
    }
}
