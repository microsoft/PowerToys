// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ShaderEffectsPage : Page
{
    internal SettingsViewModel ViewModel { get; }

    public ShaderEffectsPage()
    {
        this.InitializeComponent();

        var themeService = App.Current.Services.GetService<IThemeService>()!;
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();

        ViewModel = new SettingsViewModel(topLevelCommandManager, TaskScheduler.FromCurrentSynchronizationContext(), themeService, settingsService);
    }
}
