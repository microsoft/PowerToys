// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Management.Deployment;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class UninstallApplicationCommand : InvokableCommand
{
    // This is a ms-settings URI that opens the Apps & Features page in Windows Settings.
    // It's correct and follows the Microsoft documentation:
    // https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings-app#apps
    private const string AppsFeaturesUri = "ms-settings:appsfeatures";

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

    private async Task<CommandResult> UninstallUwpAppAsync(UWPApplication app)
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
            // Which timeout to use for the uninstallation operation?
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            {
                var packageManager = new PackageManager();
                var result = await packageManager.RemovePackageAsync(app.Package.FullName).AsTask(cts.Token);

                if (result.ErrorText is not null && result.ErrorText.Length > 0)
                {
                    Logger.LogError($"Failed to uninstall {app.Package.FullName}: {result.ErrorText}");
                    return CommandResult.ShowToast(new ToastArgs()
                    {
                        Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), app.DisplayName),
                        Result = CommandResult.KeepOpen(),
                    });
                }
            }

            // TODO: Update the Search results after uninstalling the app - unsure how to do this yet.
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_successful), app.DisplayName),
                Result = CommandResult.KeepOpen(),
            });
        }
        catch (OperationCanceledException)
        {
            Logger.LogError($"Timeout exceeded while uninstalling {app.Package.FullName}");
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), app.DisplayName),
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
            return UninstallUwpAppAsync(_uwpTarget).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        if (_win32Target is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppsFeaturesUri,
                UseShellExecute = true,
            });
            return CommandResult.Dismiss();
        }

        Logger.LogError("UninstallApplicationCommand invoked with no target.");
        return CommandResult.Dismiss();
    }
}
