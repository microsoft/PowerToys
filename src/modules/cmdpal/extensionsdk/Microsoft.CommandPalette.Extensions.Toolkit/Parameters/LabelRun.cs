// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class LabelRun : BaseObservable, ILabelRun
{
    private string? _text = string.Empty;

    public virtual string? Text
    {
        get => _text;
        set
        {
            _text = value;
            OnPropertyChanged(nameof(Text));
        }
    }

    public LabelRun(string text)
    {
        _text = text;
    }

    public LabelRun()
    {
    }
}
