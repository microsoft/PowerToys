// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.UI.ViewModels;

public partial class ToastViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string ToastMessage { get; set; } = string.Empty;
}
