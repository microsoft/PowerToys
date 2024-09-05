// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class MarkdownPage : Action, IMarkdownPage
{
    private bool _loading;
    private ITag[] _tags = [];
    private string _title = "";

    public string Title
    {
        get => !string.IsNullOrEmpty(_title) ? _title : Name;
        set
        {
            _title = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public bool Loading
    {
        get => _loading;
        set
        {
            _loading = value;
            OnPropertyChanged(nameof(Loading));
        }
    }
    public ITag[] Tags
    {
        get => _tags;
        set
        {
            _tags = value;
            OnPropertyChanged(nameof(Tags));
        }
    }

    public IContextItem[] Commands { get; set; } = [];

    public virtual string[] Bodies() => throw new NotImplementedException();

    public virtual IDetails Details() => throw new NotImplementedException();
    // public IDetails Details { get => _Details; set { _Details = value; OnPropertyChanged(nameof(Details)); } }
}
