// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Templates;

public static partial class PowerShellCliTemplates
{
    private const string ActionOutput = """
        if (-not [string]::IsNullOrWhiteSpace($__powerscriptOutput)) { [Console]::Error.WriteLine($__powerscriptOutput) }
        """;

    private const string WorkspaceDirectory = """
        $__powerscriptWorkspace = $env:POWERSCRIPTS_MXC_WORKSPACE
        if (-not $__powerscriptWorkspace) { $__powerscriptWorkspace = [IO.Path]::GetTempPath() }
        $__powerscriptResultDirectory = Join-Path $__powerscriptWorkspace ('powerscript-transform-' + [Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($__powerscriptResultDirectory) | Out-Null
        """;

    private const string FileOutput = """
        if (-not [string]::IsNullOrWhiteSpace($__powerscriptOutput)) { [Console]::Error.WriteLine($__powerscriptOutput) }
        if (-not [IO.File]::Exists($__powerscriptResultFile)) { throw 'The CLI did not produce the requested output file.' }
        [Console]::Write((@{ files = @($__powerscriptResultFile) } | ConvertTo-Json -Compress))
        """;

    private const string FileLocksmithArguments = """
        if (-not @($Files).Count) { throw 'Choose at least one file or directory.' }
        if ($Wait) {
            $__powerscriptArguments.AddRange([string[]]@('--wait', '--timeout', $TimeoutMilliseconds.ToString([Globalization.CultureInfo]::InvariantCulture)))
        } elseif ($Json) { $__powerscriptArguments.Add('--json') }
        foreach ($__powerscriptFile in $Files) {
            $__powerscriptPath = [IO.Path]::GetFullPath($__powerscriptFile)
            if (-not [IO.File]::Exists($__powerscriptPath) -and -not [IO.Directory]::Exists($__powerscriptPath)) { throw 'Choose existing files or directories.' }
            $__powerscriptArguments.Add($__powerscriptPath)
        }
        """;

    private const string DisplayTargets = """
        if ($MonitorId) { $__powerscriptArguments.AddRange([string[]]@('--monitor-id', $MonitorId)) }
        elseif ($MonitorNumbers.Trim()) {
            $__powerscriptNumbers = @(
                foreach ($__powerscriptPart in ($MonitorNumbers -split ',')) {
                    $__powerscriptNumber = 0
                    if (-not [int]::TryParse($__powerscriptPart.Trim(), [Globalization.NumberStyles]::Integer, [Globalization.CultureInfo]::InvariantCulture, [ref]$__powerscriptNumber) -or
                        $__powerscriptNumber -lt 1) { throw 'MonitorNumbers requires comma-separated positive monitor indices.' }
                    $__powerscriptNumber.ToString([Globalization.CultureInfo]::InvariantCulture)
                }
            )
            $__powerscriptArguments.AddRange([string[]]@('--monitor-number', ($__powerscriptNumbers -join ',')))
        }
        """;

