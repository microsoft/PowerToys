// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CmdPal.Ext.Apps.Packaged;
using Microsoft.CmdPal.Ext.Apps.Win32;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public sealed class MockAppCatalog : IAppCatalog
{
    public event EventHandler<AppCatalogChangedEventArgs>? Changed;

    public event EventHandler? RefreshStateChanged;

    public event EventHandler<AppVisibilityChangedEventArgs>? VisibilityChanged;

    private readonly List<AppItem> _items = [];
    private readonly List<AppItem> _hiddenItems = [];
    private Task _initializationTask = Task.CompletedTask;
    private bool _disposed;

    public bool IsRefreshing { get; private set; }

    public int InitializeCallCount { get; private set; }

    public int RefreshCallCount { get; private set; }

    public Task RefreshCompletion { get; set; } = Task.CompletedTask;

    public AppCatalogSnapshot GetSnapshot()
    {
        return new(_items.AsReadOnly(), _hiddenItems.AsReadOnly());
    }

    public Task InitializeAsync()
    {
        InitializeCallCount++;
        return _initializationTask;
    }

    public void DeferInitialization(Task initializationTask)
    {
        ArgumentNullException.ThrowIfNull(initializationTask);

        _initializationTask = initializationTask;
    }

    public async Task RefreshAsync()
    {
        var completion = RefreshCompletion;
        RefreshCallCount++;
        SetRefreshing(true);
        await Task.Yield();
        await completion;
        SetRefreshing(false);
    }

    public void SetRefreshing(bool isRefreshing)
    {
        IsRefreshing = isRefreshing;
        RefreshStateChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void AddWin32Program(Win32AppMetadata program)
    {
        ArgumentNullException.ThrowIfNull(program);
        AddAndRaise(Win32AppPayload.From(program));
    }

    internal void AddPackagedApp(PackagedAppMetadata app)
    {
        ArgumentNullException.ThrowIfNull(app);
        AddAndRaise(PackagedAppPayload.From(app));
    }

    public Task SetAppHiddenAsync(string catalogId, bool hidden)
    {
        if (TryMoveApp(catalogId, hidden, out _))
        {
            VisibilityChanged?.Invoke(this, new AppVisibilityChangedEventArgs(catalogId, hidden));
        }

        return Task.CompletedTask;
    }

    public void PublishVisibilityAsCatalogChange(string catalogId, bool hidden)
    {
        if (TryMoveApp(catalogId, hidden, out var app))
        {
            Changed?.Invoke(
                this,
                new AppCatalogChangedEventArgs(
                    [new AppCatalogItemChange(AppCatalogChangeKind.Updated, catalogId, app, hidden)]));
        }
    }

    public void ClearAll()
    {
        var changes = new List<AppCatalogItemChange>(_items.Count + _hiddenItems.Count);
        AddRemovals(changes, _items);
        AddRemovals(changes, _hiddenItems);
        _items.Clear();
        _hiddenItems.Clear();
        if (changes.Count > 0)
        {
            Changed?.Invoke(this, new AppCatalogChangedEventArgs(changes));
        }
    }

    private void AddAndRaise(IAppCatalogPayload payload)
    {
        var app = payload.ToAppItem();
        app.CatalogId = payload switch
        {
            Win32AppPayload win32 => $"{win32.Name}|{win32.TargetPath}",
            PackagedAppPayload packaged => packaged.AppUserModelId,
            _ => throw new ArgumentException("Unsupported application payload.", nameof(payload)),
        };
        app.CommandIds = [payload.GetCommandId(), .. AppIdentity.GetCommandIds(app.CatalogId)];
        _items.Add(app);
        Changed?.Invoke(
            this,
            new AppCatalogChangedEventArgs(
                [new AppCatalogItemChange(AppCatalogChangeKind.Added, app.CatalogId, app, hidden: false)]));
    }

    private bool TryMoveApp(string catalogId, bool hidden, out AppItem? app)
    {
        var source = hidden ? _items : _hiddenItems;
        var destination = hidden ? _hiddenItems : _items;
        for (var i = 0; i < source.Count; i++)
        {
            if (string.Equals(source[i].CatalogId, catalogId, StringComparison.OrdinalIgnoreCase))
            {
                app = source[i];
                source.RemoveAt(i);
                destination.Add(app);
                return true;
            }
        }

        app = null;
        return false;
    }

    private static void AddRemovals(List<AppCatalogItemChange> changes, IReadOnlyList<AppItem> items)
    {
        foreach (var item in items)
        {
            changes.Add(new AppCatalogItemChange(AppCatalogChangeKind.Removed, item.CatalogId, null, hidden: false));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _items.Clear();
        _hiddenItems.Clear();
        Changed = null;
        RefreshStateChanged = null;
        VisibilityChanged = null;
    }
}
