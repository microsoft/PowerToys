// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class FallbackSettingsViewModel(
    TopLevelViewModel fallback,
    FallbackSettings _fallbackSettings,
    ProviderSettingsViewModel _providerSettings,
    IServiceProvider _serviceProvider) : ObservableObject
{
    private readonly SettingsModel _settings = _serviceProvider.GetService<SettingsModel>()!;

    public string DisplayName => string.IsNullOrWhiteSpace(fallback.DisplayTitle)
        ? (string.IsNullOrWhiteSpace(fallback.Title) ? _providerSettings.DisplayName : fallback.Title)
        : fallback.DisplayTitle;

    public string ExtensionName => _providerSettings.ExtensionName;

    public IExtensionWrapper? Extension => _providerSettings.Extension;

    public string ExtensionVersion => _providerSettings.ExtensionVersion;

    public string ProviderId => fallback.CommandProviderId;

    public IconInfoViewModel Icon => _providerSettings.Icon;

    public bool HasSettings => _providerSettings.HasSettings;

    public ContentPageViewModel? SettingsPage => _providerSettings.SettingsPage;

    public bool IsEnabled
    {
        get => _fallbackSettings.IsEnabled;
        set
        {
            if (value != _fallbackSettings.IsEnabled)
            {
                _fallbackSettings.IsEnabled = value;
                Save();
                WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
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
                Save();
                WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
                OnPropertyChanged(nameof(IncludeInGlobalResults));
            }
        }
    }

    public int WeightBoost
    {
        get => _fallbackSettings.WeightBoost;
        set
        {
            if (value != _fallbackSettings.WeightBoost)
            {
                _fallbackSettings.WeightBoost = value;
            }
        }
    }

    private void Save() => SettingsModel.SaveSettings(_settings);
}
