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

    private readonly string? _win32DisplayName;
    private readonly string? _packagedDisplayName;
    private readonly string? _packageFullName;

    /// <summary>Initializes a new instance of the <see cref="UninstallApplicationCommand"/> class. Creates an uninstall command that opens Windows settings for the supplied desktop app.</summary>
    public UninstallApplicationCommand(string win32DisplayName)
    {
        ArgumentNullException.ThrowIfNull(win32DisplayName);

        Name = Resources.uninstall_application;
        Icon = Icons.UninstallApplicationIcon;
        _win32DisplayName = win32DisplayName;
    }

    /// <summary>Initializes a new instance of the <see cref="UninstallApplicationCommand"/> class. Creates an uninstall command for the supplied packaged app and its full package identity.</summary>
    public UninstallApplicationCommand(string displayName, string packageFullName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(packageFullName);

        Name = Resources.uninstall_application;
        Icon = Icons.UninstallApplicationIcon;
        _packagedDisplayName = displayName;
        _packageFullName = packageFullName;
    }

    private async Task<CommandResult> UninstallPackagedAppAsync(string displayName, string packageFullName)
    {
        if (string.IsNullOrWhiteSpace(packageFullName))
        {
            Logger.LogError($"Critical error while uninstalling: packageFullName cannot be null or empty.");
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), displayName),
                Result = CommandResult.KeepOpen(),
            });
        }

        try
        {
            // Which timeout to use for the uninstallation operation?
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                var packageManager = new PackageManager();
                var result = await packageManager.RemovePackageAsync(packageFullName).AsTask(cts.Token);

                if (result.ErrorText is not null && result.ErrorText.Length > 0)
                {
                    Logger.LogError($"Failed to uninstall {packageFullName}: {result.ErrorText}");
                    return CommandResult.ShowToast(new ToastArgs()
                    {
                        Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), displayName),
                        Result = CommandResult.KeepOpen(),
                    });
                }
            }

            // TODO: Update the Search results after uninstalling the app - unsure how to do this yet.
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_successful), displayName),
                Result = CommandResult.GoHome(),
            });
        }
        catch (OperationCanceledException)
        {
            Logger.LogError($"Timeout exceeded while uninstalling {packageFullName}");
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), displayName),
                Result = CommandResult.KeepOpen(),
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogError($"Permission denied to uninstall {packageFullName}. Elevated privileges may be required. Error: {ex.Message}");
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), displayName),
                Result = CommandResult.KeepOpen(),
            });
        }
        catch (Exception ex)
        {
            Logger.LogError($"An unexpected error occurred during uninstallation of {packageFullName}: {ex.Message}");
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_failed), displayName),
                Result = CommandResult.KeepOpen(),
            });
        }
    }

    public override CommandResult Invoke()
    {
        if (_packagedDisplayName is not null && _packageFullName is not null)
        {
            return UninstallPackagedAppAsync(_packagedDisplayName, _packageFullName).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        if (_win32DisplayName is not null)
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
