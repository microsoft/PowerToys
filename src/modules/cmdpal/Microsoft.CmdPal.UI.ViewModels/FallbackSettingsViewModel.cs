// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class FallbackSettingsViewModel : ObservableObject
{
    private readonly SettingsModel _settings;
    private readonly FallbackSettings _fallbackSettings;
    private readonly uint? _suggestedQueryDelayMilliseconds;
    private readonly uint? _suggestedMinQueryLength;

    public string DisplayName { get; private set; } = string.Empty;

    public IconInfoViewModel Icon { get; private set; } = new(null);

    public string Id { get; private set; } = string.Empty;

    public bool IsEnabled
    {
        get => _fallbackSettings.IsEnabled;
        set
        {
            if (value != _fallbackSettings.IsEnabled)
            {
                _fallbackSettings.IsEnabled = value;

                if (!_fallbackSettings.IsEnabled)
                {
                    _fallbackSettings.IncludeInGlobalResults = false;
                }

                Save();
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(CanShowResultsBeforeMainResults));
                OnPropertyChanged(nameof(CanEditQueryDelayMilliseconds));
                OnPropertyChanged(nameof(CanEditMinQueryLength));
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
                _fallbackSettings.IncludeInGlobalResults = value;

                if (!_fallbackSettings.IsEnabled)
                {
                    _fallbackSettings.IsEnabled = true;
                }

                Save();
                OnPropertyChanged(nameof(IncludeInGlobalResults));
            }
        }
    }

    public bool ShowResultsInDedicatedSection
    {
        get => _fallbackSettings.ShowResultsInDedicatedSection;
        set
        {
            if (value != _fallbackSettings.ShowResultsInDedicatedSection)
            {
                _fallbackSettings.ShowResultsInDedicatedSection = value;
                Save();
                OnPropertyChanged(nameof(ShowResultsInDedicatedSection));
                OnPropertyChanged(nameof(CanShowResultsBeforeMainResults));
            }
        }
    }

    public bool ShowResultsBeforeMainResults
    {
        get => _fallbackSettings.ShowResultsBeforeMainResults;
        set
        {
            if (value != _fallbackSettings.ShowResultsBeforeMainResults)
            {
                _fallbackSettings.ShowResultsBeforeMainResults = value;
                Save();
                OnPropertyChanged(nameof(ShowResultsBeforeMainResults));
            }
        }
    }

    public bool CanShowResultsBeforeMainResults => IsEnabled && ShowResultsInDedicatedSection;

    public bool HasQueryDelayOverride
    {
        get => _fallbackSettings.QueryDelayMilliseconds.HasValue;
        set
        {
            if (value == HasQueryDelayOverride)
            {
                return;
            }

            _fallbackSettings.QueryDelayMilliseconds = value ? _fallbackSettings.QueryDelayMilliseconds ?? _suggestedQueryDelayMilliseconds ?? 0 : null;
            Save();
            OnPropertyChanged(nameof(HasQueryDelayOverride));
            OnPropertyChanged(nameof(QueryDelayMillisecondsValue));
            OnPropertyChanged(nameof(CanEditQueryDelayMilliseconds));
        }
    }

    public double QueryDelayMillisecondsValue
    {
        get => _fallbackSettings.QueryDelayMilliseconds ?? _suggestedQueryDelayMilliseconds ?? 0;
        set
        {
            var normalizedValue = NormalizeUnsignedValue(value);
            if (_fallbackSettings.QueryDelayMilliseconds == normalizedValue)
            {
                return;
            }

            _fallbackSettings.QueryDelayMilliseconds = normalizedValue;
            Save();
            OnPropertyChanged(nameof(QueryDelayMillisecondsValue));
            OnPropertyChanged(nameof(HasQueryDelayOverride));
            OnPropertyChanged(nameof(CanEditQueryDelayMilliseconds));
        }
    }

    public bool CanEditQueryDelayMilliseconds => IsEnabled && HasQueryDelayOverride;

    public string QueryDelayDescription => FormatSuggestedValueDescription(
        "FallbackQueryDelayDescriptionWithSuggestion",
        "FallbackQueryDelayDescriptionWithoutSuggestion",
        _suggestedQueryDelayMilliseconds);

    public bool HasMinQueryLengthOverride
    {
        get => _fallbackSettings.MinQueryLength.HasValue;
        set
        {
            if (value == HasMinQueryLengthOverride)
            {
                return;
            }

            _fallbackSettings.MinQueryLength = value ? _fallbackSettings.MinQueryLength ?? _suggestedMinQueryLength ?? 0 : null;
            Save();
            OnPropertyChanged(nameof(HasMinQueryLengthOverride));
            OnPropertyChanged(nameof(MinQueryLengthValue));
            OnPropertyChanged(nameof(CanEditMinQueryLength));
        }
    }

    public double MinQueryLengthValue
    {
        get => _fallbackSettings.MinQueryLength ?? _suggestedMinQueryLength ?? 0;
        set
        {
            var normalizedValue = NormalizeUnsignedValue(value);
            if (_fallbackSettings.MinQueryLength == normalizedValue)
            {
                return;
            }

            _fallbackSettings.MinQueryLength = normalizedValue;
            Save();
            OnPropertyChanged(nameof(MinQueryLengthValue));
            OnPropertyChanged(nameof(HasMinQueryLengthOverride));
            OnPropertyChanged(nameof(CanEditMinQueryLength));
        }
    }

    public bool CanEditMinQueryLength => IsEnabled && HasMinQueryLengthOverride;

    public string MinQueryLengthDescription => FormatSuggestedValueDescription(
        "FallbackMinQueryLengthDescriptionWithSuggestion",
        "FallbackMinQueryLengthDescriptionWithoutSuggestion",
        _suggestedMinQueryLength);

    public FallbackSettingsViewModel(
    TopLevelViewModel fallback,
    FallbackSettings fallbackSettings,
    SettingsModel settingsModel,
    ProviderSettingsViewModel providerSettings)
    {
        _settings = settingsModel;
        _fallbackSettings = fallbackSettings;
        _suggestedQueryDelayMilliseconds = fallback.GetSuggestedFallbackQueryDelayMilliseconds();
        _suggestedMinQueryLength = fallback.GetSuggestedFallbackMinQueryLength();

        Id = fallback.Id;
        DisplayName = string.IsNullOrWhiteSpace(fallback.DisplayTitle)
            ? (string.IsNullOrWhiteSpace(fallback.Title) ? providerSettings.DisplayName : fallback.Title)
            : fallback.DisplayTitle;

        Icon = new(fallback.InitialIcon);
        Icon.InitializeProperties();
    }

    private void Save()
    {
        SettingsModel.SaveSettings(_settings);
        WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
    }

    private static string FormatSuggestedValueDescription(string withSuggestionKey, string withoutSuggestionKey, uint? suggestedValue)
    {
        var resourceManager = Properties.Resources.ResourceManager;
        var resourceKey = suggestedValue.HasValue ? withSuggestionKey : withoutSuggestionKey;
        var format = resourceManager.GetString(resourceKey, CultureInfo.CurrentUICulture) ?? string.Empty;
        return suggestedValue.HasValue ? string.Format(CultureInfo.CurrentCulture, format, suggestedValue.Value) : format;
    }

    private static uint NormalizeUnsignedValue(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return 0;
        }

        if (value >= uint.MaxValue)
        {
            return uint.MaxValue;
        }

        return (uint)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
