// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Common.UI;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerToys.ModuleContracts;

namespace Awake.ModuleServices;

/// <summary>
/// Provides CLI-based Awake control for reuse across hosts.
/// </summary>
public sealed class AwakeService : ModuleServiceBase, IAwakeService
{
    public static AwakeService Instance { get; } = new();

    public override string Key => SettingsDeepLink.SettingsWindow.Awake.ToString();

    protected override SettingsDeepLink.SettingsWindow SettingsWindow => SettingsDeepLink.SettingsWindow.Awake;

    public override Task<OperationResult> LaunchAsync(CancellationToken cancellationToken = default)
    {
        // Default launch -> indefinite, honoring Awake's own settings for display behavior.
        return SetIndefiniteAsync(cancellationToken);
    }

    public Task<OperationResult> SetIndefiniteAsync(CancellationToken cancellationToken = default)
    {
        return UpdateSettingsAsync(
            settings =>
            {
                settings.Properties.Mode = AwakeMode.INDEFINITE;
            },
            cancellationToken);
    }

    public Task<OperationResult> SetTimedAsync(int minutes, CancellationToken cancellationToken = default)
    {
        if (minutes <= 0)
        {
            return Task.FromResult(OperationResult.Fail("Minutes must be greater than zero."));
        }

        return UpdateSettingsAsync(
            settings =>
            {
                var totalMinutes = Math.Min(minutes, int.MaxValue);
                settings.Properties.Mode = AwakeMode.TIMED;
                settings.Properties.IntervalHours = (uint)(totalMinutes / 60);
                settings.Properties.IntervalMinutes = (uint)(totalMinutes % 60);
            },
            cancellationToken);
    }

    public Task<OperationResult> SetOffAsync(CancellationToken cancellationToken = default)
    {
        return UpdateSettingsAsync(
            settings =>
            {
                settings.Properties.Mode = AwakeMode.PASSIVE;
            },
            cancellationToken);
    }

    private static Task<OperationResult> UpdateSettingsAsync(Action<AwakeSettings> mutateSettings, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var settingsUtils = SettingsUtils.Default;
            var settings = settingsUtils.GetSettingsOrDefault<AwakeSettings>(AwakeSettings.ModuleName);

            mutateSettings(settings);

            settingsUtils.SaveSettings(JsonSerializer.Serialize(settings, AwakeServiceJsonContext.Default.AwakeSettings), AwakeSettings.ModuleName);
            return Task.FromResult(OperationResult.Ok());
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(OperationResult.Fail("Awake update was cancelled."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail($"Failed to update Awake settings: {ex.Message}"));
        }
    }
}
