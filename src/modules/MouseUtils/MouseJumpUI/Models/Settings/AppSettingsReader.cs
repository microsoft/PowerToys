// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCommon;

namespace MouseJumpUI.Models.Settings;

internal static class AppSettingsReader
{
    public static AppSettings ReadFile(string filename)
    {
        // determine the version of the config file so we know which converter to use
        var configJson = File.ReadAllText(filename);
        return AppSettingsReader.ParseJson(configJson);
    }

    public static AppSettings ParseJson(string? configJson)
    {
        if (configJson is null)
        {
            return AppSettings.DefaultSettings;
        }

        Logger.LogInfo($"config json =");
        Logger.LogInfo($"---");
        Logger.LogInfo(configJson);
        Logger.LogInfo($"---");

        try
        {
            var configNode = JsonNode.Parse(configJson);

            // determine the version of the config file so we know which converter to use
            // (if the version isn't specified or isn't valid we'll default to v1.0)
            var configVersion = configNode?["version"]?.GetValue<string>() ?? "1.0";
            Logger.LogInfo($"config version = '{configVersion}'");

            var appSettings = configVersion switch
            {
                "1.0" => V1.SettingsConverter.ParseAppSettings(configJson),
                _ => AppSettings.DefaultSettings,
            };
            return appSettings;
        }
        catch (Exception ex)
        {
            Logger.LogInfo("exception occurred while loading config file, using default config instead");
            Logger.LogInfo(ex.ToString());
            return AppSettings.DefaultSettings;
        }
    }
}
