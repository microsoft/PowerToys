// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class LoadingPageViewModel : PageViewModel
{
    public LoadingPageViewModel(IPage? model, TaskScheduler scheduler)
        : base(model, scheduler, CommandPaletteHost.Instance)
    {
        ModelIsLoading = true;
        IsInitialized = false;
    }
}
