// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;

namespace Microsoft.CmdPal.Ext.Apps.State;

public sealed class PinnedApps
{
    public List<string> PinnedAppIdentifiers { get; set; } = [];

    public static PinnedApps ReadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return new PinnedApps();
        }

        try
        {
            var jsonString = File.ReadAllText(path);
            var result = JsonSerializer.Deserialize<PinnedApps>(jsonString, JsonSerializationContext.Default.PinnedApps);
            return result ?? new PinnedApps();
        }
        catch
        {
            return new PinnedApps();
        }
    }

    public static void WriteToFile(string path, PinnedApps data)
    {
        try
        {
            var jsonString = JsonSerializer.Serialize(data, JsonSerializationContext.Default.PinnedApps);
            File.WriteAllText(path, jsonString);
        }
        catch
        {
            // Silently fail - we don't want pinning issues to crash the extension
        }
    }
}
