// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Command : BaseObservable, ICommand
{
    public virtual string Name
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Name));
        }
    }

= string.Empty;

    public virtual string Id { get; protected set; } = string.Empty;

    public virtual IconInfo Icon
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Icon));
        }
    }

= new();

    IIconInfo ICommand.Icon => Icon;
}
