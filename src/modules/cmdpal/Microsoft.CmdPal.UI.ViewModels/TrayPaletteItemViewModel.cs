// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class TrayPaletteItemViewModel : ObservableObject, IDisposable
{
    private readonly CommandResolution _resolution;
    private int _smallContentVersion;
    private bool _disposed;

    public TrayPaletteItemPageContext PageContext { get; }

    public PinnedCommandSettings Pin { get; }

    public CommandItemViewModel Item { get; }

    public bool IsPage => Item.Command.IsPage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSmallContent))]
    [NotifyPropertyChangedFor(nameof(IsButton))]
    public partial ContentPageViewModel? SmallContent { get; private set; }

    public bool HasSmallContent => SmallContent is not null;

    public bool IsButton => !HasSmallContent;

    /// <summary>
    /// Creates and initializes a tray palette item. This is the
    /// factory method for creating a tray palette item, to make it clear that
    /// construction also calls InitializeProperties on it. This should only
    /// ever be used on a background thread.
    /// </summary>
    internal static TrayPaletteItemViewModel CreateAndInitialize(PinnedCommandSettings pin, CommandResolution resolution, IContextMenuFactory contextMenuFactory)
    {
        return new TrayPaletteItemViewModel(pin, resolution, contextMenuFactory);
    }

    private TrayPaletteItemViewModel(PinnedCommandSettings pin, CommandResolution resolution, IContextMenuFactory contextMenuFactory)
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

    public async Task LoadSmallContentAsync(TaskScheduler scheduler, Size availableSpace)
    {
        if (_disposed || HasSmallContent)
        {
            return;
        }

        var version = ++_smallContentVersion;
        ContentPageViewModel? page = null;
        try
        {
            var content = await Task.Run(() => _resolution.Provider.GetSmallContent(Pin.CommandId, availableSpace));
            if (content is null || _disposed || version != _smallContentVersion)
            {
                return;
            }

            page = new CommandPaletteContentPageViewModel(
                new SmallContentPage(content),
                scheduler,
                _resolution.Provider.ExtensionHost,
                _resolution.Provider.GetProviderContext());
            await Task.Run(page.InitializeProperties);
            if (!_disposed && version == _smallContentVersion && page.Content.Count > 0)
            {
                SmallContent = page;
                page = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load small content for tray command '{Pin.ProviderId}/{Pin.CommandId}'; using its button.", ex);
        }
        finally
        {
            if (page is not null)
            {
                await page.CleanupAsync();
            }
        }
    }

    public void ClearSmallContent()
    {
        ++_smallContentVersion;
        var page = SmallContent;
        SmallContent = null;
        if (page is not null)
        {
            _ = page.CleanupAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClearSmallContent();
        Item.SafeCleanup();
        _resolution.Dispose();
    }

    private sealed partial class SmallContentPage(IContent content) : Microsoft.CommandPalette.Extensions.Toolkit.ContentPage
    {
        public override IContent[] GetContent() => [content];
    }
}
