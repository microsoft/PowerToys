// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

/// <summary>
/// Edits only touched leaves. Reset removes a local key, including empty parent objects,
/// rather than pinning today's recommendation. Unrepresented author and local fields survive.
/// </summary>
public sealed class PowerScriptSettingsEditor
{
    private static readonly string[] RequiredPath = { "inputSchema", "required" };

    private readonly JsonObject _recommended;
    private readonly JsonObject _effective;

    public PowerScriptSettingsEditor(PowerScriptSettingsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _recommended = (JsonObject)snapshot.Recommended.DeepClone();
        _effective = (JsonObject)snapshot.Effective.DeepClone();
        Overrides = (JsonObject)snapshot.Overrides.DeepClone();
    }

    public JsonObject Overrides { get; }

    /// <summary>
    /// Tracks the display value already represented by the draft, independently of control events.
    /// Accept reset values before assigning controls: WinUI may dispatch change events later.
    /// </summary>
    public sealed class FieldEditState<T>
    {
        private T _committedDisplayValue;

        public FieldEditState(T initialDisplayValue)
        {
            _committedDisplayValue = initialDisplayValue;
        }

        public bool HasChangedDisplay(T currentDisplayValue) =>
            !EqualityComparer<T>.Default.Equals(_committedDisplayValue, currentDisplayValue);

        public void AcceptDisplay(T displayValue) => _committedDisplayValue = displayValue;
    }

    public JsonNode Read(params string[] path) => Find(_effective, path)?.DeepClone();

    public void Set(JsonNode value, params string[] path)
    {
        ExpandDeletedAncestors(path);
        Write(Overrides, path, value);
        if (value is null)
        {
            Remove(_effective, path, 0);
        }
        else
        {
            Write(_effective, path, value);
        }
    }

    public void Reset(params string[] path)
    {
        ExpandDeletedAncestors(path);
        Remove(Overrides, path, 0);
        var recommended = Find(_recommended, path);
        if (recommended is null)
        {
            Remove(_effective, path, 0);
        }
        else
        {
            Write(_effective, path, recommended);
        }
    }

    public IEnumerable<string> ParameterNames =>
        (Read("inputSchema", "properties") as JsonObject)?.Select(pair => pair.Key) ?? Enumerable.Empty<string>();

    public void SetParameterRequired(string name, bool isRequired)
    {
        var required = (Read("inputSchema", "required") as JsonArray)?
            .Select(item => item.GetValue<string>()).ToList() ?? new List<string>();
        required.RemoveAll(item => string.Equals(item, name, StringComparison.Ordinal));
        if (isRequired)
        {
            required.Add(name);
        }

        Set(new JsonArray(required.Select(item => (JsonNode)JsonValue.Create(item)).ToArray()), "inputSchema", "required");
    }

    public void ResetParameter(string name)
    {
        Reset("inputSchema", "properties", name);
        ResetParameterRequired(name);
    }

    public void ResetParameterRequired(string name)
    {
        var recommendedRequired = (Find(_recommended, RequiredPath) as JsonArray)?
            .Select(item => item.GetValue<string>()).ToArray() ?? Array.Empty<string>();
        SetParameterRequired(name, recommendedRequired.Contains(name, StringComparer.Ordinal));
        var currentRequired = (Read("inputSchema", "required") as JsonArray)?
            .Select(item => item.GetValue<string>()).ToArray() ?? Array.Empty<string>();
        if (currentRequired.OrderBy(item => item, StringComparer.Ordinal)
            .SequenceEqual(recommendedRequired.OrderBy(item => item, StringComparer.Ordinal), StringComparer.Ordinal))
        {
            Reset("inputSchema", "required");
        }
    }

    private static JsonNode Find(JsonObject root, IEnumerable<string> path)
    {
        JsonNode node = root;
        foreach (var segment in path)
        {
            node = (node as JsonObject)?[segment];
        }

        return node;
    }

    private void ExpandDeletedAncestors(string[] path)
    {
        var local = Overrides;
        JsonObject recommended = _recommended;
        foreach (var segment in path.Take(path.Length - 1))
        {
            if (!local.ContainsKey(segment))
            {
                return;
            }

            var recommendedChild = recommended?[segment] as JsonObject;
            if (local[segment] is null && recommendedChild is not null)
            {
                // Editing/resetting a leaf beneath a whole-group deletion must not silently
                // resurrect its other author fields, or leave the leaf hidden by that deletion.
                var deletions = new JsonObject();
                foreach (var property in recommendedChild)
                {
                    deletions[property.Key] = null;
                }

                local[segment] = deletions;
            }

            if (local[segment] is not JsonObject child)
            {
                return;
            }

            local = child;
            recommended = recommendedChild;
        }
    }

    private static void Write(JsonObject root, string[] path, JsonNode value)
    {
        if (path.Length == 0)
        {
            throw new ArgumentException("A property path is required.", nameof(path));
        }

        var parent = root;
        foreach (var segment in path.Take(path.Length - 1))
        {
            if (parent[segment] is not JsonObject child)
            {
                child = new JsonObject();
                parent[segment] = child;
            }

            parent = child;
        }

        parent[path[^1]] = value?.DeepClone();
    }

    private static void Remove(JsonObject root, string[] path, int index)
    {
        if (index == path.Length - 1)
        {
            root.Remove(path[index]);
        }
        else if (root[path[index]] is JsonObject child)
        {
            Remove(child, path, index + 1);
            if (child.Count == 0)
            {
                root.Remove(path[index]);
            }
        }
    }
}
