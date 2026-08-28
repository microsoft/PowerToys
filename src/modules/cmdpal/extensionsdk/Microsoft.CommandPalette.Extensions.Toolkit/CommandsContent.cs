// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CommandsContent : BaseObservable, ICommandsContent
{
    public virtual ICommand[] Commands { get; set => SetProperty(ref field, value); } = [];
}
