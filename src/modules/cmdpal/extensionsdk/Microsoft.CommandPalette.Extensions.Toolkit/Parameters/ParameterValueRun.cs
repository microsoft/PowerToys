// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

#nullable enable
public abstract partial class ParameterValueRun : BaseObservable, IParameterValueRun
{
    public virtual string PlaceholderText { get; set => SetProperty(ref field, value); } = string.Empty;

    // NeedsValue is computed from the parameter's own state. By default, a
    // parameter needs a value as long as it is marked Required; derived types
    // override this getter to incorporate their own value state (e.g.
    // StringParameterRun checks whether Text is empty). If a derived type
    // needs to publish NeedsValue changes imperatively, it can re-introduce a
    // setter or call OnPropertyChanged(nameof(NeedsValue)).
    public virtual bool NeedsValue => _required;

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
