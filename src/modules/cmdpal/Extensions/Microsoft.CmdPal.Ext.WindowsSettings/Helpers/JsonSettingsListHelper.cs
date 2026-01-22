// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using ManagedCommon;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Helpers;

/// <summary>
/// Helper class to easier work with the JSON file that contains all Windows settings
/// </summary>
internal static class JsonSettingsListHelper
{
    /// <summary>
    /// The name of the file that contains all settings for the query
    /// </summary>
    private const string _settingsFile = "WindowsSettings.json";

    private const string _extTypeNamespace = "Microsoft.CmdPal.Ext.WindowsSettings";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
    };

    /// <summary>
    /// Read all possible Windows settings.
    /// </summary>
    /// <returns>A list with all possible windows settings.</returns>
    internal static Classes.WindowsSettings ReadAllPossibleSettings()
    {
        var assembly = Assembly.GetExecutingAssembly();

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
        Classes.WindowsSettings? settings = null;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

        try
        {
            var resourceName = $"{_extTypeNamespace}.{_settingsFile}";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream), "stream is null");
            }

            var options = _serializerOptions;

            // Why we need it? I don't see any enum usage in WindowsSettings
            // options.Converters.Add(new JsonStringEnumConverter());
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();

            settings = JsonSerializer.Deserialize(text, WindowsSettingsJsonSerializationContext.Default.WindowsSettings);
        }
#pragma warning disable CS0168
        catch (Exception exception)
        {
            // TODO GH #108 Logging is something we have to take care of
            // Log.Exception("Error loading settings JSON file", exception, typeof(JsonSettingsListHelper));
            Logger.LogError($"Error loading settings JSON file: {exception.Message}");
        }
#pragma warning restore CS0168
        return settings ?? new Classes.WindowsSettings();
    }
}
