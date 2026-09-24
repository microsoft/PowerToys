// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class TrayPaletteItemViewModel(
    PinnedCommandSettings pin,
    CommandItemViewModel item,
    CommandProviderWrapper provider,
    TrayPaletteItemPageContext pageContext)
{
    public TrayPaletteItemPageContext PageContext { get; } = pageContext;

    public PinnedCommandSettings Pin { get; } = pin;

    public CommandItemViewModel Item { get; } = item;

    public bool IsPage { get; } = item.Command.Model.Unsafe is IPage;

    public PerformCommandMessage CreateMessage() => new(Item.Command.Model, Item.Model)
    {
        SourceExtensionHost = provider.ExtensionHost,
        SourceProviderContext = provider.GetProviderContext(),
    };
}
