// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    [RelayCommand]
    public async Task<bool> LoadAsync()
    {
        await Task.Delay(2000);

        return true;
    }
}
