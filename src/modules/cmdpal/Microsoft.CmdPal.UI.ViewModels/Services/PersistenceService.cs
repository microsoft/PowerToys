// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            Logger.LogDebug("Settings file not found at {FilePath}", filePath);
            return new T();
        }

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var loaded = JsonSerializer.Deserialize(jsonContent, typeInfo);

            if (loaded is null)
            {
                Logger.LogDebug("Failed to parse settings file at {FilePath}", filePath);
            }
            else
            {
                Logger.LogDebug("Successfully loaded settings file from {FilePath}", filePath);
            }

            return loaded ?? new T();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load settings from {filePath}:", ex);
        }

        return new T();
    }

    /// <inheritdoc/>
    public void Save<T>(T model, string filePath, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            var settingsJson = JsonSerializer.Serialize(model, typeInfo);

            if (JsonNode.Parse(settingsJson) is not JsonObject newSettings)
            {
                Logger.LogError("Failed to parse serialized model as JsonObject.");
                return;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serialized = newSettings.ToJsonString(typeInfo.Options);
            File.WriteAllText(filePath, serialized);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save to {filePath}:", ex);
        }
    }
}
