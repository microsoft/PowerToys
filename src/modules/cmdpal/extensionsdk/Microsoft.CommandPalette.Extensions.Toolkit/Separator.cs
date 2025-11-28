// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Separator : CommandItem, IListItem, ISeparatorContextItem, ISeparatorFilterItem
{
    public Separator(string? section = "")
        : base()
    {
        this.Command = null;
        Title = section ?? string.Empty;
    }

    public IDetails? Details => null;

    public string Section => Title;

    public ITag[]? Tags => null;

    public string? TextToSuggest => null;
}
