// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class LoadingPageViewModel : PageViewModel
{
    [ObservableProperty]
    public partial string LoadingMessage { get; set; } = string.Empty;

    public LoadingPageViewModel(IPage? model, TaskScheduler scheduler, AppExtensionHost host)
        : base(model, scheduler, host)
    {
        ModelIsLoading = true;
        IsInitialized = false;
    }
}
