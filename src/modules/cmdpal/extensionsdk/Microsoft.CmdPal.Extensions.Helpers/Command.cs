// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class Command : BaseObservable, ICommand
{
    private string _name = string.Empty;
    private string _id = string.Empty;
    private IconDataType _icon = new(string.Empty);

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged(nameof(Name));
        }
    }

    public string Id { get => _id; protected set => _id = value; }

    public IconDataType Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            OnPropertyChanged(nameof(Icon));
        }
    }
}
