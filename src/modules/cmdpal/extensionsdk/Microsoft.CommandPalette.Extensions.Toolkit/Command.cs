// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Command : BaseObservable, ICommand
{
    public virtual string Name { get; set => SetProperty(ref field, value); } = string.Empty;

    public virtual string Id { get; set; } = string.Empty;

    public virtual IconInfo Icon { get; set => SetProperty(ref field, value); } = new();

    IIconInfo ICommand.Icon => Icon;
}
