// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

/// <summary>Read-only presentation of authoritative metadata; never generates settings overrides.</summary>
public static class PowerScriptMetadataDetails
{
    public static IReadOnlyList<PowerScriptMetadataSection> Create(JsonObject descriptor, Func<string, string> getString, string entryExtension = null)
    {
        string Text(string key) => getString("PowerScripts_Details" + key);
        string Value(JsonNode node) => node switch
        {
            null => Text("Null"),
            JsonValue value when value.GetValueKind() == JsonValueKind.String =>
                value.GetValue<string>() is { Length: > 0 } text ? text : Text("EmptyText"),
            JsonValue value when value.GetValueKind() == JsonValueKind.True => Text("Yes"),
            JsonValue value when value.GetValueKind() == JsonValueKind.False => Text("No"),
            JsonArray { Count: 0 } => Text("EmptyList"),
            JsonObject { Count: 0 } => Text("EmptyObject"),
            JsonArray array => string.Join(Environment.NewLine, array.Select(Value)),
            JsonObject map => string.Join(Environment.NewLine, map.Select(pair => pair.Key + ": " + Value(pair.Value))),
            _ => node.ToJsonString(),
        };
        string Optional(JsonNode node) => node is null ? Text("NotSpecified") : Value(node);
        string Token(JsonNode node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : string.Empty;
        string Shape(JsonNode node) => node?["properties"] is JsonObject properties
            ? properties.ContainsKey("files") ? "files" : properties.ContainsKey("text") ? "text" : "none" : "none";
        string Io(string shape) => Text(shape switch { "files" => "Files", "text" => "Text", _ => "None" });
        PowerScriptMetadataRow Row(string key, string label, string value) => new(key, Text(label), value);
        var sections = new List<PowerScriptMetadataSection>();
        var general = new List<PowerScriptMetadataRow>
        {
            Row("Name", "Name", Optional(descriptor["title"])),
            Row("Id", "Id", Optional(descriptor["name"])),
            Row("Description", "Description", Optional(descriptor["description"])),
        };
        var extension = descriptor["x-powerscript"] as JsonObject ?? new JsonObject();
        foreach (var key in new[] { "publisher", "version", "source", "icon" })
        {
            if (extension[key] is { } value)
            {
                var label = char.ToUpperInvariant(key[0]) + key[1..];
                general.Add(Row(label, label, Value(value)));
            }
        }

        sections.Add(new("General", Text("General"), general));
        var io = new List<PowerScriptMetadataRow>
        {
            Row("Input", "Input", Io(Shape(descriptor["inputSchema"]))),
            Row("Output", "Output", Io(Shape(descriptor["outputSchema"]))),
        };
        if (descriptor["inputSchema"]?["properties"]?["files"] is JsonObject files)
        {
            var extensions = files["items"]?["x-powerscript"]?["extensions"] as JsonArray;
            var acceptedExtensions = extensions?.Any(value => Token(value) == "*") == true
                ? Text("AllFiles") : extensions is { Count: > 0 } ? string.Join(", ", extensions.Select(Value)) : Text("NotSpecified");
            var maximumFiles = files["maxItems"] is null ? Text("NoLimit") : Value(files["maxItems"]);
            io.Add(Row("Extensions", "Extensions", acceptedExtensions));
            io.Add(Row("MinFiles", "MinFiles", files["minItems"] is null ? "0" : Value(files["minItems"])));
            io.Add(Row("MaxFiles", "MaxFiles", maximumFiles));
        }

        sections.Add(new("Io", Text("Io"), io));
        var capabilities = extension["capabilities"] as JsonArray;
        var capabilityNames = capabilities?.Select(value => Token(value) switch
        {
            "fileRead" => Text("FileRead"),
            "fileWrite" => Text("FileWrite"),
            "network" => Text("Network"),
            "ui" => Text("Ui"),
            _ => Value(value),
        });
        var consumersDisplay = extension["consumers"] is JsonArray { Count: > 0 } consumers
            ? string.Join(", ", consumers.Select(Value)) : Text("AllConsumers");
        var capabilitiesDisplay = capabilities is { Count: > 0 } ? string.Join(", ", capabilityNames) : Text("None");
        sections.Add(new("Discovery", Text("Discovery"), new[]
        {
            Row("Consumers", "Consumers", consumersDisplay),
            Row("Capabilities", "Capabilities", capabilitiesDisplay),
        }));

        foreach (var schemaName in new[] { "inputSchema", "outputSchema" })
        {
            var isOutput = schemaName == "outputSchema";
            var schema = descriptor[schemaName] as JsonObject;
            if (isOutput && schema is null)
            {
                continue;
            }

            var fields = isOutput ? schema : schema?["properties"]?["parameters"] as JsonObject;
            var properties = fields?["properties"] as JsonObject;
            var required = fields?["required"] as JsonArray;
            var index = 0;
            foreach (var (name, node) in properties ?? new JsonObject())
            {
                if (node is not JsonObject parameter)
                {
                    continue;
                }

                var key = (isOutput ? "Result" : "Parameter") + (index++).ToString(CultureInfo.InvariantCulture);
                var label = Token(parameter["title"]);
                var type = Token(parameter["type"]);
                var filePath = Token(parameter["format"]) == "file-path";
                var typeLabel = filePath ? Text("FilePath") : type switch
                {
                    "string" => Text("Text"),
                    "integer" => Text("Integer"),
                    "number" => Text("Number"),
                    "boolean" => Text("Boolean"),
                    "array" => Text("Array"),
                    "object" => Text("Object"),
                    _ => Optional(parameter["type"]),
                };
                var rows = new List<PowerScriptMetadataRow>
                {
                    Row(key + "_Name", isOutput ? "ResultFieldName" : "ParameterName", name),
                    Row(key + "_Type", "Type", typeLabel),
                    Row(key + "_Required", "Required", required?.Any(value => Token(value) == name) == true ? Text("Yes") : Text("No")),
                    Row(key + "_Default", "Default", parameter.ContainsKey("default") ? Value(parameter["default"]) : Text("NotSpecified")),
                };
                foreach (var (field, fieldLabel) in new[]
                {
                    ("description", "Description"), ("enum", "Options"), ("minimum", "Minimum"),
                    ("maximum", "Maximum"), ("format", "Format"),
                })
                {
                    if (parameter[field] is { } value)
                    {
                        rows.Add(Row(key + "_" + fieldLabel, fieldLabel, Value(value)));
                    }
                }

                sections.Add(new(key, Text(isOutput ? "ResultFieldTitle" : "ParameterTitle").Replace("{0}", string.IsNullOrWhiteSpace(label) ? name : label, StringComparison.Ordinal), rows));
            }

            if (index == 0)
            {
                var key = isOutput ? "ResultFields" : "Parameters";
                sections.Add(new(key, Text(key), new[] { Row(key, key, Text(isOutput ? "NoResultFields" : "NoParameters")) }));
            }
        }

        var execute = descriptor["x-execute"] as JsonObject;
        var execution = new List<PowerScriptMetadataRow>
        {
            Row("Runtime", "Runtime", entryExtension?.ToLowerInvariant() switch { ".ps1" => "PowerShell", ".py" => "Python", _ => Text("NotSpecified") }),
            Row("ExecutionMode", "ExecutionMode", Text(execute is null ? "BuiltIn" : "Custom")),
        };
        if (execute is not null)
        {
            foreach (var (field, label) in new[]
            {
                ("command", "Command"), ("argMap", "ArgumentMap"), ("exitCodes", "ExitCodes"),
            })
            {
                if (execute[field] is { } value)
                {
                    execution.Add(Row(label, label, Value(value)));
                }
            }

            execution.Add(Row("StandardInput", "StandardInput", Token(execute["stdin"]) == "json" ? Text("JsonInput") : Text("ArgumentInput")));
        }

        sections.Add(new("Execution", Text("Execution"), execution));
        if (descriptor["annotations"] is JsonObject annotations)
        {
            var rows = new List<PowerScriptMetadataRow>();
            foreach (var (field, label) in new[] { ("title", "Name"), ("readOnlyHint", "ReadOnlyHint"), ("destructiveHint", "DestructiveHint") })
            {
                if (annotations[field] is { } value)
                {
                    rows.Add(Row("Annotation_" + label, label, Value(value)));
                }
            }

            if (rows.Count > 0)
            {
                sections.Add(new("Annotations", Text("Annotations"), rows));
            }
        }

        return sections;
    }
}
