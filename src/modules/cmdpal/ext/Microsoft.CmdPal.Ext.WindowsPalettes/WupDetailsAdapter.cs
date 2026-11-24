// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.Palettes;
using WinRT;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

// Content is loaded on the first GetContent() call rather than when the list item is created, so
// building a result list doesn't issue a content request to the source for every item.
[WinRTRuntimeClassName("Microsoft.CommandPalette.Extensions.IDetails2")]
internal sealed partial class WupDetailsAdapter : Details, IDisposable
{
    private readonly PaletteItemView _item;
    private readonly object _stateLock = new();
    private WupTemplateContentAdapter? _content;
    private int _loadRequested;
    private long _loadGeneration;
    private bool _disposed;

    internal WupDetailsAdapter(PaletteItemView item)
    {
        _item = item;
        _item.ContentChanged += Item_ContentChanged;
    }

    public override IContent[] GetContent()
    {
        if (Interlocked.Exchange(ref _loadRequested, 1) == 0)
        {
            _ = LoadContentAsync();
        }

        return base.GetContent();
    }

    public void Dispose()
    {
        WupTemplateContentAdapter? content;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            content = _content;
            _content = null;
        }

        _item.ContentChanged -= Item_ContentChanged;
        content?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Item_ContentChanged(PaletteItemView sender, object args)
    {
        if (Volatile.Read(ref _loadRequested) != 0)
        {
            _ = LoadContentAsync();
        }
    }

    private async Task LoadContentAsync()
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        if (_disposed || !_item.IsContentAvailable)
        {
            return;
        }

        PaletteContentView view;
        try
        {
            view = await _item.GetContentAsync();
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Failed to load content for '{_item.ExtensionId}:{_item.Id}': 0x{ex.HResult:X8} {ex.Message}");
            if (!_disposed && generation == Volatile.Read(ref _loadGeneration))
            {
                Content = [];
            }

            return;
        }

        if (view is not PaletteTemplateContentView template)
        {
            return;
        }

        var content = new WupTemplateContentAdapter(template);
        WupTemplateContentAdapter? oldContent;
        lock (_stateLock)
        {
            if (_disposed || generation != Volatile.Read(ref _loadGeneration))
            {
                oldContent = content;
                content = null;
            }
            else
            {
                oldContent = _content;
                _content = content;
            }
        }

        oldContent?.Dispose();
        if (content is not null)
        {
            Content = [content];
        }
    }
}
