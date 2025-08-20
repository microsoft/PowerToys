// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Management.Deployment;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class UninstallUwpApplicationCommand : InvokableCommand
{
    private readonly UWPApplication _target;

    public UninstallUwpApplicationCommand(UWPApplication target)
    {
        // TODO: Use Resources for the Messages and Multilanguage Support
        Name = "Uninstall Application";
        Icon = Icons.UninstallApplicationIcon;
        _target = target;
    }

    // TODO: Bring the Error Messages as Response to the Command
    public static async Task<bool> UninstallUwpAppAsync(string packageFullName)
    {
        if (string.IsNullOrWhiteSpace(packageFullName))
        {
            // TODO: Use Resources for the Messages and Multilanguage Support
            Console.Error.WriteLine("Error: packageFullName cannot be null or empty.");
            return false;
        }

        try
        {
            var packageManager = new PackageManager();

            var result = await packageManager.RemovePackageAsync(packageFullName);

            if (result.ErrorText is not null && result.ErrorText.Length > 0)
            {
                // TODO: Use Resources for the Messages and Multilanguage Support
                Console.Error.WriteLine($"Failed to uninstall {packageFullName}: {result.ErrorText}");
            }

            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            // TODO: Use Resources for the Messages and Multilanguage Support
            Console.Error.WriteLine($"Permission denied to uninstall {packageFullName}. Elevated privileges may be required. Error: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            // TODO: Use Resources for the Messages and Multilanguage Support
            Console.Error.WriteLine($"An unexpected error occurred during uninstallation of {packageFullName}: {ex.Message}");
            return false;
        }
    }

    public override CommandResult Invoke()
    {
        try
        {
            var success = UninstallUwpAppAsync(_target.Package.FullName).Result;

            if (success)
            {
                // TODO: Update the Search results after uninstalling the app
                return CommandResult.ShowToast(new ToastArgs()
                {
                    // TODO: Use Resources for the Messages and Multilanguage Support
                    Message = $"'{_target.DisplayName}' wurde erfolgreich deinstalliert.",
                    Result = CommandResult.KeepOpen(),
                });
            }
            else
            {
                return CommandResult.ShowToast(new ToastArgs()
                {
                    // TODO: Use Resources for the Messages and Multilanguage Support
                    Message = $"Error while uninstalling '{_target.DisplayName}'",
                    Result = CommandResult.KeepOpen(),
                });
            }
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast(new ToastArgs()
            {
                // TODO: Use Resources for the Messages and Multilanguage Support
                Message = $"Critical error while uninstalling: {ex.Message}",
                Result = CommandResult.KeepOpen(),
            });
        }
    }
}
