// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

#nullable enable
public abstract partial class ParameterValueRun : BaseObservable, IParameterValueRun
{
    private string _placeholderText = string.Empty;

    public virtual string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            _placeholderText = value;
            OnPropertyChanged(nameof(PlaceholderText));
        }
    }

    private bool _needsValue = true;

    // _required | _needsValue | out
    // F         | F           | T
    // F         | T           | T
    // T         | F           | F
    // T         | T           | T
    public virtual bool NeedsValue
    {
        get => !_required || _needsValue;
        set
        {
            _needsValue = value;
            OnPropertyChanged(nameof(NeedsValue));
        }
    }

    // Toolkit helper
    private bool _required = true;

    public virtual bool Required
    {
        get => _required;
        set
        {
            _required = value;
            OnPropertyChanged(nameof(NeedsValue));
        }
    }

    public abstract void ClearValue();

    public abstract object? Value { get; set; }
}
#nullable disable
