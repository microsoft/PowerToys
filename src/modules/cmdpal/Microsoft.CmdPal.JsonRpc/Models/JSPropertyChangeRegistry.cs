// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.CmdPal.JsonRpc;

namespace Microsoft.CmdPal.JsonRpc.Models;

internal static class JSPropertyChangeRegistry
{
    private static readonly ConditionalWeakTable<JsonRpcConnection, Registry> Registries = new();

    internal static void Register(JsonRpcConnection connection, string commandId, IJSPropertyChangeTarget target)
    {
        var registry = Registries.GetValue(connection, static _ => new Registry());
        registry.Targets.Register(commandId, target);
    }

    internal static void Unregister(JsonRpcConnection connection, string commandId, IJSPropertyChangeTarget target)
    {
        if (!Registries.TryGetValue(connection, out var registry))
        {
            return;
        }

        registry.Targets.Unregister(commandId, target);
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
        if (commandId is null)
        {
            return;
        }

        foreach (var target in registry.Targets.GetLiveTargets(commandId))
        {
            target.ApplyPropertyChanges(commandId, properties);
        }
    }

    internal static int GetRegistrationCount(JsonRpcConnection connection, string commandId)
    {
        return Registries.TryGetValue(connection, out var registry)
            ? registry.Targets.GetRegistrationCount(commandId)
            : 0;
    }

    private sealed class Registry
    {
        internal JSWeakReferenceRegistry<string, IJSPropertyChangeTarget> Targets { get; } = new();
    }
}
