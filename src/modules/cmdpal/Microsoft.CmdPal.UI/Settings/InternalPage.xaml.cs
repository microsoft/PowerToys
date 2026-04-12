// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Page = Microsoft.UI.Xaml.Controls.Page;

namespace Microsoft.CmdPal.UI.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class InternalPage : Page
{
    internal InternalPageViewModel ViewModel { get; }

    public InternalPage()
    {
        ViewModel = ActivatorUtilities.CreateInstance<InternalPageViewModel>(App.Current.Services);
        InitializeComponent();
    }
}
