// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

// Compact literal text without the document viewer's editing or scrolling surface.
public partial class TextContent : BaseObservable, ITextContent
{
    public virtual string Text { get; set => SetProperty(ref field, value); } = string.Empty;

    public TextContent()
    {
    }

    public TextContent(string text)
    {
        Text = text;
    }
}
