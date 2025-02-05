// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ProgressState : BaseObservable, IProgressState
{
    private bool _isIndeterminate;

    private uint _progressPercent;

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            _isIndeterminate = value;
            OnPropertyChanged(nameof(IsIndeterminate));
        }
    }

    public uint ProgressPercent
    {
        get => _progressPercent;
        set
        {
            _progressPercent = value;
            OnPropertyChanged(nameof(ProgressPercent));
        }
    }
}
