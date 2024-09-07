// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class Page : Command, IPage
{
    private bool _loading;
    private string _title = string.Empty;

    public bool Loading
    {
        get => _loading;
        set
        {
            _loading = value;
            OnPropertyChanged(nameof(Loading));
        }
    }
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged(nameof(Name));
        }
    }
}
