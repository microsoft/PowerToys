// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Services.JsonRpc;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

internal static class JSPropertyChangeRegistry
{
    private static readonly ConditionalWeakTable<JsonRpcConnection, Registry> Registries = new();

    internal static void Register(JsonRpcConnection connection, string commandId, IJSPropertyChangeTarget target)
    {
        var registry = Registries.GetValue(connection, static _ => new Registry());
        var targets = registry.Targets.GetOrAdd(commandId, static _ => []);
        lock (targets)
        {
            targets.Add(new WeakReference<IJSPropertyChangeTarget>(target));
        }
    }

    internal static void Unregister(JsonRpcConnection connection, string commandId, IJSPropertyChangeTarget target)
    {
        if (!Registries.TryGetValue(connection, out var registry) ||
            !registry.Targets.TryGetValue(commandId, out var targets))
        {
            return;
        }

        lock (targets)
        {
            targets.RemoveAll(reference =>
                !reference.TryGetTarget(out var current) || ReferenceEquals(current, target));
            if (targets.Count == 0)
            {
                registry.Targets.TryRemove(commandId, out _);
            }
        }
    }

    internal static void Dispatch(JsonRpcConnection connection, JsonElement paramsElement)
    {
        if (paramsElement.ValueKind != JsonValueKind.Object ||
            !paramsElement.TryGetProperty("commandId", out var commandIdProperty) ||
            commandIdProperty.ValueKind != JsonValueKind.String ||
            !paramsElement.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object ||
            !Registries.TryGetValue(connection, out var registry))
        {
            return;
        }

        var commandId = commandIdProperty.GetString();
        if (commandId is null || !registry.Targets.TryGetValue(commandId, out var targets))
        {
            return;
        }

        List<IJSPropertyChangeTarget> liveTargets = [];
        lock (targets)
        {
            targets.RemoveAll(reference => !reference.TryGetTarget(out _));
            foreach (var reference in targets)
            {
                if (reference.TryGetTarget(out var target))
                {
                    liveTargets.Add(target);
                }
            }
        }

        foreach (var target in liveTargets)
        {
            target.ApplyPropertyChanges(properties);
        }
    }

    private sealed class Registry
    {
        internal ConcurrentDictionary<string, List<WeakReference<IJSPropertyChangeTarget>>> Targets { get; } = new();
    }
}
