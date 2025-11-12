// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Properties;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ProviderSettingsViewModel(
    CommandProviderWrapper _provider,
    ProviderSettings _providerSettings,
    IServiceProvider _serviceProvider) : ObservableObject
{
    private readonly SettingsModel _settings = _serviceProvider.GetService<SettingsModel>()!;
    private readonly Lock _initializeSettingsLock = new();
    private Task? _initializeSettingsTask;

    public string DisplayName => _provider.DisplayName;

    public string ExtensionName => _provider.Extension?.ExtensionDisplayName ?? "Built-in";

    public string ExtensionSubtext => IsEnabled ?
        HasFallbackCommands ?
            $"{ExtensionName}, {TopLevelCommands.Count} commands, {FallbackCommands.Count} fallback commands" :
            $"{ExtensionName}, {TopLevelCommands.Count} commands" :
        Resources.builtin_disabled_extension;

    [MemberNotNullWhen(true, nameof(Extension))]
    public bool IsFromExtension => _provider.Extension is not null;

    public IExtensionWrapper? Extension => _provider.Extension;

    public string ExtensionVersion => IsFromExtension ? $"{Extension.Version.Major}.{Extension.Version.Minor}.{Extension.Version.Build}.{Extension.Version.Revision}" : string.Empty;

    public IconInfoViewModel Icon => _provider.Icon;

    [ObservableProperty]
    public partial bool LoadingSettings { get; set; } = _provider.Settings?.HasSettings ?? false;

    public bool IsEnabled
    {
        get => _providerSettings.IsEnabled;
        set
        {
            if (value != _providerSettings.IsEnabled)
            {
                _providerSettings.IsEnabled = value;
                Save();
                WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(ExtensionSubtext));
            }

            if (value == true)
            {
                _provider.CommandsChanged -= Provider_CommandsChanged;
                _provider.CommandsChanged += Provider_CommandsChanged;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether returns true if we have a settings page
    /// that's initialized, or we are still working on initializing that
    /// settings page. If we don't have a settings object, or that settings
    /// object doesn't have a settings page, then we'll return false.
    /// </summary>
    public bool HasSettings
    {
        get
        {
            if (_provider.Settings is null)
            {
                return false;
            }

            if (_provider.Settings.Initialized)
            {
                return _provider.Settings.HasSettings;
            }

            // settings still need to be loaded.
            return LoadingSettings;
        }
    }

    /// <summary>
    /// Gets will return the settings page, if we have one, and have initialized it.
    /// If we haven't initialized it, this will kick off a thread to start
    /// initializing it.
    /// </summary>
    public ContentPageViewModel? SettingsPage
    {
        get
        {
            if (_provider.Settings is null)
            {
                return null;
            }

            if (_provider.Settings.Initialized)
            {
                LoadingSettings = false;
                return _provider.Settings.SettingsPage;
            }

            // Don't load the settings if we're already working on it
            lock (_initializeSettingsLock)
            {
                _initializeSettingsTask ??= Task.Run(InitializeSettingsPage);
            }

            return null;
        }
    }

    [field: AllowNull]
    public List<TopLevelViewModel> TopLevelCommands
    {
        get
        {
            if (field is null)
            {
                field = BuildTopLevelViewModels();
            }

            return field;
        }
    }

    private List<TopLevelViewModel> BuildTopLevelViewModels()
    {
        var thisProvider = _provider;
        var providersCommands = thisProvider.TopLevelItems;

        // Remember! This comes in on the UI thread!
        return [.. providersCommands];
    }

    [field: AllowNull]
    public List<TopLevelViewModel> FallbackCommands
    {
        get
        {
            if (field is null)
            {
                field = BuildFallbackViewModels();
            }

            return field;
        }
    }

    public bool HasFallbackCommands => _provider.FallbackItems?.Length > 0;

    private List<TopLevelViewModel> BuildFallbackViewModels()
    {
        var thisProvider = _provider;
        var providersCommands = thisProvider.FallbackItems;

        // Remember! This comes in on the UI thread!
        return [.. providersCommands];
    }

    private void Save() => SettingsModel.SaveSettings(_settings);

    private void InitializeSettingsPage()
    {
        if (_provider.Settings is null)
        {
            return;
        }

        _provider.Settings.SafeInitializeProperties();
        _provider.Settings.DoOnUiThread(() =>
        {
            // Changing these properties will try to update XAML, and that has
            // to be handled on the UI thread, so we need to raise them on the
            // UI thread
            LoadingSettings = false;
            OnPropertyChanged(nameof(HasSettings));
            OnPropertyChanged(nameof(LoadingSettings));
            OnPropertyChanged(nameof(SettingsPage));
        });
    }

    private void Provider_CommandsChanged(CommandProviderWrapper sender, CommandPalette.Extensions.IItemsChangedEventArgs args)
    {
        OnPropertyChanged(nameof(ExtensionSubtext));
        OnPropertyChanged(nameof(TopLevelCommands));
    }
}