    private static IReadOnlyList<PowerShellCliTemplate> CreateScenarioCatalogue() =>
    [
        Legacy("settings-query"),
        AsAction(Legacy("settings-module-state") with { Id = "settings-module-action" }),
        Legacy("advanced-paste-local"),
        Legacy("advanced-paste-list"),
        Template("advanced-paste-file-text", "Advanced Paste: convert a file to text or extract OCR",
            "Read one text/HTML/image file and return plain text, Markdown, JSON text or Windows OCR text. No AI provider or clipboard access.", "files",
            @"WinUI3Apps\PowerToys.AdvancedPaste.CLI.exe",
            """
            if (@($Files).Count -ne 1) { throw 'Choose exactly one input file.' }
            $__powerscriptArguments.AddRange([string[]]@('transform', '--action', $Action, '--input', $Files[0], '--stdout'))
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """,
            Text("Action", "Text-producing transformation; input must support the chosen action.", "plain-text", "plain-text", "markdown", "json", "image-to-text"),
            Flag("Json", "Return the CLI's structured report as text.")),
        AdvancedPasteFileResult(),
        AdvancedPasteTextFile(),
        AdvancedPasteHtml(files: false),
        AdvancedPasteHtml(files: true),
        AdvancedPasteAi(input: "text", files: false),
        AdvancedPasteAi(input: "files", files: false),
        AdvancedPasteAi(input: "text", files: true),
        AdvancedPasteAi(input: "files", files: true),
        FileLocksmith(wait: false, paths: false),
        FileLocksmith(wait: true, paths: false),
        FileLocksmith(wait: false, paths: true),
        FileLocksmith(wait: true, paths: true),
        Variant("fancyzones", "fancyzones-query", "FancyZones: query layouts, monitors or hotkeys",
            "Read layout/monitor/hotkey information. Requires the running host module and any saved layout data.", "text", string.Empty,
            Text("Command", "Read-only query.", "get-layouts", "get-layouts", "get-monitors", "get-active-layout", "get-hotkeys")),
        Variant("fancyzones", "fancyzones-layout", "FancyZones: apply a layout",
            "Apply a saved layout by name or UUID to one or all monitors. Changes desktop layout settings.", "none", "$Command = 'set-layout'",
            Text("Layout", "Required saved layout name or UUID."),
            Integer("Monitor", "One-based monitor index, ignored when AllMonitors is enabled.", 1, 1),
            Flag("AllMonitors", "Apply to every monitor.")),
        Variant("fancyzones", "fancyzones-hotkey", "FancyZones: assign or remove a layout hotkey",
            "Change saved layout hotkeys; not a report or automatic replay.", "none", string.Empty,
            Text("Command", "Assign a layout UUID or remove the selected digit.", "set-hotkey", "set-hotkey", "remove-hotkey"),
            Text("Layout", "Layout UUID required by set-hotkey; ignored by remove-hotkey."),
            Integer("Hotkey", "Hotkey digit.", 1, 0, 9)),
        Variant("fancyzones", "fancyzones-open", "FancyZones: open editor or settings",
            "Launch the requested UI on the host desktop.", "none", string.Empty,
            Text("Command", "UI to open.", "open-editor", "open-editor", "open-settings")),
        Variant("workspaces", "workspaces-query", "Workspaces: list saved workspaces",
            "Read workspace summaries or application details, optionally filtered by ID or name.", "text", "$Command = 'list'",
            Text("WorkspaceId", "Optional GUID; mutually exclusive with WorkspaceName."),
            Text("WorkspaceName", "Optional name; mutually exclusive with WorkspaceId."),
            Flag("Details", "Include application details."),
            Flag("Json", "Return structured JSON text.", true)),
        Variant("workspaces", "workspaces-launch", "Workspaces: launch a saved workspace",
            "Launch applications and arrange windows. Choose ID or name; execution can show prompts and is not replay-safe.", "none", "$Command = 'launch'",
            Text("WorkspaceId", "GUID; specify this or WorkspaceName."),
            Text("WorkspaceName", "Name; specify this or WorkspaceId."),
            Integer("TimeoutSeconds", "Native launch timeout; the Host execution budget still applies.", 120, 1, 600)),
        Template("image-resizer-config", "Image Resizer: inspect saved configuration",
            "Read saved presets and resize/encoding defaults; does not resize or replace files.", "none",
            @"WinUI3Apps\PowerToys.ImageResizerCLI.exe", "$__powerscriptArguments.Add('--show-config')"),
        ImageResizerOptions(),
        AsAction(Legacy("awake") with { Id = "awake-action" }),
        Variant("light-switch", "light-switch-status", "Light Switch: query theme and schedule",
            "Read the current theme/schedule from the running host module.", "text", "$Command = 'status'",
            Flag("Json", "Return structured JSON text.", true)),
        Variant("light-switch", "light-switch-theme", "Light Switch: change the theme",
            "Select light/dark or toggle the host desktop theme.", "none", string.Empty,
            Text("Command", "Theme action.", "toggle", "light", "dark", "toggle")),
        Variant("light-switch", "light-switch-schedule", "Light Switch: enable or disable scheduling",
            "Change saved theme scheduling. Fixed hours/location remain configured in module Settings.", "none", string.Empty,
            Text("Command", "Schedule action.", "schedule-enable", "schedule-enable", "schedule-disable"),
            Text("ScheduleMode", "Used by schedule-enable.", "fixed-hours", "fixed-hours", "sunset-to-sunrise", "follow-night-light")),
        Variant("power-display-query", "power-display-inspect", "Power Display: query monitors, capabilities or profiles",
            "Read connected monitors, setting values, capabilities or saved profiles. Get/capabilities target at most one monitor.", "text",
            string.Empty, [.. Legacy("power-display-query").Parameters, Flag("Json", "Return structured JSON Lines text.", true)])
            with { Arguments = Legacy("power-display-query").Arguments + "\nif ($Json) { $__powerscriptArguments.Add('--json') }" },
        PowerDisplayAdjust(),
        PowerDisplayDiscrete(),
        Template("power-display-profile", "Power Display: apply a saved profile",
            "Apply a saved profile to connected monitors. Review its settings first: applying it can change the desktop/display. Requires the running host module.", "none",
            @"WinUI3Apps\PowerToys.PowerDisplay.Cli.exe",
            """
            $__powerscriptArguments.AddRange([string[]]@('apply-profile', $ProfileId.ToString([Globalization.CultureInfo]::InvariantCulture)))
            """,
            Integer("ProfileId", "Positive numeric profile ID from the profiles query, not a profile name or GUID.", 1, 1))
            with { Output = "none", OutputScript = ActionOutput },
    ];

