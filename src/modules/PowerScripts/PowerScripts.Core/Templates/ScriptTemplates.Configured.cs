// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace PowerScripts.Core.Templates;

public static partial class ScriptTemplates
{
    public static CreatedScriptTemplate Create(string scriptsRoot, ScriptTemplateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Runtime is not ("powershell" or "python") ||
            options.Input is not ("none" or "text" or "files") ||
            options.Output is not ("none" or "text" or "files"))
        {
            throw new ArgumentException("Choose PowerShell or Python and an input and output type of none, text or files.", nameof(options));
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
        if (options.Input == "files")
        {
            capabilities.Add("fileRead");
        }

        if (options.Output == "files")
        {
            capabilities.Add("fileWrite");
        }

        var descriptor = new JsonObject
        {
            ["inputSchema"] = CreateSchema(options.Input, options),
            ["outputSchema"] = CreateSchema(options.Output),
            ["x-powerscript"] = new JsonObject { ["capabilities"] = capabilities },
        };
        return CreatePackage(scriptsRoot, options.ProjectName, options.Description ?? string.Empty,
            entryName, descriptor, options.Runtime == "python" ? CreatePython(options) : CreatePowerShell(options),
            CreateGuide(options, entryName));
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
        var parameter = options.Input switch
        {
            "text" => "param([string]$Text = '')\n",
            "files" => "param([string[]]$Files)\n",
            _ => string.Empty,
        };
        var input = options.Input switch
        {
            "text" => "$content = $Text",
            "files" => """
                if ($env:POWERSCRIPTS_FILES) {
                    $Files = @($env:POWERSCRIPTS_FILES -split "`n" | Where-Object { $_ })
                }
                $content = ($Files | ForEach-Object {
                    $file = Get-Item -LiteralPath $_ -ErrorAction Stop
                    '{0} ({1} bytes)' -f $file.Name, $file.Length
                }) -join [Environment]::NewLine
                """,
            _ => "$content = 'Hello from your new PowerScript.'",
        };
        var output = options.Output switch
        {
            "text" => "Write-Output $content",
            "files" => """
                $path = Join-Path $env:TEMP ('powerscript-' + [Guid]::NewGuid().ToString('N') + '.txt')
                Set-Content -LiteralPath $path -Value $content -Encoding UTF8
                @{ files = @($path) } | ConvertTo-Json -Compress
                """,
            _ => "# Add work here; send diagnostics to stderr.",
        };
        return $"""
            # Edit the sibling script.ps1.tool.json to change the contract; see README.md.
            {parameter}
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
            _ => "# Add work here; send diagnostics to stderr with print(..., file=sys.stderr).",
        };
        return $"""
            # Read the schema-validated request; no entry function is required.
            import json
            import sys
            from pathlib import Path
            import tempfile

            request = json.load(sys.stdin)
            {input}

            {output}

            """;
    }

    private static string CreateGuide(ScriptTemplateOptions options, string entryName) => $$"""
        # Your PowerScript

        Edit `{{entryName}}` and `{{entryName}}.tool.json`, then refresh the catalogue.
        Selected contract: **{{options.Runtime}}, {{options.Input}} -> {{options.Output}}**.
        Creation neither runs code nor approves trust.

        ## Schemas

        Both `inputSchema` and `outputSchema` are required closed JSON Schema objects.
        Declare one required `text` string or `files` array of path strings, or neither for no primary data.
        Add named options in `inputSchema.properties.parameters`, an object with its own `properties`,
        `required` list and `additionalProperties: false`. Supported options are string, integer and boolean.
        String options support `enum`, `minLength`, `maxLength` and `pattern`; integers support
        `minimum` and `maximum`. Defaults are applied by PowerScripts. `format: "file-path"` enables a picker.
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
        Rich HTML and media use files, not text.

        ## Metadata and isolation

        `name`, `title`, `description`, `annotations`, `inputSchema` and `outputSchema` describe the tool.
        Optional `capabilities`, `consumers`, `publisher`, `version`, `source` and `icon` belong
        under root `x-powerscript`. They do not grant permissions.
        All scripts use the global MXC settings; there are no per-script settings or policy overrides.
        Python environment selection is also global. Managed Linux requires globally acknowledged
        UI and least-privilege exceptions because those Windows policies cannot be enforced there.
        Write output only to the temporary workspace, never the source package.
        Source or metadata edits invalidate trust and require renewed consent.
        Advanced custom launch recipes use `x-execute`.

        """;
}
