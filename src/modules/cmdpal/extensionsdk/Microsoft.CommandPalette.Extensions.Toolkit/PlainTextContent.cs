// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class PlainTextContent : BaseObservable, IPlainTextContent
{
    public FontFamily FontFamily { get; set => SetProperty(ref field, value); }

    public bool WrapWords { get; set => SetProperty(ref field, value); }

    public string? Text { get; set => SetProperty(ref field, value); }

    public PlainTextContent()
    {
    }

    public PlainTextContent(string? text)
    {
        Text = text;
    }
}
