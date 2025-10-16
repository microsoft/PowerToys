// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using WyHash;

namespace Microsoft.CmdPal.Ext.Apps;

internal sealed partial class AppCommand : InvokableCommand
{
    private readonly AppItem _app;

    public AppCommand(AppItem app)
    {
        _app = app;
        Name = Resources.run_command_action!;
        Id = GenerateId();
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
        if (_app.IsPackaged)
        {
            await StartApp(_app.UserModelId);
        }
        else
        {
            await StartExe(_app.ExePath);
        }
    }

    public override CommandResult Invoke()
    {
        _ = Launch();
        return CommandResult.Dismiss();
    }

    private string GenerateId()
    {
        // Use WyHash64 to generate stable ID hashes.
        // manually seeding with 0, so that the hash is stable across launches
        var result = WyHash64.ComputeHash64(_app.Name + _app.Subtitle + _app.ExePath, seed: 0);
        return $"{_app.Name}_{result}";
    }
}