    private static PowerShellCliTemplate Legacy(string id) => LegacyDefinitions.Single(template => template.Id == id);

    private static PowerShellCliTemplate AsAction(PowerShellCliTemplate template) => template with { Output = "none", OutputScript = ActionOutput };

    private static PowerShellCliTemplate Variant(string source, string id, string name, string description, string output, string prefix, params ScriptTemplateParameter[] parameters)
    {
        var template = Legacy(source) with
        {
            Id = id,
            Name = name,
            Description = description,
            Output = output,
            Parameters = parameters,
            Arguments = prefix + "\n" + Legacy(source).Arguments,
        };
        return output == "none" ? AsAction(template) : template;
    }

    private static PowerShellCliTemplate AdvancedPasteFileResult() =>
        Template("advanced-paste-file-result", "Advanced Paste: create an image, document or media file",
            "Transform one supported input file into TXT, PNG, JPG, HTML, MP3 or MP4. Return a new workspace file; the consumer owns saving it. No clipboard or host destination.", "files",
            @"WinUI3Apps\PowerToys.AdvancedPaste.CLI.exe",
            """
            if (@($Files).Count -ne 1) { throw 'Choose exactly one input file.' }
            """ + "\n" + WorkspaceDirectory + """

            $__powerscriptExtension = switch ($Action) {
                'paste-as-txt-file' { 'txt' }
                'paste-as-png-file' { 'png' }
                'paste-as-jpg-file' { 'jpg' }
                'paste-as-html-file' { 'html' }
                'transcode-to-mp3' { 'mp3' }
                'transcode-to-mp4' { 'mp4' }
            }
            $__powerscriptResultFile = Join-Path $__powerscriptResultDirectory ('result.' + $__powerscriptExtension)
            $__powerscriptArguments.AddRange([string[]]@('transform', '--action', $Action, '--input', $Files[0], '--output', $__powerscriptResultFile))
            """,
            Text("Action", "Input must support the chosen transformation.", "paste-as-txt-file",
                "paste-as-txt-file", "paste-as-png-file", "paste-as-jpg-file", "paste-as-html-file", "transcode-to-mp3", "transcode-to-mp4"))
            with { Output = "files", OutputScript = FileOutput };

    private static PowerShellCliTemplate AdvancedPasteTextFile() =>
        Template("advanced-paste-text-file", "Advanced Paste: save text as a new TXT file",
            "Convert supplied text into a returned workspace TXT file; does not overwrite a host destination or read the clipboard.", "text",
            @"WinUI3Apps\PowerToys.AdvancedPaste.CLI.exe",
            WorkspaceDirectory + """

            $__powerscriptResultFile = Join-Path $__powerscriptResultDirectory 'result.txt'
            $__powerscriptArguments.AddRange([string[]]@('transform', '--action', 'paste-as-txt-file', '--stdin', '--output', $__powerscriptResultFile))
            """)
            with { Output = "files", StandardInput = "$Content", OutputScript = FileOutput };

