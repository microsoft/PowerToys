// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.IO;
using System.Text.Json;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

#nullable enable

public sealed class AppSettingsManager
{
    private const string FileName = "appsettings.json";

    private static string SettingsPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, FileName);
    }

    private readonly string _filePath;

    public AppSettings Current { get; private set; } = new();

    public AppSettingsManager()
    {
        _filePath = SettingsPath();
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize(json, AppSettingsJsonContext.Default.AppSettings);
                if (loaded is not null)
                {
                    Current = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage { Message = ex.ToString() });
            Logger.LogError("Failed to load app settings", ex);
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, AppSettingsJsonContext.Default.AppSettings);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage { Message = ex.ToString() });
            Logger.LogError("Failed to save app settings", ex);
        }
    }
}
