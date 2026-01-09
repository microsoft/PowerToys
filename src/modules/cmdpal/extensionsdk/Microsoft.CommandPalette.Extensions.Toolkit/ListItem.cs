// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ListItem : CommandItem, IListItem
{
    public virtual ITag[] Tags { get; set => SetProperty(ref field, value); } = [];

    public virtual IDetails? Details { get; set => SetProperty(ref field, value); }

    public virtual string Section { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string TextToSuggest { get; set => SetProperty(ref field, value); } = string.Empty;

    public ListItem(ICommand command)
        : base(command)
    {
    }

    public ListItem(ICommandItem command)
        : base(command)
    {
    }

    public ListItem()
        : base()
    {
    }
}
