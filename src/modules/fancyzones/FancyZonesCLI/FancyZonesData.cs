// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FancyZonesCLI;

/// <summary>
/// Provides methods to read and write FancyZones configuration data.
/// </summary>
internal static class FancyZonesData
{
    /// <summary>
    /// Try to read applied layouts configuration.
    /// </summary>
    public static bool TryReadAppliedLayouts(out AppliedLayouts result, out string error)
    {
        return TryReadJsonFile(FancyZonesPaths.AppliedLayouts, FancyZonesJsonContext.Default.AppliedLayouts, out result, out error);
    }

    /// <summary>
    /// Read applied layouts or return null if not found.
    /// </summary>
    public static AppliedLayouts ReadAppliedLayouts()
    {
        return ReadJsonFileOrDefault(FancyZonesPaths.AppliedLayouts, FancyZonesJsonContext.Default.AppliedLayouts);
    }

    /// <summary>
    /// Write applied layouts configuration.
    /// </summary>
    public static void WriteAppliedLayouts(AppliedLayouts layouts)
    {
        WriteJsonFile(FancyZonesPaths.AppliedLayouts, layouts, FancyZonesJsonContext.Default.AppliedLayouts);
    }

    /// <summary>
    /// Read custom layouts or return null if not found.
    /// </summary>
    public static CustomLayouts ReadCustomLayouts()
    {
        return ReadJsonFileOrDefault(FancyZonesPaths.CustomLayouts, FancyZonesJsonContext.Default.CustomLayouts);
    }

    /// <summary>
    /// Read layout templates or return null if not found.
    /// </summary>
    public static LayoutTemplates ReadLayoutTemplates()
    {
        return ReadJsonFileOrDefault(FancyZonesPaths.LayoutTemplates, FancyZonesJsonContext.Default.LayoutTemplates);
    }

    /// <summary>
    /// Read layout hotkeys or return null if not found.
    /// </summary>
    public static LayoutHotkeys ReadLayoutHotkeys()
    {
        return ReadJsonFileOrDefault(FancyZonesPaths.LayoutHotkeys, FancyZonesJsonContext.Default.LayoutHotkeys);
    }

    /// <summary>
    /// Write layout hotkeys configuration.
    /// </summary>
    public static void WriteLayoutHotkeys(LayoutHotkeys hotkeys)
    {
        WriteJsonFile(FancyZonesPaths.LayoutHotkeys, hotkeys, FancyZonesJsonContext.Default.LayoutHotkeys);
    }

    /// <summary>
    /// Check if editor parameters file exists.
    /// </summary>
    public static bool EditorParametersExist()
    {
        return File.Exists(FancyZonesPaths.EditorParameters);
    }

    private static bool TryReadJsonFile<T>(string filePath, JsonTypeInfo<T> jsonTypeInfo, out T result, out string error)
        where T : class
    {
        result = null;
        error = null;

        Logger.LogDebug($"Reading file: {filePath}");

        if (!File.Exists(filePath))
        {
            error = $"File not found: {Path.GetFileName(filePath)}";
            Logger.LogWarning(error);
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            result = JsonSerializer.Deserialize(json, jsonTypeInfo);
            if (result == null)
            {
                error = $"Failed to parse {Path.GetFileName(filePath)}";
                Logger.LogError(error);
                return false;
            }

            Logger.LogDebug($"Successfully read {Path.GetFileName(filePath)}");
            return true;
        }
        catch (JsonException ex)
        {
            error = $"JSON parse error in {Path.GetFileName(filePath)}: {ex.Message}";
            Logger.LogError(error, ex);
            return false;
        }
        catch (IOException ex)
        {
            error = $"Failed to read {Path.GetFileName(filePath)}: {ex.Message}";
            Logger.LogError(error, ex);
            return false;
        }
    }

    private static T ReadJsonFileOrDefault<T>(string filePath, JsonTypeInfo<T> jsonTypeInfo, T defaultValue = null)
        where T : class
    {
        if (TryReadJsonFile(filePath, jsonTypeInfo, out var result, out _))
        {
            return result;
        }

        return defaultValue;
    }

    private static void WriteJsonFile<T>(string filePath, T data, JsonTypeInfo<T> jsonTypeInfo)
    {
        Logger.LogDebug($"Writing file: {filePath}");
        var json = JsonSerializer.Serialize(data, jsonTypeInfo);
        File.WriteAllText(filePath, json);
        Logger.LogInfo($"Successfully wrote {Path.GetFileName(filePath)}");
    }
}
