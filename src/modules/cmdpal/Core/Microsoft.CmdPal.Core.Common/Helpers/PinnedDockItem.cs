// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Core.Common.Helpers;

public partial class PinnedDockItem : WrappedDockItem
{
    public override string Title => $"{base.Title} ({Properties.Resources.PinnedItemSuffix})";

    public PinnedDockItem(ICommand command)
        : base(command, command.Name)
    {
    }

    public PinnedDockItem(ICommandItem item, string id)
        : base(item, id, item.Title)
    {
    }
}
