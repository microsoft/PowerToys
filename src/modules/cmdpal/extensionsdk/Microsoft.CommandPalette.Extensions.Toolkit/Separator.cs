// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Separator : BaseObservable, IListItem, ISeparatorContextItem, ISeparatorFilterItem
{

    public IDetails? Details => null;

    public string? Section { get; private set; }

    public ITag[]? Tags => null;

    public string? TextToSuggest => null;

    public ICommand? Command => null;

    public IIconInfo? Icon => null;

    public IContextItem[]? MoreCommands => null;

    public string? Subtitle => null;

    public string? Title
    {
        get => Section;
        set
        {
            if (Section != value)
            {
                Section = value;
                OnPropertyChanged();
                OnPropertyChanged(Section);
            }
        }
    }

    public Separator(string? title = "")
    {
        Section = title ?? string.Empty;
    }
}
