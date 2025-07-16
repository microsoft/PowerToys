// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class CommandPaletteContentPageViewModel : ContentPageViewModel
{
    public CommandPaletteContentPageViewModel(IContentPage model, TaskScheduler scheduler, AppExtensionHost host)
        : base(model, scheduler, host)
    {
    }

    public override ContentViewModel? ViewModelFromContent(IContent content, WeakReference<IPageContext> context)
    {
        ContentViewModel? viewModel = content switch
        {
            IFormContent form => new ContentFormViewModel(form, context),
            IMarkdownContent markdown => new ContentMarkdownViewModel(markdown, context),
            ITreeContent tree => new ContentTreeViewModel(tree, context),
            _ => null,
        };
        return viewModel;
    }
}
