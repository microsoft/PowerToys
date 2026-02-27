// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Services;

public sealed partial class PersistenceService
{
    private readonly ILogger<PersistenceService> _logger;
    private readonly IApplicationInfoService _applicationInfoService;

    public PersistenceService(
        ILogger<PersistenceService> logger,
        IApplicationInfoService applicationInfoService)
    {
        _logger = logger;
        _applicationInfoService = applicationInfoService;
    }

    private bool TryParseJsonObject(string json, [NotNullWhen(true)] out JsonObject? obj)
    {
        obj = null;
        try
        {
            obj = JsonNode.Parse(json) as JsonObject;
            return obj is not null;
        }
        catch (Exception ex)
        {
            Log_PersistenceParseFailure(ex);
            return false;
        }
    }

    private bool TryReadSavedObject(string filePath, [NotNullWhen(true)] out JsonObject? saved)
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
            Log_PersistenceReadFileFailure(filePath, ex);
            return false;
        }

        if (string.IsNullOrWhiteSpace(oldContent))
        {
            Log_FileEmpty(filePath);
            return false;
        }

        return TryParseJsonObject(oldContent, out saved);
    }

    public T LoadObject<T>(string fileName, JsonTypeInfo<T> typeInfo)
        where T : new()
    {
        if (string.IsNullOrEmpty(fileName))
        {
            throw new InvalidOperationException($"You must set a valid file name before loading {typeof(T).Name}");
        }

        var filePath = SettingsJsonPath(fileName);

        if (!File.Exists(filePath))
        {
            Log_FileDoesntExist(typeof(T).Name, filePath);
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
            Log_PersistenceReadFailure(typeof(T).Name, filePath, ex);
            return new T();
        }
    }

    public void SaveObject<T>(
        T model,
        string filePath,
        JsonTypeInfo<T> typeInfo,
        JsonSerializerOptions optionsForWrite,
        Action<JsonObject>? beforeWriteMutation,
        Action<T>? afterWriteCallback)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new InvalidOperationException($"You must set a valid file path before saving {typeof(T).Name}");
        }

        try
        {
            var json = JsonSerializer.Serialize(model, typeInfo);

            if (!TryParseJsonObject(json, out var newObj))
            {
                Log_SerializationError(typeof(T).Name);
                return;
            }

            if (!TryReadSavedObject(filePath, out var savedObj))
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
            Log_PersistenceSaveFailure(typeof(T).Name, filePath, ex);
        }
    }

    public string SettingsJsonPath(string fileName)
    {
        var directory = _applicationInfoService.ConfigDirectory;
        Directory.CreateDirectory(directory);

        // now, the settings is just next to the exe
        return Path.Combine(directory, fileName);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save {typeName} to '{filePath}'.")]
    partial void Log_PersistenceSaveFailure(string typeName, string filePath, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to read {typeName} from '{filePath}'.")]
    partial void Log_PersistenceReadFailure(string typeName, string filePath, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to serialize {typeName} to JsonObject.")]
    partial void Log_SerializationError(string typeName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "The provided {typeName} file does not exist ({filePath})")]
    partial void Log_FileDoesntExist(string typeName, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "The file at '{filePath}' is empty.")]
    partial void Log_FileEmpty(string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to read file at '{filePath}'.")]
    partial void Log_PersistenceReadFileFailure(string filePath, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to parse persisted JSON.")]
    partial void Log_PersistenceParseFailure(Exception exception);
}
