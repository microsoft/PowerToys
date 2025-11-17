// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class GeneralPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel? viewModel;

    public GeneralPage()
    {
        this.InitializeComponent();

        var settings = App.Current.Services.GetService<SettingsModel>()!;
        viewModel = new SettingsViewModel(settings, App.Current.Services, _mainTaskScheduler);
    }

    public string ApplicationVersion
    {
        get
        {
            var version = Package.Current.Id.Version;
            return $"Version {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }
}
