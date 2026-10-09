// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using WyHash;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class AppCommand : InvokableCommand
{
    private readonly AppItem _app;
    private readonly string? _shortcutPath;

    /// <summary>Creates an application command, optionally launching a matched shortcut under the same command ID.</summary>
    /// <param name="app">The canonical application that owns the command identity.</param>
    /// <param name="shortcutPath">A retained shortcut to launch instead of the preferred representation.</param>
    public AppCommand(AppItem app, string? shortcutPath = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        _app = app;
        _shortcutPath = shortcutPath;
        Name = Resources.run_command_action!;
        Id = string.IsNullOrEmpty(app.CatalogId)
            ? GenerateId(app.Name, app.Subtitle, app.LaunchTarget)
            : AppIdentity.ForCommand(app.CatalogId);
        Icon = Icons.GenericAppIcon;
    }

    private static async Task StartApp(string aumid)
    {
        await Task.Run(() =>
        {
            unsafe
            {
                IApplicationActivationManager* appManager = null;
                try
                {
                    PInvoke.CoCreateInstance(typeof(ApplicationActivationManager).GUID, null, CLSCTX.CLSCTX_INPROC_SERVER, out appManager).ThrowOnFailure();
                    using var handle = new SafeComHandle((IntPtr)appManager);
                    appManager->ActivateApplication(
                        aumid,
                        string.Empty,
                        ACTIVATEOPTIONS.AO_NONE,
                        out var unusedPid);
                }
                catch (System.Exception ex)
                {
                    Logger.LogError(ex.Message);
                }
            }
        }).ConfigureAwait(false);
    }

    private static async Task StartExe(string path)
    {
        await Task.Run(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        });
    }

    private async Task Launch()
    {
        if (_shortcutPath is not null)
        {
            await StartExe(_shortcutPath);
        }
        else if (_app.IsPackaged)
        {
            await StartApp(_app.AppUserModelId);
        }
        else
        {
            await StartExe(_app.LaunchTarget);
        }
    }

    public override CommandResult Invoke()
    {
        _ = Launch();
        return CommandResult.Dismiss();
    }

    /// <summary>Creates the legacy ID used by existing saved app commands.</summary>
    internal static string GenerateId(string name, string subtitle, string exePath)
    {
        // Use WyHash64 to generate stable ID hashes.
        // manually seeding with 0, so that the hash is stable across launches
        var result = WyHash64.ComputeHash64(name + subtitle + exePath, seed: 0);
        return $"{name}_{result}";
    }
}
