// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class FormattedFallbackCommandItem : FallbackCommandItem, IFormattedFallbackCommandItem
{
    public FormattedFallbackCommandItem(
        ICommand command,
        string displayTitle,
        string id,
        string titleTemplate,
        string subtitleTemplate = "")
        : base(command, displayTitle, id)
    {
        TitleTemplate = titleTemplate;
        SubtitleTemplate = subtitleTemplate;
    }

    public virtual string TitleTemplate { get; }

    public virtual string SubtitleTemplate { get; }
}
