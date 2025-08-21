// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Management.Deployment;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class UninstallApplicationCommand : InvokableCommand
{
    private readonly UWPApplication? _uwpTarget;
    private readonly Win32Program? _win32Target;

    public UninstallApplicationCommand(UWPApplication target)
    {
        Name = Resources.uninstall_application;
        Icon = Icons.UninstallApplicationIcon;
        _uwpTarget = target ?? throw new ArgumentNullException(nameof(target));
    }

    public UninstallApplicationCommand(Win32Program target)
    {
        Name = Resources.uninstall_application;
        Icon = Icons.UninstallApplicationIcon;
        _win32Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public static async Task<CommandResult> UninstallUwpAppAsync(UWPApplication app)
    {
        if (string.IsNullOrWhiteSpace(app.Package.FullName))
        {
            Logger.LogError($"Critical error while uninstalling: packageFullName cannot be null or empty.");
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), app.DisplayName),
                Result = CommandResult.KeepOpen(),
            });
        }

        try
        {
            var packageManager = new PackageManager();

            var result = await packageManager.RemovePackageAsync(app.Package.FullName);

            if (result.ErrorText is not null && result.ErrorText.Length > 0)
            {
                Logger.LogError($"Failed to uninstall {app.Package.FullName}: {result.ErrorText}");
                return CommandResult.ShowToast(new ToastArgs()
                {
                    Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), app.DisplayName),
                    Result = CommandResult.KeepOpen(),
                });
            }

            // TODO: Update the Search results after uninstalling the app
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_successful), app.DisplayName),
                Result = CommandResult.KeepOpen(),
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogError($"Permission denied to uninstall {app.Package.FullName}. Elevated privileges may be required. Error: {ex.Message}");
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), app.DisplayName),
                Result = CommandResult.KeepOpen(),
            });
        }
        catch (Exception ex)
        {
            Logger.LogError($"An unexpected error occurred during uninstallation of {app.Package.FullName}: {ex.Message}");
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), app.DisplayName),
                Result = CommandResult.KeepOpen(),
            });
        }
    }

    public override CommandResult Invoke()
    {
        if (_uwpTarget is not null)
        {
            return UninstallUwpAppAsync(_uwpTarget).Result;
        }

        if (_win32Target is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:appsfeatures",
                UseShellExecute = true,
            });
            return CommandResult.Dismiss();
        }

        Logger.LogError("UninstallApplicationCommand invoked with no target.");
        return CommandResult.Dismiss();
    }
}
