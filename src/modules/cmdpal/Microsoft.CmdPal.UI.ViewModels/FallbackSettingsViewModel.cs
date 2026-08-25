// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class FallbackSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ProviderSettingsViewModel _providerSettingsViewModel;
    private readonly uint? _suggestedQueryDelayMilliseconds;
    private readonly uint? _suggestedMinimumQueryLength;

    private FallbackSettings _fallbackSettings;

    public string DisplayName { get; private set; } = string.Empty;

    public IconInfoViewModel Icon { get; private set; } = new(null);

    public string Id { get; private set; } = string.Empty;

    public string RankId { get; private set; } = string.Empty;

    public bool HasQuerySettings { get; private set; }

    public bool HasResultSettings { get; private set; }

    public bool IsEnabled
    {
        get => _fallbackSettings.IsEnabled;
        set
        {
            if (value != _fallbackSettings.IsEnabled)
            {
                var newSettings = _fallbackSettings with { IsEnabled = value };

                if (!newSettings.IsEnabled)
                {
                    newSettings = newSettings with { IncludeInGlobalResults = false };
                }

                _fallbackSettings = newSettings;
                _providerSettingsViewModel.UpdateFallbackSettings(Id, _fallbackSettings);

                OnPropertyChanged(nameof(IsEnabled));
                WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
            }
        }
    }

    public bool IncludeInGlobalResults
    {
        get => _fallbackSettings.IncludeInGlobalResults;
        set
        {
            if (value != _fallbackSettings.IncludeInGlobalResults)
            {
                var newSettings = _fallbackSettings with { IncludeInGlobalResults = value };

                if (!newSettings.IsEnabled)
                {
                    newSettings = newSettings with { IsEnabled = true };
                }

                _fallbackSettings = newSettings;
                _providerSettingsViewModel.UpdateFallbackSettings(Id, _fallbackSettings);

                OnPropertyChanged(nameof(IncludeInGlobalResults));
                WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
            }
        }
    }

    public double QueryDelayMilliseconds
    {
        get => _fallbackSettings.QueryDelayMilliseconds ?? _suggestedQueryDelayMilliseconds ?? 0;
        set => UpdateQuerySetting(
            nameof(QueryDelayMilliseconds),
            _fallbackSettings with { QueryDelayMilliseconds = ToUInt32(value, 2000) });
    }

    public double MinimumQueryLength
    {
        get => _fallbackSettings.MinimumQueryLength ?? _suggestedMinimumQueryLength ?? 0;
        set => UpdateQuerySetting(
            nameof(MinimumQueryLength),
            _fallbackSettings with { MinimumQueryLength = ToUInt32(value, 100) });
    }

    public double MaximumVisibleItemCount
    {
        get => _fallbackSettings.MaximumVisibleItemCount ?? FallbackResultQueryManager.InitialRequestedItemCount;
        set => UpdateQuerySetting(
            nameof(MaximumVisibleItemCount),
            _fallbackSettings with { MaximumVisibleItemCount = Math.Max(1, ToUInt32(value, 100)) });
    }

    public FallbackSettingsViewModel(
    TopLevelViewModel fallback,
    FallbackSettings fallbackSettings,
    ProviderSettingsViewModel providerSettings,
    ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _providerSettingsViewModel = providerSettings;
        _fallbackSettings = fallbackSettings;
        _suggestedQueryDelayMilliseconds = fallback.SuggestedQueryDelayMilliseconds;
        _suggestedMinimumQueryLength = fallback.SuggestedMinQueryLength;

        Id = fallback.Id;
        RankId = fallback.FallbackKey;
        HasQuerySettings = fallback.IsFallbackV2 && fallback.FallbackMode is FallbackCommandMode.Active or FallbackCommandMode.Results;
        HasResultSettings = fallback.IsFallbackV2 && fallback.FallbackMode == FallbackCommandMode.Results;
        DisplayName = string.IsNullOrWhiteSpace(fallback.DisplayTitle)
            ? (string.IsNullOrWhiteSpace(fallback.Title) ? providerSettings.DisplayName : fallback.Title)
            : fallback.DisplayTitle;

        Icon = new(fallback.InitialIcon);
        Icon.InitializeProperties();
    }

    private void UpdateQuerySetting(string propertyName, FallbackSettings newSettings)
    {
        if (newSettings == _fallbackSettings)
        {
            return;
        }

        _fallbackSettings = newSettings;
        _providerSettingsViewModel.UpdateFallbackSettings(Id, _fallbackSettings);
        OnPropertyChanged(propertyName);
        WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
    }

    private static uint ToUInt32(double value, uint maximum)
    {
        if (double.IsNaN(value) || value <= 0)
        {
            return 0;
        }

        return (uint)Math.Min(Math.Round(value), maximum);
    }
}
