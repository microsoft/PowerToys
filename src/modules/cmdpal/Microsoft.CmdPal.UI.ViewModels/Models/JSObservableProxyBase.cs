// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

internal abstract class JSObservableProxyBase : BaseObservable, IJSPropertyChangeTarget, IDisposable
{
    private readonly string _commandId;
    private readonly JsonRpcConnection _connection;
    private DataBox _data;
    private bool _disposed;

    protected JSObservableProxyBase(string commandId, JsonRpcConnection connection, JsonElement data)
    {
        _commandId = commandId ?? throw new ArgumentNullException(nameof(commandId));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _data = new DataBox(data);
        JSPropertyChangeRegistry.Register(connection, commandId, this);
    }

    protected JsonRpcConnection Connection => _connection;

    protected JsonElement Data => Volatile.Read(ref _data).Element;

    protected abstract bool SupportsProperty(string propertyName);

    public void ApplyPropertyChanges(JsonElement properties)
    {
        var current = Data;
        if (current.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var changed = new List<string>();
        var merged = JsonNode.Parse(current.GetRawText()) as JsonObject;
        if (merged is null)
        {
            return;
        }

        foreach (var property in properties.EnumerateObject())
        {
            if (!SupportsProperty(property.Name))
            {
                continue;
            }

            merged[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            changed.Add(property.Name);
        }

        if (changed.Count == 0)
        {
            return;
        }

        using var document = JsonDocument.Parse(merged.ToJsonString());
        Volatile.Write(ref _data, new DataBox(document.RootElement.Clone()));
        OnPropertyChangesApplied(changed);

        foreach (var property in changed)
        {
            OnPropertyChanged(ToAbiPropertyName(property));
        }
    }

    protected virtual void OnPropertyChangesApplied(IReadOnlyList<string> propertyNames)
    {
    }

    public virtual void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        JSPropertyChangeRegistry.Unregister(_connection, _commandId, this);
    }

    private static string ToAbiPropertyName(string propertyName)
    {
        return propertyName.Length == 0
            ? propertyName
            : char.ToUpperInvariant(propertyName[0]) + propertyName[1..];
    }

    private sealed class DataBox
    {
        internal DataBox(JsonElement element) => Element = element;

        internal JsonElement Element { get; }
    }
}