    private static PowerShellCliTemplate AdvancedPasteHtml(bool files) =>
        Template(files ? "advanced-paste-html-file" : "advanced-paste-html-text",
            files ? "Advanced Paste: save HTML as a new HTML file" : "Advanced Paste: convert supplied HTML to text",
            "Stage supplied HTML as an explicit UTF-8 .html input so the full CLI sees HTML, not only stdin plain text. New files stay in the workspace.", "text",
            @"WinUI3Apps\PowerToys.AdvancedPaste.CLI.exe",
            WorkspaceDirectory + """

            $__powerscriptHtmlInput = Join-Path $__powerscriptResultDirectory 'input.html'
            [IO.File]::WriteAllText($__powerscriptHtmlInput, $Content, [Text.UTF8Encoding]::new($false))
            """ + (files ? """

            $__powerscriptResultFile = Join-Path $__powerscriptResultDirectory 'result.html'
            $__powerscriptArguments.AddRange([string[]]@('transform', '--action', 'paste-as-html-file', '--input', $__powerscriptHtmlInput, '--output', $__powerscriptResultFile))
            """ : """

            $__powerscriptArguments.AddRange([string[]]@('transform', '--action', $Action, '--input', $__powerscriptHtmlInput, '--stdout'))
            """),
            files ? [] : [Text("Action", "Text-producing HTML transformation.", "markdown", "markdown", "plain-text", "json")])
            with { Output = files ? "files" : "text", OutputScript = files ? FileOutput : null };

    private static PowerShellCliTemplate AdvancedPasteAi(string input, bool files) =>
        Template($"advanced-paste-ai-{input}-{(files ? "file" : "text")}", $"Advanced Paste: AI {input} to {(files ? "a file" : "text")}",
            "Run spelling/grammar, a prompt or a saved custom action with the configured provider. Content may leave the machine and incur costs. Text output rejects file-only results; file output saves a single result to the workspace.", input,
            @"WinUI3Apps\PowerToys.AdvancedPaste.CLI.exe",
            """
            $__powerscriptArguments.Add('transform')
            if ($CustomAction) { $__powerscriptArguments.AddRange([string[]]@('--custom-action', $CustomAction)) }
            else {
                $__powerscriptArguments.AddRange([string[]]@('--action', $Action))
                if ($Action -eq 'paste-with-ai' -and -not $Prompt.Trim()) { throw 'AI paste requires Prompt.' }
            }
            if ($Prompt) { $__powerscriptArguments.AddRange([string[]]@('--prompt', $Prompt)) }
            if ($Provider) { $__powerscriptArguments.AddRange([string[]]@('--provider', $Provider)) }
            """ + (input == "files" ? """

            if (@($Files).Count -ne 1) { throw 'Choose exactly one input file.' }
            $__powerscriptArguments.AddRange([string[]]@('--input', $Files[0]))
            """ : "\n$__powerscriptArguments.Add('--stdin')") +
            (files ? "\n" + WorkspaceDirectory + """

            if (-not $FileName.Trim() -or $FileName.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $FileName -in @('.', '..')) { throw 'FileName must be a single valid filename, not a path.' }
            $__powerscriptResultFile = Join-Path $__powerscriptResultDirectory $FileName
            $__powerscriptArguments.AddRange([string[]]@('--output', $__powerscriptResultFile))
            """ : "\n$__powerscriptArguments.Add('--stdout')"),
            [
                Text("Action", "Ignored when CustomAction is specified.", "fix-spelling-and-grammar", "fix-spelling-and-grammar", "paste-with-ai"),
                Text("CustomAction", "Optional saved action ID or name from actions list. Overrides Action."),
                Text("Prompt", "Required by paste-with-ai unless a saved custom action is selected."),
                Text("Provider", "Optional configured provider ID; no API key is stored in this script."),
                .. files ? new[] { Text("FileName", "Single output filename with the expected extension for the configured action.", "result.txt") } : [],
            ])
            with { Output = files ? "files" : "text", StandardInput = input == "text" ? "$Content" : null, OutputScript = files ? FileOutput : null };

    private static PowerShellCliTemplate FileLocksmith(bool wait, bool paths) =>
        Template("file-locksmith-" + (paths ? "paths-" : string.Empty) + (wait ? "wait" : "query"),
            "File Locksmith: " + (wait ? "wait for paths to unlock" : "find processes holding paths") + (paths ? " (path text)" : " (selected files)"),
            paths
                ? "Supply one absolute file/directory path per line. Directory queries are recursive. Never terminates processes; waiting is bounded by both native timeout and Host budget."
                : "Inspect selected-file batches or wait for them to unlock. Never terminates processes; waiting is bounded by both native timeout and Host budget.",
            paths ? "text" : "files", "FileLocksmithCLI.exe",
            (paths ? """
                $Files = @($Content -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
                foreach ($__powerscriptPath in $Files) {
                    if (-not [IO.Path]::IsPathFullyQualified($__powerscriptPath)) { throw 'Supply absolute file or directory paths.' }
                }

                """ : string.Empty) +
                (wait ? "$Wait = $true\n" : "$Wait = $false\n") + FileLocksmithArguments,
            wait ? [Integer("TimeoutMilliseconds", "Milliseconds to wait before native failure; Host timeout can expire earlier.", 10000, 0, 600000)]
                 : [Flag("Json", "Return structured process information.", true)])
            with { Output = wait ? "none" : "text", OutputScript = wait ? ActionOutput : null };

