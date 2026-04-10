// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ControlItem : BaseObservable, IControlItem
{
    public virtual string Name { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual ControlType Type { get; set => SetProperty(ref field, value); }

    public virtual double Value { get; set => SetProperty(ref field, value); }

    public virtual double Minimum { get; set => SetProperty(ref field, value); }

    public virtual double Maximum { get; set => SetProperty(ref field, value); } = 100.0;

    public virtual double StepValue { get; set => SetProperty(ref field, value); } = 1.0;
}
