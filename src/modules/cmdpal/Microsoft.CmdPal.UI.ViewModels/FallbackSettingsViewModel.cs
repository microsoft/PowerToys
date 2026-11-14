// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class FallbackSettingsViewModel : ObservableObject
{
    private readonly ProviderSettingsViewModel _providerSettingsViewModel;

    private FallbackSettings _fallbackSettings;

    public string DisplayName { get; private set; } = string.Empty;

    public IconInfoViewModel Icon { get; private set; } = new(null);

    public string Id { get; private set; } = string.Empty;

    public string RankId { get; private set; } = string.Empty;

    public bool HasQuerySettings { get; private set; }

    //// Limits for the number boxes, so the view and the model clamp to the same values.

    public double QueryDelayUpperBound => FallbackSettings.MaximumQueryDelayMilliseconds;

    public double MinimumQueryLengthUpperBound => FallbackSettings.MaximumMinimumQueryLength;

    public bool IsEnabled
    {
        get => _fallbackSettings.IsEnabled;
        set
        {
            var newSettings = _fallbackSettings with { IsEnabled = value };
            var disableGlobalResults = !value && _fallbackSettings.IncludeInGlobalResults;
            if (disableGlobalResults)
            {
                newSettings = newSettings with { IncludeInGlobalResults = false };
            }

            Update(nameof(IsEnabled), newSettings);
            if (disableGlobalResults)
            {
                OnPropertyChanged(nameof(IncludeInGlobalResults));
            }
        }
    }

    public bool IncludeInGlobalResults
    {
        get => _fallbackSettings.IncludeInGlobalResults;
        set
        {
            var newSettings = _fallbackSettings with { IncludeInGlobalResults = value };
            var enableFallback = value && !_fallbackSettings.IsEnabled;
            if (enableFallback)
            {
                newSettings = newSettings with { IsEnabled = true };
            }

            Update(nameof(IncludeInGlobalResults), newSettings);
            if (enableFallback)
            {
                OnPropertyChanged(nameof(IsEnabled));
            }
        }
    }

    public double QueryDelayMilliseconds
    {
        get => _fallbackSettings.QueryDelayMilliseconds ?? 0;
        set => Update(
            nameof(QueryDelayMilliseconds),
            _fallbackSettings with { QueryDelayMilliseconds = ToUInt32(value, FallbackSettings.MaximumQueryDelayMilliseconds) });
    }

    public double MinimumQueryLength
    {
        get => _fallbackSettings.MinimumQueryLength ?? 0;
        set => Update(
            nameof(MinimumQueryLength),
            _fallbackSettings with { MinimumQueryLength = ToUInt32(value, FallbackSettings.MaximumMinimumQueryLength) });
    }

    public FallbackSettingsViewModel(
        TopLevelViewModel fallback,
        FallbackSettings fallbackSettings,
        ProviderSettingsViewModel providerSettings)
    {
        _providerSettingsViewModel = providerSettings;
        _fallbackSettings = fallbackSettings;

        Id = fallback.Id;
        RankId = fallback.FallbackKey;
        HasQuerySettings = fallback.HasFallbackQueryHandler;
        DisplayName = string.IsNullOrWhiteSpace(fallback.DisplayTitle)
            ? (string.IsNullOrWhiteSpace(fallback.Title) ? providerSettings.DisplayName : fallback.Title)
            : fallback.DisplayTitle;

        Icon = new(fallback.InitialIcon);
        Icon.InitializeProperties();
    }

    /// <summary>
    /// Saves one changed setting and tells the palette to reload its commands.
    /// </summary>
    private void Update(string propertyName, FallbackSettings newSettings)
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
