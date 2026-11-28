// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

/// <summary>A PowerToys settings folder of its own for one test, deleted afterwards.</summary>
internal sealed class TestFolder : IDisposable
{
    public TestFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "CmdPalTextExpanderTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = new TextExpanderPaths(Root);
    }

    public string Root { get; }

    public TextExpanderPaths Paths { get; }

    /// <summary>Gets Text Expander's default snippets file in this folder.</summary>
    public string DefaultSnippetsFile => Path.Combine(Paths.ModuleFolder, "PowerToysTextExpanderSnippets.json");

    /// <summary>Writes PowerToys' general settings with Text Expander on or off.</summary>
    public void SetEnabled(bool enabled)
        => File.WriteAllText(Paths.GeneralSettingsFile, $$"""{ "startup": true, "enabled": { "Awake": true, "TextExpander": {{(enabled ? "true" : "false")}} } }""");

    /// <summary>Writes Text Expander's settings the way PowerToys Settings saves them.</summary>
    public void SetModuleSettings(string? snippetsPath = null, bool caseSensitive = true, string injectionBackend = "auto")
    {
        Directory.CreateDirectory(Paths.ModuleFolder);
        var properties = new JsonObject
        {
            ["snippets_path"] = new JsonObject { ["value"] = snippetsPath ?? string.Empty },
            ["injection_backend"] = new JsonObject { ["value"] = injectionBackend },
            ["case_sensitive"] = caseSensitive,
        };
        var settings = new JsonObject
        {
            ["name"] = "TextExpander",
            ["version"] = "1.0",
            ["properties"] = properties,
        };
        File.WriteAllText(Paths.ModuleSettingsFile, settings.ToJsonString());
    }

    /// <summary>Writes a snippets file of (trigger, replacement) pairs, by default Text Expander's own.</summary>
    public string WriteSnippets(IEnumerable<(string Trigger, string Replacement)> snippets, string? path = null)
    {
        path ??= DefaultSnippetsFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var list = new JsonArray();
        foreach (var (trigger, replacement) in snippets)
        {
            list.Add(new JsonObject { ["trigger"] = trigger, ["replacement"] = replacement });
        }

        File.WriteAllText(path, new JsonObject { ["version"] = 1, ["snippets"] = list }.ToJsonString());
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
