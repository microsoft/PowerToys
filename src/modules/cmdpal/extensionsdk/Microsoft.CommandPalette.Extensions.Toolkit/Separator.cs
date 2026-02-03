// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Separator : IListItem, ISeparatorContextItem, ISeparatorFilterItem
{
    public Separator(string? title = "")
        : base()
    {
        Section = title ?? string.Empty;
        Command = null;
    }

    public IDetails? Details => null;

    public string? Section { get; private set; }

    public ITag[]? Tags => null;

    public string? TextToSuggest => null;

    public ICommand? Command { get; private set; }

    public IIconInfo? Icon => null;

    public IContextItem[]? MoreCommands => null;

    public string? Subtitle => null;

    public string? Title
    {
        get => Section;
        set => Section = value;
    }

    public event Windows.Foundation.TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add { }
        remove { }
    }
}
