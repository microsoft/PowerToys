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
    private readonly ISettingsService _settingsService;
    private readonly ProviderSettingsViewModel _providerSettingsViewModel;

    private FallbackSettings _fallbackSettings;

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
                var newSettings = _fallbackSettings with { IsEnabled = value };

                if (!newSettings.IsEnabled)
                {
                    newSettings = newSettings with { IncludeInGlobalResults = false };
                }

                _fallbackSettings = newSettings;
                _providerSettingsViewModel.UpdateFallbackSettings(Id, _fallbackSettings);
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
                var newSettings = _fallbackSettings with { IncludeInGlobalResults = value };

                if (!newSettings.IsEnabled)
                {
                    newSettings = newSettings with { IsEnabled = true };
                }

                _fallbackSettings = newSettings;
                _providerSettingsViewModel.UpdateFallbackSettings(Id, _fallbackSettings);
                Save();
                OnPropertyChanged(nameof(IncludeInGlobalResults));
            }
        }
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

        Id = fallback.Id;
        DisplayName = string.IsNullOrWhiteSpace(fallback.DisplayTitle)
            ? (string.IsNullOrWhiteSpace(fallback.Title) ? providerSettings.DisplayName : fallback.Title)
            : fallback.DisplayTitle;

        Icon = new(fallback.InitialIcon);
        Icon.InitializeProperties();
    }

    private void Save()
    {
        _settingsService.Save();
        WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
    }
}
