// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation.Metadata;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

[Deprecated("Use MarkdownContent & ContentPage instead", DeprecationType.Deprecate, 8)]
public partial class MarkdownPage : Page, IMarkdownPage
{
    private IDetails? _details;

    public IDetails? Details
    {
        get => _details;
        set
        {
            _details = value;
            OnPropertyChanged(nameof(Details));
        }
    }

    public IContextItem[] Commands { get; set; } = [];

    public virtual string[] Bodies() => [];

    IDetails? IMarkdownPage.Details() => Details;
}
