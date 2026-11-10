// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerScripts.Client;

/// <summary>The effective preference and policy state for one PowerScripts integration.</summary>
public sealed class PowerScriptConsumerState
{
    /// <summary>The caller-supplied consumer identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Whether the shared PowerScripts module is enabled.</summary>
    public bool IsPowerScriptsEnabled { get; init; }

    /// <summary>Whether Group Policy controls the shared module's enabled state.</summary>
    public bool IsPowerScriptsPolicyControlled { get; init; }

    /// <summary>The saved consumer preference, before Group Policy is applied.</summary>
    public bool UserEnabled { get; init; }

    /// <summary>Whether this consumer may discover and execute scripts.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>Whether the consumer preference may be changed in PowerScripts settings.</summary>
    public bool CanChange { get; init; }

    /// <summary>Whether Group Policy controls the consumer or disables its parent utility.</summary>
    public bool IsPolicyControlled { get; init; }

    /// <summary>
    /// The Host's reason for a disabled parent, such as <c>module-disabled</c> or
    /// <c>parent-policy-disabled</c>. The Host omits this field when it has no reason, including a
    /// disabled consumer preference or consumer policy;
    /// use <see cref="IsEnabled"/> and <see cref="IsPolicyControlled"/> to distinguish those cases.
    /// </summary>
    public string? DisabledReason { get; init; }

    /// <summary>
    /// Reads one registered consumer from the JSON emitted by <c>consumers list</c>.
    /// Missing, duplicate, or malformed state throws <see cref="JsonException"/> instead of
    /// treating an unavailable integration as an empty script catalogue. Uses no reflection.
    /// </summary>
    public static PowerScriptConsumerState ParseSnapshot(string json, string consumerId)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumerId);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("consumers", out var consumers) || consumers.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("PowerScripts did not return a consumer settings snapshot.");
        }

        var moduleEnabled = ReadBoolean(root, "isPowerScriptsEnabled");
        var modulePolicyControlled = ReadBoolean(root, "isPowerScriptsPolicyControlled");
        JsonElement? consumer = null;
        foreach (var item in consumers.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("PowerScripts consumer state must contain an id.");
            }

            if (!string.Equals(id.GetString(), consumerId, StringComparison.Ordinal))
            {
                continue;
            }

            if (consumer.HasValue)
            {
                throw new JsonException($"PowerScripts returned duplicate state for consumer '{consumerId}'.");
            }

            consumer = item;
        }

        var state = consumer ?? throw new JsonException($"PowerScripts did not return state for consumer '{consumerId}'.");
        var hasReason = state.TryGetProperty("disabledReason", out var reason);
        if (hasReason && reason.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
        {
            throw new JsonException("PowerScripts consumer disabledReason must be a string or null.");
        }

        return new PowerScriptConsumerState
        {
            Id = consumerId,
            IsPowerScriptsEnabled = moduleEnabled,
            IsPowerScriptsPolicyControlled = modulePolicyControlled,
            UserEnabled = ReadBoolean(state, "userEnabled"),
            IsEnabled = ReadBoolean(state, "isEnabled"),
            CanChange = ReadBoolean(state, "canChange"),
            IsPolicyControlled = ReadBoolean(state, "isPolicyControlled"),
            DisabledReason = hasReason ? reason.GetString() : null,
        };
    }

    private static bool ReadBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new JsonException($"PowerScripts consumer settings '{name}' must be a boolean.");
        }

        return value.GetBoolean();
    }
}
