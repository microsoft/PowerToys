// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ProviderSettingsViewModel(
    CommandProviderWrapper _provider,
    ProviderSettings _providerSettings,
    IServiceProvider _serviceProvider) : ObservableObject
{
    private readonly TopLevelCommandManager _tlcManager = _serviceProvider.GetService<TopLevelCommandManager>()!;
    private readonly SettingsModel _settings = _serviceProvider.GetService<SettingsModel>()!;

    public string DisplayName => _provider.DisplayName;

    public string ExtensionName => _provider.Extension?.ExtensionDisplayName ?? "Built-in";

    public string ExtensionSubtext => $"{ExtensionName}, {TopLevelCommands.Count} commands";

    [MemberNotNullWhen(true, nameof(Extension))]
    public bool IsFromExtension => _provider.Extension != null;

    public IExtensionWrapper? Extension => _provider.Extension;

    public string ExtensionVersion => IsFromExtension ? $"{Extension.Version.Major}.{Extension.Version.Minor}.{Extension.Version.Build}.{Extension.Version.Revision}" : string.Empty;

    public IconInfoViewModel Icon => _provider.Icon;

    public bool IsEnabled
    {
        get => _providerSettings.IsEnabled;
        set
        {
            if (value != _providerSettings.IsEnabled)
            {
                _providerSettings.IsEnabled = value;
                WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
            }
        }
    }

    public bool HasSettings => _provider.Settings != null && _provider.Settings.SettingsPage != null;

    public ContentPageViewModel? SettingsPage => HasSettings ? _provider?.Settings?.SettingsPage : null;

    [field: AllowNull]
    public List<TopLevelViewModel> TopLevelCommands
    {
        get
        {
            if (field == null)
            {
                field = BuildTopLevelViewModels();
            }

            return field;
        }
    }

    private List<TopLevelViewModel> BuildTopLevelViewModels()
    {
        CommandProviderWrapper thisProvider = _provider;
        TopLevelViewModel[] providersCommands = thisProvider.TopLevelItems;

        // Remember! This comes in on the UI thread!
        return [.. providersCommands];
    }
}
