// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using ManagedCommon;
using Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Commands;

internal sealed partial class LaunchProfileAsAdminCommand : InvokableCommand
{
    private readonly string _id;
    private readonly string _profile;
    private readonly bool _openNewTab;
    private readonly bool _openQuake;

    internal LaunchProfileAsAdminCommand(string id, string profile, bool openNewTab, bool openQuake)
    {
        this._id = id;
        this._profile = profile;
        this._openNewTab = openNewTab;
        this._openQuake = openQuake;

        this.Name = Resources.launch_profile_as_admin;
        this.Icon = new IconInfo("\xE7EF"); // Admin icon
    }

    private void LaunchElevated(string id, string profile)
    {
        try
        {
            var path = "shell:AppsFolder\\" + id;
            var arguments = TerminalHelper.GetArguments(profile, _openNewTab, _openQuake);

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
            };

            System.Diagnostics.Process.Start(startInfo);
        }
#pragma warning disable IDE0059, CS0168, SA1005
        catch (Exception ex)
        {
            // TODO GH #108 We need to figure out some logging
            //var name = "Plugin: " + Resources.plugin_name;
            //var message = Resources.run_terminal_failed;
            //Log.Exception("Failed to open Windows Terminal", ex, GetType());
            //_context.API.ShowMsg(name, message, string.Empty);
            Logger.LogError($"Failed to open Windows Terminal: {ex.Message}");
        }
    }
#pragma warning restore IDE0059, CS0168, SA1005

    private void Launch(string id, string profile)
    {
        ComWrappers cw = new StrategyBasedComWrappers();
        var appManagerPtr = IntPtr.Zero;

        var hr = NativeHelpers.CoCreateInstance(ref Unsafe.AsRef(in NativeHelpers.ApplicationActivationManagerCLSID), IntPtr.Zero, NativeHelpers.CLSCTXINPROCALL, ref Unsafe.AsRef(in NativeHelpers.ApplicationActivationManagerIID), out appManagerPtr);

        if (hr != 0)
        {
            throw new ArgumentException($"Failed to create IApplicationActivationManager instance. HR: 0x{hr:X}");
        }

        var appManager = (IApplicationActivationManager)cw.GetOrCreateObjectForComInstance(
            appManagerPtr, CreateObjectFlags.None);

        if (appManager == null)
        {
            throw new ArgumentException("Failed to get IApplicationActivationManager interface");
        }

        const ActivateOptions noFlags = ActivateOptions.None;
        var queryArguments = TerminalHelper.GetArguments(profile, _openNewTab, _openQuake);
        try
        {
            appManager.ActivateApplication(id, queryArguments, noFlags, out var unusedPid);
        }
#pragma warning disable IDE0059, CS0168
        catch (Exception ex)
        {
            // TODO GH #108 We need to figure out some logging
            // var name = "Plugin: " + Resources.plugin_name;
            // var message = Resources.run_terminal_failed;
            // Log.Exception("Failed to open Windows Terminal", ex, GetType());
            // _context.API.ShowMsg(name, message, string.Empty);
            Logger.LogError($"Failed to open Windows Terminal: {ex.Message}");
        }
        finally
        {
            if (appManagerPtr != IntPtr.Zero)
            {
                Marshal.Release(appManagerPtr);
            }
        }
    }
#pragma warning restore IDE0059, CS0168

    public override CommandResult Invoke()
    {
        try
        {
            LaunchElevated(_id, _profile);
        }
        catch
        {
            // TODO GH #108 We need to figure out some logging
            // No need to log here, as the exception is already logged in LaunchElevated
        }

        return CommandResult.Dismiss();
    }
}
