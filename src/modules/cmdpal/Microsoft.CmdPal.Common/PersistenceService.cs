// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common;

public partial class PersistenceService
{
    private static bool TryParseJsonObject(string json, ILogger logger, [NotNullWhen(true)] out JsonObject? obj)
    {
        obj = null;
        try
        {
            obj = JsonNode.Parse(json) as JsonObject;
            return obj is not null;
        }
        catch (Exception ex)
        {
            Log_PersistenceParseFailure(logger, ex);
            return false;
        }
    }

    private static bool TryReadSavedObject(string filePath, ILogger logger, [NotNullWhen(true)] out JsonObject? saved)
    {
        saved = null;

        string oldContent;
        try
        {
            if (!File.Exists(filePath))
            {
                saved = new JsonObject();
                return true;
            }

            oldContent = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            Log_PersistenceReadFileFailure(logger, filePath, ex);
            return false;
        }

        if (string.IsNullOrWhiteSpace(oldContent))
        {
            Log_FileEmpty(logger, filePath);
            return false;
        }

        return TryParseJsonObject(oldContent, logger, out saved);
    }

    public static T LoadObject<T>(string filePath, JsonTypeInfo<T> typeInfo, ILogger logger)
        where T : new()
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new InvalidOperationException($"You must set a valid file path before loading {typeof(T).Name}");
        }

        if (!File.Exists(filePath))
        {
            Log_FileDoesntExist(logger, typeof(T).Name, filePath);
            return new T();
        }

        try
        {
            var jsonContent = File.ReadAllText(filePath);
            var loaded = JsonSerializer.Deserialize(jsonContent, typeInfo);
            return loaded ?? new T();
        }
        catch (Exception ex)
        {
            Log_PersistenceReadFailure(logger, typeof(T).Name, filePath, ex);
            return new T();
        }
    }

    public static void SaveObject<T>(
        T model,
        string filePath,
        JsonTypeInfo<T> typeInfo,
        JsonSerializerOptions optionsForWrite,
        Action<JsonObject>? beforeWriteMutation,
        Action<T>? afterWriteCallback,
        ILogger logger)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new InvalidOperationException($"You must set a valid file path before saving {typeof(T).Name}");
        }

        try
        {
            var json = JsonSerializer.Serialize(model, typeInfo);

            if (!TryParseJsonObject(json, logger, out var newObj))
            {
                Log_SerializationError(logger, typeof(T).Name);
                return;
            }

            if (!TryReadSavedObject(filePath, logger, out var savedObj))
            {
                savedObj = new JsonObject();
            }

            foreach (var kvp in newObj)
            {
                savedObj[kvp.Key] = kvp.Value?.DeepClone();
            }

            beforeWriteMutation?.Invoke(savedObj);

            var serialized = savedObj.ToJsonString(optionsForWrite);
            File.WriteAllText(filePath, serialized);

            afterWriteCallback?.Invoke(model);
        }
        catch (Exception ex)
        {
            Log_PersistenceSaveFailure(logger, typeof(T).Name, filePath, ex);
        }
    }

    public static string SettingsJsonPath(string fileName)
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CommandPalette");
        Directory.CreateDirectory(directory);

        // now, the settings is just next to the exe
        return Path.Combine(directory, fileName);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save {typeName} to '{filePath}'.")]
    static partial void Log_PersistenceSaveFailure(Extensions.Logging.ILogger logger, string typeName, string filePath, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to read {typeName} from '{filePath}'.")]
    static partial void Log_PersistenceReadFailure(Extensions.Logging.ILogger logger, string typeName, string filePath, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to serialize {typeName} to JsonObject.")]
    static partial void Log_SerializationError(Extensions.Logging.ILogger logger, string typeName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "The provided {typeName} file does not exist ({filePath})")]
    static partial void Log_FileDoesntExist(Extensions.Logging.ILogger logger, string typeName, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "The file at '{filePath}' is empty.")]
    static partial void Log_FileEmpty(Extensions.Logging.ILogger logger, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to read file at '{filePath}'.")]
    static partial void Log_PersistenceReadFileFailure(Extensions.Logging.ILogger logger, string filePath, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse persisted JSON.")]
    static partial void Log_PersistenceParseFailure(Extensions.Logging.ILogger logger, Exception exception);
}
