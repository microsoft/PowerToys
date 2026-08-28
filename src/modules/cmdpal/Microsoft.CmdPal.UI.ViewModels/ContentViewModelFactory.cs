// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public static class ContentViewModelFactory
{
    public static ContentViewModel? Create(IContent content, WeakReference<IPageContext> context) => content switch
    {
        IFormContent form => new ContentFormViewModel(form, context),
        IMarkdownContent markdown => new ContentMarkdownViewModel(markdown, context),
        ITreeContent tree => new ContentTreeViewModel(tree, context),
        IPlainTextContent text => new ContentPlainTextViewModel(text, context),
        IImageContent image => new ContentImageViewModel(image, context),
        ITextContent text => new ContentTextViewModel(text, context),
        IHeaderContent header => new ContentHeaderViewModel(header, context),
        IPropertyContent property => new ContentPropertyViewModel(property, context),
        IPropertyGridContent grid => new ContentPropertyGridViewModel(grid, context),
        ISectionContent section => new ContentSectionViewModel(section, context),
        ILinkContent link => new ContentLinkViewModel(link, context),
        ITagsContent tags => new ContentTagsViewModel(tags, context),
        ICommandsContent commands => new ContentCommandsViewModel(commands, context),
        ISeparatorContent separator => new ContentSeparatorViewModel(separator, context),
        _ => null,
    };
}
