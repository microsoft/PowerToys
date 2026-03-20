// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using ManagedCommon;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Default implementation of <see cref="IPersistenceService"/> that reads/writes
/// JSON files with a shallow-merge strategy to preserve unknown keys.
/// </summary>
public sealed class PersistenceService : IPersistenceService
{
    /// <inheritdoc/>
    public T Load<T>(string filePath, JsonTypeInfo<T> typeInfo)
        where T : new()
    {
        if (!File.Exists(filePath))
        {
            Debug.WriteLine("The provided settings file does not exist");
            return new T();
        }

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var loaded = JsonSerializer.Deserialize(jsonContent, typeInfo);
            Debug.WriteLine(loaded is not null ? "Loaded settings file" : "Failed to parse");
            return loaded ?? new T();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
        }

        return new T();
    }

    /// <inheritdoc/>
    public void Save<T>(T model, string filePath, JsonTypeInfo<T> typeInfo, Action<JsonObject>? postProcessMerge = null)
    {
        try
        {
            var settingsJson = JsonSerializer.Serialize(model, typeInfo);

            if (JsonNode.Parse(settingsJson) is not JsonObject newSettings)
            {
                Logger.LogError("Failed to parse serialized model as JsonObject.");
                return;
            }

            // Read existing file content for merge
            var oldContent = File.Exists(filePath) ? File.ReadAllText(filePath) : "{}";

            if (JsonNode.Parse(oldContent) is not JsonObject savedSettings)
            {
                savedSettings = new JsonObject();
            }

            // Shallow merge: new values win, unknown keys preserved
            foreach (var item in newSettings)
            {
                savedSettings[item.Key] = item.Value?.DeepClone();
            }

            // Let callers remove deprecated keys or apply fixups
            postProcessMerge?.Invoke(savedSettings);

            var serialized = savedSettings.ToJsonString(typeInfo.Options);
            File.WriteAllText(filePath, serialized);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save to {filePath}:", ex);
        }
    }
}
