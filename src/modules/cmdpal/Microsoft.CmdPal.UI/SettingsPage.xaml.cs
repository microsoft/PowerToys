// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel;

namespace Microsoft.CmdPal.UI;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class SettingsPage : Page
{
    public SettingsViewModel? ViewModel
    {
        get => (SettingsViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // Using a DependencyProperty as the backing store for ViewModel.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(SettingsViewModel), typeof(SettingsPage), new PropertyMetadata(null, null));

    public SettingsPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is SettingsViewModel vm)
        {
            ViewModel = vm;
            Debug.WriteLine("Opened settings");
        }
        else
        {
            throw new ArgumentException("You should pass in a SettingsViewModel");
        }

        base.OnNavigatedTo(e);
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
