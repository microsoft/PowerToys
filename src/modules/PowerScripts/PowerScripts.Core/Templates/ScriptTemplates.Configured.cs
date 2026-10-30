// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Core.Templates;

public static partial class ScriptTemplates
{
    private static readonly HashSet<string> ReservedParameterNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "null", "args", "input", "PSItem", "this", "PSBoundParameters", "PSCmdlet",
        "ExecutionContext", "Host", "HOME", "PID", "PWD", "PSVersionTable", "PSScriptRoot", "PSCommandPath",
        "MyInvocation", "Error", "Matches", "foreach", "switch", "ShellId", "EnabledExperimentalFeatures",
        "IsWindows", "IsLinux", "IsMacOS", "IsCoreCLR", "PSCulture", "PSUICulture", "PSHOME",
        "NestedPromptLevel", "StackTrace", "ConsoleFileName", "PSDebugContext", "ErrorActionPreference",
    };

    public static CreatedScriptTemplate Create(string scriptsRoot, ScriptTemplateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Runtime is not ("powershell" or "python") ||
            options.Input is not ("none" or "text" or "files" or "image" or "audio") ||
            options.Output is not ("none" or "text" or "files" or "image" or "audio"))
        {
            throw new ArgumentException("Choose PowerShell or Python and an input and output type of none, text, files, image, or audio. Video uses files.", nameof(options));
        }

        if (options.Input == "files")
        {
            if (options.MinFiles < 1 || options.MaxFiles < 0 ||
                (options.MaxFiles > 0 && options.MaxFiles < options.MinFiles))
            {
                throw new ArgumentException("Minimum files must be at least 1. Maximum files must be 0 (unlimited) or at least the minimum.", nameof(options));
            }
        }
        else if (!string.IsNullOrWhiteSpace(options.FileExtensions) || options.MinFiles != 1 || options.MaxFiles != 0)
        {
            throw new ArgumentException("File filters and file counts apply only to file input.", nameof(options));
        }

        var entryName = options.Runtime == "python" ? "script.py" : "script.ps1";
        var capabilities = new JsonArray();
        if (options.Input == "files" || options.Parameters?.Any(parameter => parameter?.Schema?.IsFileParameter == true) == true)
        {
            capabilities.Add("fileRead");
        }

        if (options.Output == "files")
        {
            capabilities.Add("fileWrite");
        }

        var inputSchema = CreateSchema(options.Input, options);
        AddParameters(inputSchema, options.Parameters);
        var descriptor = new JsonObject
        {
            ["inputSchema"] = inputSchema,
            ["outputSchema"] = CreateSchema(options.Output),
            ["x-powerscript"] = new JsonObject { ["capabilities"] = capabilities },
        };
        return CreatePackage(scriptsRoot, options.ProjectName, options.Description ?? string.Empty,
            entryName, descriptor, options.Runtime == "python" ? CreatePython(options) : CreatePowerShell(options),
            CreateGuide(options, entryName));
    }

    private static void AddParameters(JsonObject inputSchema, IReadOnlyList<ScriptTemplateParameter>? parameters)
    {
        if (parameters is not { Count: > 0 })
        {
            return;
        }

        var properties = new JsonObject();
        var required = new JsonArray();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            if (parameter is null || string.IsNullOrEmpty(parameter.Name) ||
                !char.IsAsciiLetter(parameter.Name[0]) ||
                parameter.Name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') ||
                ReservedParameterNames.Contains(parameter.Name) || !names.Add(parameter.Name))
            {
                throw new ArgumentException("Parameter names must be unique (ignoring case), start with a letter, contain only letters, digits or underscores, and not use PowerShell automatic variable names.", nameof(parameters));
            }

            if (parameter.Schema is not { Type: "string" or "integer" or "boolean" } schema)
            {
                throw new ArgumentException($"Parameter '{parameter.Name}' must declare a string, integer, or boolean schema.", nameof(parameters));
            }

            var definition = JsonSerializer.SerializeToNode(schema, ToolDescriptorParser.JsonOptions)!.AsObject();
            if (schema.IsFileParameter)
            {
                if (parameter.FileExtensions is not null || schema.PowerScript is null)
                {
                    definition["x-powerscript"] = new JsonObject
                    {
                        ["extensions"] = JsonSerializer.SerializeToNode(ParseFileExtensions(parameter.FileExtensions)),
                    };
                }
            }
            else if (!string.IsNullOrWhiteSpace(parameter.FileExtensions))
            {
                throw new ArgumentException($"File extensions apply only to file-path parameters ('{parameter.Name}').", nameof(parameters));
            }

            properties.Add(parameter.Name, definition);
            if (parameter.Required)
            {
                required.Add(parameter.Name);
            }
        }

        inputSchema["properties"]!["parameters"] = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = properties,
            ["required"] = required,
        };
    }

    private static JsonObject CreateSchema(string shape, ScriptTemplateOptions? options = null)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        if (shape == "text")
        {
            properties["text"] = new JsonObject { ["type"] = "string" };
            required.Add("text");
        }
        else if (shape == "files")
        {
            var items = new JsonObject { ["type"] = "string" };
            var files = new JsonObject { ["type"] = "array", ["items"] = items };
            if (options is not null)
            {
                items["x-powerscript"] = new JsonObject
                {
                    ["extensions"] = JsonSerializer.SerializeToNode(ParseFileExtensions(options.FileExtensions)),
                };
                files["minItems"] = options.MinFiles;
                if (options.MaxFiles > 0)
                {
                    files["maxItems"] = options.MaxFiles;
                }
            }

            properties["files"] = files;
            required.Add("files");
        }
        else if (shape is "image" or "audio")
        {
            properties[shape] = new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["mimeType"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = JsonSerializer.SerializeToNode(shape == "image" ? ScriptMedia.ImageMimeTypes : ScriptMedia.AudioMimeTypes),
                    },
                    ["data"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["contentEncoding"] = "base64",
                        ["minLength"] = 1,
                        ["maxLength"] = ScriptMedia.MaxEncodedLength,
                    },
                },
                ["required"] = new JsonArray("mimeType", "data"),
            };
            required.Add(shape);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }

    private static string CreatePowerShell(ScriptTemplateOptions options)
    {
        var bindings = new List<string>();
        var primary = options.Input switch
        {
            "text" => "[string]$Text = ''",
            "files" => "[string[]]$Files",
            "image" => "[object]$Image",
            "audio" => "[object]$Audio",
            _ => null,
        };
        if (primary is not null)
        {
            bindings.Add(primary);
        }

        foreach (var parameter in options.Parameters ?? Array.Empty<ScriptTemplateParameter>())
        {
            var type = parameter.Schema.Type switch { "integer" => "int", "boolean" => "bool", _ => "string" };
            bindings.Add($"[{type}]${parameter.Name}");
        }

        var declaration = bindings.Count == 0 ? string.Empty : "param(" + string.Join(",\n    ", bindings) + ")\n";
        var input = options.Input switch
        {
            "text" => "$__powerscriptContent = $Text",
            "files" => """
                if ($env:POWERSCRIPTS_FILES) {
                    $Files = @($env:POWERSCRIPTS_FILES -split "`n" | Where-Object { $_ })
                }
                $__powerscriptContent = ($Files | ForEach-Object {
                    $__powerscriptFile = Get-Item -LiteralPath $_ -ErrorAction Stop
                    '{0} ({1} bytes)' -f $__powerscriptFile.Name, $__powerscriptFile.Length
                }) -join [Environment]::NewLine
                """,
            "image" or "audio" => $$"""
                $__powerscriptMedia = ${{(options.Input == "image" ? "Image" : "Audio")}}
                $__powerscriptContent = (@($__powerscriptMedia) | ForEach-Object {
                    '{0} ({1} bytes)' -f $_.mimeType, ([Convert]::FromBase64String($_.data)).Length
                }) -join [Environment]::NewLine
                """,
            _ => "$__powerscriptContent = 'Hello from your new PowerScript.'",
        };
        var output = options.Output switch
        {
            "text" => "Write-Output $__powerscriptContent",
            "files" => """
                $__powerscriptPath = Join-Path $env:TEMP ('powerscript-' + [Guid]::NewGuid().ToString('N') + '.txt')
                Set-Content -LiteralPath $__powerscriptPath -Value $__powerscriptContent -Encoding UTF8
                @{ files = @($__powerscriptPath) } | ConvertTo-Json -Compress
                """,
            "image" or "audio" => $$"""
                # Replace this example with your media processing; matching media input is passed through.
                $__powerscriptMedia = {{(options.Input == options.Output ? "$" + (options.Input == "image" ? "Image" : "Audio") : "ConvertFrom-Json '" + SampleMedia(options.Output).ToJsonString() + "'")}}
                @{ {{options.Output}} = $__powerscriptMedia } | ConvertTo-Json -Depth 8 -Compress
                """,
            _ => "# Add work here; send diagnostics to stderr.",
        };
        return $"""
            # Edit the sibling script.ps1.tool.json to change the contract; see README.md.
            {declaration}
            [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
            $ErrorActionPreference = 'Stop'
            {input}

            {output}

            """;
    }

    private static string CreatePython(ScriptTemplateOptions options)
    {
        var input = options.Input switch
        {
            "text" => "content = request['text']",
            "files" => "content = '\\n'.join(f'{Path(path).name} ({Path(path).stat().st_size} bytes)' for path in request['files'])",
            "image" or "audio" => $$"""
                media = request['{{options.Input}}']
                items = media if isinstance(media, list) else [media]
                content = '\n'.join(f"{item['mimeType']} ({len(base64.b64decode(item['data'], validate=True))} bytes)" for item in items)
                """,
            _ => "content = 'Hello from your new PowerScript.'",
        };
        var output = options.Output switch
        {
            "text" => "print(content)",
            "files" => """
                with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', encoding='utf-8', delete=False) as output:
                    output.write(content)
                print(json.dumps({'files': [output.name]}))
                """,
            "image" or "audio" => $$"""
                # Replace this example with your media processing; matching media input is passed through.
                media = {{(options.Input == options.Output ? "request['" + options.Input + "']" : SampleMedia(options.Output).ToJsonString())}}
                print(json.dumps({'{{options.Output}}': media}))
                """,
            _ => "# Add work here; send diagnostics to stderr with print(..., file=sys.stderr).",
        };
        return $"""
            # Read the schema-validated request; no entry function is required.
            import json
            import base64
            import sys
            from pathlib import Path
            import tempfile

            request = json.load(sys.stdin)
            {(options.Parameters is { Count: > 0 } ? "parameters = request['parameters']" : string.Empty)}
            {input}

            {output}

            """;
    }

    private static JsonObject SampleMedia(string category)
    {
        if (category == "image")
        {
            return new JsonObject
            {
                ["mimeType"] = "image/png",
                ["data"] = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=",
            };
        }

        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + 1600);
            writer.Write("WAVEfmt "u8);
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(8000);
            writer.Write(16000);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(1600);
            writer.Write(new byte[1600]);
        }

        return new JsonObject { ["mimeType"] = "audio/wav", ["data"] = Convert.ToBase64String(buffer.ToArray()) };
    }

    private static string CreateGuide(ScriptTemplateOptions options, string entryName) => $$"""
        # Your PowerScript

        Edit `{{entryName}}` and `{{entryName}}.tool.json`, then refresh the catalogue.
        Selected contract: **{{options.Runtime}}, {{options.Input}} -> {{options.Output}}**.
        Creation neither runs code nor approves trust.

        ## Schemas

        Both `inputSchema` and `outputSchema` are required closed JSON Schema objects.
        Declare one required primary field: `text`, `files`, `image`, or `audio`, or neither for no primary data.
        Add named options in `inputSchema.properties.parameters`, an object with its own `properties`,
        `required` list and `additionalProperties: false`. Supported options are string, integer and boolean.
        String options support `enum`, `minLength`, `maxLength` and `pattern`; integers support
        `minimum` and `maximum`. Defaults are applied by PowerScripts. `format: "file-path"` enables a picker.
        Parameters selected during creation are already declared in the descriptor and PowerShell `param(...)`.
        In Python, read them from `parameters` using their exact names, for example `parameters.get("count")`.
        Parameter names start with a letter, contain only letters, digits or underscores, and are unique
        ignoring case. PowerShell automatic variable names and the active primary input name are reserved.
        Defaults and enum values remain data in the descriptor, never executable script text.
        Use `minItems` / `maxItems` on a files array and `x-powerscript.extensions` on its string `items`.
        Omit `maxItems` for no limit. Restrictions are enforced on invocation and output, not just discovery.
        Unsupported schema keywords are rejected rather than silently ignored.

        ## Script arguments and results

        Runtime is inferred from `.ps1` or `.py`; there is no entry function setting.
        PowerShell uses native `param(...)`: `$Text` or `$Files` for primary input, plus named options.
        `POWERSCRIPTS_FILES` contains the complete newline-separated selection.
        Python executes top-level statements and reads the complete request with `json.load(sys.stdin)`.
        Use `request["files"]`, `request["text"]` and `request["parameters"]` as declared.
        Parameters retain JSON boolean and integer types; Python does not receive keyword arguments.
        Text output is literal stdout. File output is a JSON object `{"files": ["absolute path"]}`.
        No-output scripts emit no result; send diagnostics to stderr.
        Image/audio values contain `mimeType` and base64 `data`, never a file path.
        Python reads `request["image"]` / `request["audio"]`; PowerShell receives `$Image` / `$Audio`.
        Both directions support PNG, JPEG, static WebP, WAV, MP3 and FLAC according to the MIME enum.
        There is no implicit conversion, streaming, or codec installation. Matching media templates
        pass input through; other media-output templates create a one-pixel PNG or 0.1-second silent WAV.
        Replace those examples with your own processing.
        For collections, wrap the media object schema in `{"type":"array","minItems":1,"items":...}`,
        optionally set `maxItems`, and return an array even for one item.
        Limits: 64 items, 16 MiB decoded per item, 32 MiB decoded total per request/result.
        Host checks base64, MIME declarations and format headers; scripts still handle codec errors.
        Animated images, SVG, multipage images, video, and large assets use file contracts.
        For video, select files with extensions such as `.mp4, .webm, .mov, .mkv`.
        HTML markup uses text; HTML documents use files.

        ## Metadata and isolation

        `name`, `title`, `description`, `annotations`, `inputSchema` and `outputSchema` describe the tool.
        Optional `capabilities`, `publisher`, `version`, `source` and `icon` belong
        under root `x-powerscript`. They do not grant permissions.
        All scripts use the global MXC settings; there are no per-script settings or policy overrides.
        Python environment selection is also global. Managed Linux requires globally acknowledged
        UI and least-privilege exceptions because those Windows policies cannot be enforced there.
        Write output only to the temporary workspace, never the source package.
        Source or metadata edits invalidate trust and require renewed consent.
        Advanced custom launch recipes use `x-execute`.

        """;
}
