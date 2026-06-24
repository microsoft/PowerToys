// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class MarkdownContent : BaseObservable, IMarkdownContent
{
    public virtual string Body { get; set => SetProperty(ref field, value); } = string.Empty;

    public MarkdownContent()
    {
    }

    public MarkdownContent(string body)
    {
        Body = body;
    }
}
