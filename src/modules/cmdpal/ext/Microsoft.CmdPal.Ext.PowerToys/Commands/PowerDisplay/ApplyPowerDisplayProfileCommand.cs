// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;

namespace PowerToysExtension.Commands;

internal sealed partial class ApplyPowerDisplayProfileCommand : InvokableCommand
{
    private static readonly CompositeFormat ProfileTitleFormat = CompositeFormat.Parse(Resources.PowerDisplay_Profile_Title_Format);
    private static readonly CompositeFormat ProcessedFormat = CompositeFormat.Parse(Resources.PowerDisplay_Apply_Processed_Format);

    private readonly int _profileId;
    private readonly string _profileName;
    private readonly IPowerDisplayCliService _cliService;

    internal ApplyPowerDisplayProfileCommand(int profileId, string profileName, IPowerDisplayCliService cliService)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileId);

        _profileId = profileId;
        _profileName = profileName ?? string.Empty;
        _cliService = cliService ?? throw new ArgumentNullException(nameof(cliService));

        Id = PowerDisplayCommandIds.BuildApplyProfileCommandId(profileId);
        Name = string.Format(CultureInfo.CurrentCulture, ProfileTitleFormat, _profileName, _profileId);
        Icon = PowerToysResourcesHelper.IconFromSettingsIcon("PowerDisplay.png");
    }

    public override CommandResult Invoke()
    {
        try
        {
            var result = _cliService.ApplyProfileAsync(_profileId, CancellationToken.None).GetAwaiter().GetResult();
            var appliedProfile = result.Value;
            if (result.IsSuccess && appliedProfile is not null)
            {
                var profileName = string.IsNullOrWhiteSpace(appliedProfile.Profile)
                    ? _profileName
                    : appliedProfile.Profile;
                return ShowToast(
                    string.Format(CultureInfo.CurrentCulture, ProcessedFormat, profileName),
                    CommandResult.Dismiss());
            }

            return ShowToast(GetFailureMessage(result.FailureKind, result.ErrorMessage), CommandResult.KeepOpen());
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Applying Power Display profile {_profileId} failed: {ex}");
            return ShowToast(Resources.PowerDisplay_Apply_ProcessError, CommandResult.KeepOpen());
        }
    }

    private static string GetFailureMessage(PowerDisplayCliFailureKind failureKind, string errorMessage) => failureKind switch
    {
        PowerDisplayCliFailureKind.ArgumentError => Resources.PowerDisplay_Apply_ArgumentError,
        PowerDisplayCliFailureKind.Timeout => Resources.PowerDisplay_Apply_TimeoutError,
        PowerDisplayCliFailureKind.InternalError => Resources.PowerDisplay_Apply_InternalError,
        PowerDisplayCliFailureKind.ProviderUnavailable when !string.IsNullOrWhiteSpace(errorMessage) => errorMessage,
        PowerDisplayCliFailureKind.ProviderUnavailable => Resources.PowerDisplay_Apply_ProviderUnavailableError,
        PowerDisplayCliFailureKind.MissingExecutable => Resources.PowerDisplay_Apply_MissingCliError,
        PowerDisplayCliFailureKind.InvalidResponse => Resources.PowerDisplay_Apply_InvalidResponseError,
        PowerDisplayCliFailureKind.Cancelled => Resources.PowerDisplay_Apply_CancelledError,
        _ => Resources.PowerDisplay_Apply_ProcessError,
    };

    private static CommandResult ShowToast(string message, CommandResult result)
    {
        return CommandResult.ShowToast(new ToastArgs
        {
            Message = message,
            Icon = PowerToysResourcesHelper.IconFromSettingsIcon("PowerDisplay.png"),
            Result = result,
        });
    }
}
