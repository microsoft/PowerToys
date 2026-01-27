// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class FallbackSettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly FallbackSettings _fallbackSettings;

    private SettingsModel Settings => _settingsService.CurrentSettings;

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

    public FallbackSettingsViewModel(
    TopLevelViewModel fallback,
    FallbackSettings fallbackSettings,
    SettingsService settingsService,
    ProviderSettingsViewModel providerSettings)
    {
        _settingsService = settingsService;
        _fallbackSettings = fallbackSettings;

        Id = fallback.Id;
        DisplayName = string.IsNullOrWhiteSpace(fallback.DisplayTitle)
            ? (string.IsNullOrWhiteSpace(fallback.Title) ? providerSettings.DisplayName : fallback.Title)
            : fallback.DisplayTitle;

        Icon = new(fallback.InitialIcon);
        Icon.InitializeProperties();
    }

    private void Save()
    {
        _settingsService.SaveSettings(Settings);
        WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
    }
}