    private static PowerShellCliTemplate ImageResizerOptions() =>
        Legacy("image-resizer") with
        {
            Id = "image-resizer-options",
            Name = "Image Resizer: resize files using dimensions or a saved preset",
            Description = "Resize copies and return new workspace files. Choose dimensions (one can be zero for automatic sizing) or a saved preset. Quality, metadata and orientation options apply to copies; originals and host destinations are never replaced.",
            Parameters =
            [
                Text("Sizing", "Preset preserves its saved dimensions, fit and unit.", "dimensions", "dimensions", "preset"),
                Integer("PresetIndex", "Zero-based saved preset index, used only by preset sizing.", 0),
                Text("Width", "Nonnegative invariant decimal; zero means automatic.", "1024"),
                Text("Height", "Nonnegative invariant decimal; zero means automatic.", "768"),
                Text("Fit", "Dimension sizing fit mode.", "Fit", "Fit", "Fill", "Stretch"),
                Text("Unit", "Dimension sizing measurement unit.", "Pixel", "Pixel", "Percent", "Centimeter", "Inch"),
                Flag("ShrinkOnly", "Enable shrink-only; false retains the module's saved default."),
                Flag("IgnoreOrientation", "Enable ignoring orientation; false retains the saved default."),
                Flag("RemoveMetadata", "Enable metadata removal; false retains the saved default."),
                Flag("KeepDateModified", "Preserve modification date; false retains the saved default."),
                Integer("JpegQuality", "JPEG quality 1-100; zero uses the module's saved quality.", 0, 0, 100),
                Text("FileNamePattern", "Filename-only pattern; paths are not allowed. Native %1-style substitutions are supported.", "%1 (resized)"),
            ],
            Arguments = """
                if (-not @($Files).Count) { throw 'Choose image files.' }
                $__powerscriptResizeOptions = [Collections.Generic.List[string]]::new()
                if ($Sizing -eq 'preset') {
                    $__powerscriptResizeOptions.AddRange([string[]]@('--size', $PresetIndex.ToString([Globalization.CultureInfo]::InvariantCulture)))
                } else {
                    $__powerscriptDimensions = @(
                        foreach ($__powerscriptDimension in @($Width, $Height)) {
                            $__powerscriptNumber = 0.0
                            if (-not [double]::TryParse($__powerscriptDimension, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$__powerscriptNumber) -or
                                -not [double]::IsFinite($__powerscriptNumber) -or $__powerscriptNumber -lt 0) { throw 'Dimensions must be nonnegative invariant decimal numbers.' }
                            $__powerscriptNumber
                        }
                    )
                    if ($__powerscriptDimensions[0] -eq 0 -and $__powerscriptDimensions[1] -eq 0) { throw 'At least one dimension must be positive.' }
                    $__powerscriptResizeOptions.AddRange([string[]]@('--width', $Width, '--height', $Height, '--fit', $Fit, '--unit', $Unit))
                }
                if (-not $FileNamePattern.Trim() -or $FileNamePattern.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or $FileNamePattern -in @('.', '..')) { throw 'FileNamePattern must not contain a path or invalid filename characters.' }
                foreach ($__powerscriptOption in @(
                    @{ enabled = $ShrinkOnly; name = '--shrink-only' },
                    @{ enabled = $IgnoreOrientation; name = '--ignore-orientation' },
                    @{ enabled = $RemoveMetadata; name = '--remove-metadata' },
                    @{ enabled = $KeepDateModified; name = '--keep-date-modified' }
                )) {
                    if ($__powerscriptOption.enabled) { $__powerscriptResizeOptions.Add($__powerscriptOption.name) }
                }
                if ($JpegQuality -gt 0) { $__powerscriptResizeOptions.AddRange([string[]]@('--quality', $JpegQuality.ToString([Globalization.CultureInfo]::InvariantCulture))) }
                $__powerscriptWorkspace = $env:POWERSCRIPTS_MXC_WORKSPACE
                if (-not $__powerscriptWorkspace) { $__powerscriptWorkspace = [IO.Path]::GetTempPath() }
                $__powerscriptResizeRoot = Join-Path $__powerscriptWorkspace ('powerscript-resize-' + [Guid]::NewGuid().ToString('N'))
                $__powerscriptResizeOutput = Join-Path $__powerscriptResizeRoot 'output'
                [IO.Directory]::CreateDirectory($__powerscriptResizeOutput) | Out-Null
                $__powerscriptArguments.AddRange($__powerscriptResizeOptions)
                $__powerscriptArguments.AddRange([string[]]@('--destination', $__powerscriptResizeOutput, '--replace', 'false', '--filename', $FileNamePattern))
                $__powerscriptArguments.Add('--')
                $__powerscriptIndex = 0
                foreach ($__powerscriptFile in $Files) {
                    $__powerscriptInputDirectory = Join-Path $__powerscriptResizeRoot ('input-' + $__powerscriptIndex++)
                    [IO.Directory]::CreateDirectory($__powerscriptInputDirectory) | Out-Null
                    $__powerscriptCopy = Join-Path $__powerscriptInputDirectory ([IO.Path]::GetFileName($__powerscriptFile))
                    [IO.File]::Copy([IO.Path]::GetFullPath($__powerscriptFile), $__powerscriptCopy, $false)
                    $__powerscriptArguments.Add($__powerscriptCopy)
                }
                """,
        };

