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
        => CreateViewModel(content, context, FallbackContext);

    internal static ContentViewModel? CreateViewModel(
        IContent content,
        WeakReference<IPageContext> context,
        FallbackQueryContext? fallbackContext = null)
    {
        ContentViewModel? viewModel = content switch
        {
            IFormContent form => new ContentFormViewModel(form, context, fallbackContext),
            IMarkdownContent markdown => new ContentMarkdownViewModel(markdown, context),
            ITreeContent tree => new ContentTreeViewModel(tree, context, fallbackContext),
            IPlainTextContent plainText => new ContentPlainTextViewModel(plainText, context),
            IImageContent image => new ContentImageViewModel(image, context),
            _ => null,
        };
        return viewModel;
    }
}
