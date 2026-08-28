// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandPaletteContentPageViewModel : ContentPageViewModel
{
    public CommandPaletteContentPageViewModel(IContentPage model, TaskScheduler scheduler, AppExtensionHost host, ICommandProviderContext providerContext)
        : base(model, scheduler, host, providerContext)
    {
    }

    public override ContentViewModel? ViewModelFromContent(IContent content, WeakReference<IPageContext> context)
    {
        var viewModel = ContentViewModelFactory.Create(content, context);
        return viewModel is null ? null : ShareFallbackContext(viewModel);
    }
}
