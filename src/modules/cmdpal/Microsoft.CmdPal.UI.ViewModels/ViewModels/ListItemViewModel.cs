// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ListItemViewModel : ObservableObject
{
    // Observable from MVVM Toolkit will auto create public properties that use INotifyPropertyChange
    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private string _subHeader = string.Empty;
}