    private static PowerShellCliTemplate PowerDisplayAdjust() =>
        Template("power-display-adjust-action", "Power Display: adjust one or multiple monitors",
            "Set or adjust brightness, contrast or volume on selected monitors. Monitor ID takes precedence; comma-separated positive indices support batches. No power-off or input-source change.", "none",
            @"WinUI3Apps\PowerToys.PowerDisplay.Cli.exe",
            "$__powerscriptArguments.Add($Command)\n" + DisplayTargets + """

            $__powerscriptArguments.Add('--' + $Setting)
            if ($Command -eq 'set') { $__powerscriptArguments.Add($Value.ToString([Globalization.CultureInfo]::InvariantCulture)) }
            else { $__powerscriptArguments.AddRange([string[]]@('--step', $Step.ToString([Globalization.CultureInfo]::InvariantCulture))) }
            """,
            Text("Command", "Set an absolute value or adjust by a step.", "set", "set", "up", "down"),
            Text("Setting", "Continuous setting.", "brightness", "brightness", "contrast", "volume"),
            Integer("Value", "Absolute value for set.", 50, 0, 100),
            Integer("Step", "Relative increment for up/down.", 5, 0),
            Text("MonitorId", "Optional stable ID; overrides MonitorNumbers."),
            Text("MonitorNumbers", "Comma-separated positive monitor indices; empty lets the CLI select displays.", "1"))
            with { Output = "none", OutputScript = ActionOutput };

    private static PowerShellCliTemplate PowerDisplayDiscrete() =>
        Template("power-display-discrete-action", "Power Display: change discrete settings on selected monitors",
            "Change color temperature, input source or orientation. Input/orientation changes may disrupt the desktop; query capabilities first. No monitor power-off.", "none",
            @"WinUI3Apps\PowerToys.PowerDisplay.Cli.exe",
            "$__powerscriptArguments.Add('set')\n" + DisplayTargets + """

            if ($Setting -eq 'orientation') {
                if ($Value -notin @('0', '90', '180', '270')) { throw 'Orientation must be 0, 90, 180 or 270.' }
            } elseif ($Value -notmatch '^(0[xX])?[0-9a-fA-F]{1,4}$') { throw 'Choose a supported hexadecimal VCP value.' }
            $__powerscriptArguments.AddRange([string[]]@(('--' + $Setting), $Value))
            """,
            Text("Setting", "Query monitor capabilities before choosing a discrete value.", "orientation", "color-temperature", "input-source", "orientation"),
            Text("Value", "Orientation angle or supported hexadecimal VCP value.", "0"),
            Text("MonitorId", "Optional stable ID; overrides MonitorNumbers."),
            Text("MonitorNumbers", "Comma-separated positive monitor indices; empty lets the CLI select displays.", "1"))
            with { Output = "none", OutputScript = ActionOutput };
}
