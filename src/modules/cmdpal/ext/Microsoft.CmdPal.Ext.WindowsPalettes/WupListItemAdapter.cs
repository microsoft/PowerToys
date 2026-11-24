// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.Palettes;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal sealed partial class WupListItemAdapter : ListItem, IDisposable
{
    private readonly PaletteItemView _item;
    private readonly IPaletteItemSourceExtension _source;
    private readonly PaletteQuery? _query;
    private readonly string _sourceDisplayName;
    private readonly WupAnalyticsSession? _analytics;
    private readonly string? _commandId;
    private WupDetailsAdapter? _details;
    private WupCommandShape _commandShape;
    private CancellationTokenSource? _commandsCancellation;
    private int _commandsRequested;

    internal WupListItemAdapter(
        PaletteItemView item,
        IPaletteItemSourceExtension source,
        PaletteQuery? query,
        string sourceDisplayName,
        WupAnalyticsSession? analytics,
        string? commandId = null)
    {
        _item = item;
        _source = source;
        _query = query;
        _sourceDisplayName = sourceDisplayName;
        _analytics = analytics;
        _commandId = commandId;
        Refresh();
        _item.PropertyChanged += Item_PropertyChanged;
    }

    public void Dispose()
    {
        _item.PropertyChanged -= Item_PropertyChanged;
        _details?.Dispose();
        _details = null;
        (Command as IDisposable)?.Dispose();
        var commandsCancellation = Interlocked.Exchange(ref _commandsCancellation, null);
        commandsCancellation?.Cancel();
        commandsCancellation?.Dispose();
        foreach (var contextItem in base.MoreCommands)
        {
            ((contextItem as CommandContextItem)?.Command as IDisposable)?.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    public override IContextItem[] MoreCommands
    {
        get
        {
            EnsureCommandsRequested();
            return base.MoreCommands;
        }
        set => base.MoreCommands = value;
    }

    private void EnsureCommandsRequested()
    {
        if (Interlocked.Exchange(ref _commandsRequested, 1) != 0)
        {
            return;
        }

        _commandsCancellation = new CancellationTokenSource();
        _ = LoadMoreCommandsAsync(_commandsCancellation.Token);
    }

    private async Task LoadMoreCommandsAsync(CancellationToken cancellationToken)
    {
        var query = new PaletteQuery
        {
            Arguments = [],
            Mode = PaletteQueryMode.Commands,
        };

        try
        {
            WupDiagnostics.Write($"Item commands query started for '{_item.ExtensionId}:{_item.Id}'.");
            PaletteItemView[] items;
            try
            {
                var itemResults = await _item.ExecuteQueryAsync(query).AsCancellableTask(cancellationToken);
                items = itemResults.Items ?? [];
                WupDiagnostics.Write($"Item commands query returned {items.Length} item(s) for '{_item.ExtensionId}:{_item.Id}'.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                items = [];
                WupDiagnostics.Write($"Item commands query failed for '{_item.ExtensionId}:{_item.Id}': 0x{ex.HResult:X8}; using source-scoped fallback.");
            }

            if (items.Length == 0)
            {
                query.Scopes = [_item];
                WupDiagnostics.Write($"Source-scoped commands fallback started for '{_item.ExtensionId}:{_item.Id}'.");
                var sourceResults = await _source.ExecuteQueryAsync(query).AsCancellableTask(cancellationToken);
                items = sourceResults.Items ?? [];
                WupDiagnostics.Write($"Source-scoped commands fallback returned {items.Length} item(s) for '{_item.ExtensionId}:{_item.Id}'.");
            }

            var commands = new IContextItem[items.Length];
            for (var index = 0; index < items.Length; index++)
            {
                var item = items[index];
                commands[index] = new CommandContextItem(
                    WupCommandFactory.Create(item, _source, _query, _sourceDisplayName, _analytics))
                {
                    Title = item.Title ?? string.Empty,
                    Subtitle = WupTextAdapter.CreateSubtitle(item.Subtitle),
                    Icon = WupIconAdapter.Create(item.Icon),
                };
            }

            MoreCommands = commands;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Commands failed for '{_item.ExtensionId}:{_item.Id}': 0x{ex.HResult:X8}.");
        }
    }

    private void Item_PropertyChanged(PaletteItemView sender, PalettePropertyChangedEventArgs args) => Refresh();

    internal double? Score { get; private set; }

    internal string? DeduplicationKey { get; private set; }

    private void Refresh()
    {
        Title = _item.Title ?? string.Empty;
        Subtitle = WupTextAdapter.CreateSubtitle(_item.Subtitle);
        Icon = WupIconAdapter.Create(_item.Icon);
        // The robots thought adding the source as a tag was a good idea, but it is not. Too noisy.
        // Tags = WupTextAdapter.CreateTags(_item.Tags, _sourceDisplayName);
        Tags = WupTextAdapter.CreateStatusTags(_item.Status);
        RefreshRankingProperties();

        var hasContent = _item.IsContentAvailable;
        if (hasContent != (_details is not null))
        {
            var oldDetails = _details;
            _details = hasContent ? new WupDetailsAdapter(_item) : null;
            Details = _details;
            oldDetails?.Dispose();
        }

        var shape = WupCommandFactory.GetShape(_item);
        if (Command is null || shape != _commandShape)
        {
            var oldCommand = Command;
            _commandShape = shape;
            Command = WupCommandFactory.Create(_item, _source, _query, _sourceDisplayName, _analytics, _commandId);
            (oldCommand as IDisposable)?.Dispose();
        }
    }

    private void RefreshRankingProperties()
    {
        Score = null;
        DeduplicationKey = null;
        try
        {
            var properties = _item.Properties;
            if (properties is null)
            {
                return;
            }

            if (properties.TryGetValue(KnownPaletteProperties.Score, out var score) && score is double value && double.IsFinite(value))
            {
                Score = value;
            }

            if (properties.TryGetValue(KnownPaletteProperties.KindSpecificId, out var id) && id is string kindSpecificId && kindSpecificId.Length > 0)
            {
                DeduplicationKey = $"{_item.Kinds?.GetPrimaryKind()}\u001F{kindSpecificId}";
            }
        }
        catch (Exception ex)
        {
            WupDiagnostics.Write($"Failed to read properties for '{_item.ExtensionId}:{_item.Id}': 0x{ex.HResult:X8} {ex.Message}");
        }
    }
}
