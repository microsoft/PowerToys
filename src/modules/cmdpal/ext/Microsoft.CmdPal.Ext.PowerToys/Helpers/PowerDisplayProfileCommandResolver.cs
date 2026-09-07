// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerDisplay.Contracts;
using PowerToysExtension.Commands;
using PowerToysExtension.Pages;
using PowerToysExtension.Properties;

namespace PowerToysExtension.Helpers;

/// <summary>
/// Shares one CLI query while the host restores a provider's pinned commands and dock items.
/// A new resolver is created for each provider load; pages keep their own loading lifecycle.
/// </summary>
internal sealed class PowerDisplayProfileCommandResolver
{
    private readonly IPowerDisplayCliService _cliService;
    private readonly CancellationToken _cancellationToken;
    private readonly Lazy<Task<PowerDisplayCliResult<CliProfileListResult>>> _profiles;

    internal PowerDisplayProfileCommandResolver(IPowerDisplayCliService cliService, CancellationToken cancellationToken = default)
    {
        _cliService = cliService ?? throw new ArgumentNullException(nameof(cliService));
        _cancellationToken = cancellationToken;
        _profiles = new(() => Task.Run(
            () => _cliService.GetProfilesAsync(_cancellationToken),
            _cancellationToken));
    }

    internal ListItem GetCommandItem(int profileId)
    {
        var command = new ApplyPowerDisplayProfileCommand(profileId, Resources.PowerDisplay_Profile_FallbackName, _cliService);
        var item = new ListItem(command)
        {
            Title = command.Name,
            Subtitle = Resources.PowerDisplay_Profile_Loading,
            Icon = PowerToysResourcesHelper.IconFromSettingsIcon("PowerDisplay.png"),
        };

        _ = LoadDetailsAsync(item, profileId);
        return item;
    }

    private async Task LoadDetailsAsync(ListItem item, int profileId)
    {
        try
        {
            var result = await _profiles.Value.ConfigureAwait(false);
            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (!result.IsSuccess || result.Value is null)
            {
                item.Subtitle = Resources.PowerDisplay_Profile_DetailsUnavailable;
                return;
            }

            var profile = result.Value.Profiles.FirstOrDefault(profile => profile.Id == profileId);
            if (profile is null)
            {
                item.Subtitle = Resources.PowerDisplay_Apply_ArgumentError;
                return;
            }

            var resolved = PowerDisplayProfilesPage.CreateProfileListItem(profile, _cliService);
            item.Command = resolved.Command;
            item.Title = resolved.Title;
            item.Subtitle = resolved.Subtitle;
        }
        catch (OperationCanceledException) when (_cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Loading restored Power Display profile details failed: {ex}");
            if (!_cancellationToken.IsCancellationRequested)
            {
                item.Subtitle = Resources.PowerDisplay_Profile_DetailsUnavailable;
            }
        }
    }
}
