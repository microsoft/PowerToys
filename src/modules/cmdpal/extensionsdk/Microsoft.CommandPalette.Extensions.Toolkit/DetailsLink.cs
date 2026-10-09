// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class DetailsLink : IDetailsLink
{
    public virtual Uri? Link { get; set; }

    public virtual string Text { get; set; } = string.Empty;

    public DetailsLink()
    {
    }

    public DetailsLink(string url)
        : this(url, url)
    {
    }

    public DetailsLink(string url, string text)
    {
        if (Uri.TryCreate(url, default(UriCreationOptions), out var newUri))
        {
            Link = newUri;
        }

        Text = text;
    }
}
