// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class DetailsPaneViewModel : ObservableObject
{
    [ObservableProperty]
    public partial DetailsViewModel? Details { get; set; }

    public DetailsPaneViewModel()
    {
    }
}
