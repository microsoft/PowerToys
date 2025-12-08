// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;

namespace Microsoft.CommandPalette.UI.ViewModels;

public partial class PageViewModel : ObservableObject
{
    public string Title { get; set; } = string.Empty;
}
