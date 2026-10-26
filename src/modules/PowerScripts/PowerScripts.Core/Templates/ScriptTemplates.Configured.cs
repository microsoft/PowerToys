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

        var extension = new JsonObject
        {
            ["runtime"] = options.Runtime,
            ["input"] = options.Input,
            ["output"] = options.Output,
            ["capabilities"] = capabilities,
        };
        if (options.Runtime == "python")
        {
            extension["function"] = "main";
        }

        if (options.Input == "files")
        {
            extension["extensions"] = JsonSerializer.SerializeToNode(ParseFileExtensions(options.FileExtensions));
            extension["minFiles"] = options.MinFiles;
            extension["maxFiles"] = options.MaxFiles;
        }

        var descriptor = new JsonObject
        {
            ["inputSchema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject(),
                ["required"] = new JsonArray(),
            },
            ["x-powerscript"] = extension,
        };
        return CreatePackage(scriptsRoot, options.ProjectName, options.Description ?? string.Empty,
            entryName, descriptor, options.Runtime == "python" ? CreatePython(options) : CreatePowerShell(options),
            CreateGuide(options, entryName));
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
                if (-not $Files) { throw 'Select at least one file.' }
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
                @{ file_paths = @($path) } | ConvertTo-Json -Compress
                """,
            _ => "# Add your work here. The none-output contract must not write to stdout.",
        };
        return $"""
            # Edit the sibling script.ps1.tool.json to change the contract; see README.md.
            {parameter}
            [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
            $ErrorActionPreference = 'Stop'
            {input}

            # Replace this example with your own implementation.
            {output}

            """;
    }

    private static string CreatePython(ScriptTemplateOptions options)
    {
        var parameter = options.Input switch
        {
            "text" => "text: str",
            "files" => "file_paths: list[str]",
            _ => string.Empty,
        };
        var input = options.Input switch
        {
            "text" => "content = text",
            "files" => "content = \"\\n\".join(f\"{Path(path).name} ({Path(path).stat().st_size} bytes)\" for path in file_paths)",
            _ => "content = \"Hello from your new PowerScript.\"",
        };
        var output = options.Output switch
        {
            "text" => "    return content\n",
            "files" => """
                    with tempfile.NamedTemporaryFile(mode="w", suffix=".txt", encoding="utf-8", delete=False) as output:
                        output.write(content)
                    return [output.name]

                """,
            _ => "    return None\n",
        };
        return "# Edit script.py.tool.json to change the contract; see README.md.\n" +
            (options.Input == "files" ? "from pathlib import Path\n" : string.Empty) +
            (options.Output == "files" ? "import tempfile\n" : string.Empty) +
            $"\n\ndef main({parameter}):\n    {input}\n    # Replace this example with your own implementation.\n{output}";
    }

    private static string CreateGuide(ScriptTemplateOptions options, string entryName) => $"""
        # Your PowerScript

        Edit `{entryName}` and `{entryName}.tool.json`, then refresh the catalogue.
        Selected contract: **{options.Runtime}, {options.Input} -> {options.Output}**.
        Creation does not run code, approve trust, write local policy or relax MXC restrictions.

        ## Descriptor fields

        - `name`: generated unique action ID; keep it stable when sharing the script.
        - `title` and `description`: the project name and explanation shown by consumers.
        - `inputSchema`: named parameters, separate from primary input. Add properties and required
          names here, and matching PowerShell parameters or Python keyword parameters in the script.
          Use `type`, `title`, `description`, `default`, `enum`, `minimum`, `maximum`, and
          `format: "file-path"` as needed. An empty properties object means no extra prompts.
        - `x-powerscript.runtime`: PowerShell or Python. Python must be enabled in module settings.
        - `x-powerscript.function`: `main` for the Python entry point; omitted for PowerShell.
        - `x-powerscript.input`: `none`, `text` or `files`. PowerShell receives text as `$Text` and
          files as `$Files` / `POWERSCRIPTS_FILES`; Python receives a string or list of paths.
        - `x-powerscript.output`: `none`, `text` or `files`. PowerShell emits nothing, literal text,
          or a JSON object with `file_paths`. Python returns None, a string, or a list of paths.
          Rich HTML and media use files, not text.
        - `extensions`, `minFiles`, `maxFiles`: only present for file input. `*` accepts any type;
          maximum 0 means unlimited. These are discovery/selection rules, not access grants.
        - `capabilities`: fileRead for selected-file inspection and fileWrite for file output.
          They describe intent, not permission grants. Update them when changing the implementation.

        ## Execution and policy

        The starter reads only supplied files and writes output only to the temporary workspace.
        All default MXC restrictions stay enabled; review code and trust before running from a consumer.
        Per-script local settings are only for MXC policy, never metadata overrides.
        Do not write output into this source package. Files returned from an isolated run are exported
        for the consuming module, which decides how to display, save or copy them.
        Metadata and source changes require trust review again.
        Optional `consumers`, `publisher`, `version`, `source`, `icon` and `mxc.recommendedPolicies`
        belong under `x-powerscript`. A consumer list narrows discovery, not authorization.
        Advanced custom launch recipes use `x-execute`; the starter uses the built-in runtime.

        """;
}
