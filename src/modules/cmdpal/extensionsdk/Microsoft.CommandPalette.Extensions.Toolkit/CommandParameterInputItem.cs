// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract partial class CommandParameterInputItem : BaseObservable, ICommandParameterInputItem
{
    public abstract ParameterType ParameterType { get; }

    public virtual string ParameterName { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual bool IsRequired { get; set => SetProperty(ref field, value); }

    public virtual string Value { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string Label { get; set => SetProperty(ref field, value); } = string.Empty;

    private IconInfo _icon = new();

    public virtual IconInfo Icon { get => _icon; set => SetProperty(ref _icon, value); }

    IIconInfo ICommandParameterInputItem.Icon => Icon;
}
