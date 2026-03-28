// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Tag : BaseObservable, ITag
{
    public virtual OptionalColor Foreground { get; set => SetProperty(ref field, value); }

    public virtual OptionalColor Background { get; set => SetProperty(ref field, value); }

    public virtual IIconInfo Icon { get; set => SetProperty(ref field, value); } = new IconInfo();

    public virtual string Text { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string ToolTip { get; set => SetProperty(ref field, value); } = string.Empty;

    public Tag()
    {
    }

    public Tag(string text)
    {
        Text = text;
    }
}
