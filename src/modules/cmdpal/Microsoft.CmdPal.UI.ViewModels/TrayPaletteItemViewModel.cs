// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class TrayPaletteItemViewModel : IDisposable
{
    private readonly CommandResolution _resolution;

    public TrayPaletteItemPageContext PageContext { get; }

    public PinnedCommandSettings Pin { get; }

    public CommandItemViewModel Item { get; }

    public bool IsPage => Item.Command.IsPage;

    internal TrayPaletteItemViewModel(PinnedCommandSettings pin, CommandResolution resolution, IContextMenuFactory contextMenuFactory)
    {
        _resolution = resolution;
        Pin = pin;
        PageContext = new(pin, resolution.Provider);

        // Own a separate presentation and context menu, not the shared Home/Dock item.
        Item = new(resolution.Command.ItemViewModel.Model, new(PageContext), contextMenuFactory);
        try
        {
            Item.InitializeProperties();
        }
        catch
        {
            Item.SafeCleanup();
            throw;
        }
    }

    public PerformCommandMessage CreateMessage() => new(Item.Command, Item.Model)
    {
        Context = new SourceContext(_resolution.Provider.ExtensionHost, _resolution.Provider.GetProviderContext()),
    };

    public void Dispose()
    {
        Item.SafeCleanup();
        _resolution.Dispose();
    }
}
