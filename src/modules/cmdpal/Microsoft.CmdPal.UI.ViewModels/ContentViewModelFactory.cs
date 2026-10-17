// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// Builds the view-model for one piece of page content.
/// </summary>
internal static class ContentViewModelFactory
{
    /// <summary>
    /// Builds the view-model that matches the type of the content.
    /// </summary>
    /// <returns>Null when the content is of a type this version does not know.</returns>
    internal static ContentViewModel? Create(IContent content, WeakReference<IPageContext> context)
        => content switch
        {
            IFormContent form => new ContentFormViewModel(form, context),
            IMarkdownContent markdown => new ContentMarkdownViewModel(markdown, context),
            ITreeContent tree => new ContentTreeViewModel(tree, context),
            IPlainTextContent plainText => new ContentPlainTextViewModel(plainText, context),
            IImageContent image => new ContentImageViewModel(image, context),
            _ => null,
        };
}
