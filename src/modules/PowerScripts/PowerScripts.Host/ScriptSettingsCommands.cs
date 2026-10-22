// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PowerScripts.Core.Storage;

namespace PowerScripts.Host;

internal static class ScriptSettingsCommands
{
    public static int Run(string root, IReadOnlyList<string> positional, IReadOnlyDictionary<string, List<string>> options)
    {
        if (positional.Count != 1 ||
            !options.TryGetValue("entry", out var entries) || entries.Count != 1 ||
            string.IsNullOrWhiteSpace(entries[0]) ||
            options.Keys.Any(key => !key.Equals("entry", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("root", StringComparison.OrdinalIgnoreCase)))
        {
            Console.Error.WriteLine("script-settings: use get|save|reset|forget --entry <script-path> [--root <dir>].");
            return 1;
        }

        var store = new ScriptSettingsStore();
        var entry = entries[0];
        ScriptSettingsSnapshot snapshot;
        switch (positional[0].ToLowerInvariant())
        {
            case "get":
                snapshot = store.Get(root, entry);
                break;
            case "save":
                using (var reader = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8))
                {
                    var buffer = new char[4096];
                    var text = new StringBuilder();
                    int count;
                    while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (text.Length + count > ScriptSettingsStore.MaximumRecordBytes)
                        {
                            throw new InvalidDataException("Script settings request is too large.");
                        }

                        text.Append(buffer, 0, count);
                    }

                    var request = JsonNode.Parse(text.ToString(), documentOptions: new JsonDocumentOptions { MaxDepth = 32 }) as JsonObject
                        ?? throw new InvalidDataException("Script settings request must be a JSON object.");
                    snapshot = store.Save(root, entry, request);
                }

                break;
            case "reset":
                snapshot = store.Reset(root, entry);
                break;
            case "forget":
                store.Forget(root, entry);
                return 0;
            default:
                Console.Error.WriteLine("script-settings: unknown operation. Use get, save, reset, or forget.");
                return 1;
        }

        Console.WriteLine(JsonSerializer.Serialize(snapshot, ScriptSettingsStore.JsonOptions));
        return 0;
    }
}
