// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ProgressState : BaseObservable, IProgressState
{
    public virtual bool IsIndeterminate { get; set => SetProperty(ref field, value); }

    public virtual uint ProgressPercent { get; set => SetProperty(ref field, value); }
}
