// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Common.Services;
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
        set => _providerSettings.IsEnabled = value;
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
        var topLevelCommands = _tlcManager.TopLevelCommands;
        var thisProvider = _provider;
        var providersCommands = thisProvider.TopLevelItems;
        List<TopLevelViewModel> results = [];

        // Remember! This comes in on the UI thread!
        // TODO: GH #426
        // Probably just do a background InitializeProperties
        // Or better yet, merge TopLevelCommandWrapper and TopLevelViewModel
        foreach (var command in providersCommands)
        {
            var match = topLevelCommands.Where(tlc => tlc.Model.Unsafe == command).FirstOrDefault();
            if (match != null)
            {
                results.Add(new(match, _settings, _serviceProvider));
            }
        }

        return results;
    }
}
