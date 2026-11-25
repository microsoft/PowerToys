// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Templates;

public static partial class PowerShellCliTemplates
{
    private static IReadOnlyList<PowerShellCliTemplate> CreateDefinitions() =>
    [
        Template("settings-query", "Settings: list modules or check enabled status",
            "Read saved module state and Group Policy overrides. Status does not prove that a module's process is running.", "none",
            @"WinUI3Apps\PowerToys.Settings.Cli.exe",
            """
            $__powerscriptArguments.Add($Command)
            if ($Command -eq 'status') {
                if (-not $ModuleName.Trim()) { throw 'Status requires ModuleName.' }
                $__powerscriptArguments.Add($ModuleName)
            }
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """,
            Text("Command", "List all modules or query one module.", "list", "list", "status"),
            Text("ModuleName", "Module name for status, such as Awake, FancyZones or LightSwitch.", "Awake"),
            Flag("Json", "Return structured module state.", true)),
        Template("settings-module-state", "Settings: save a module's enabled or disabled state",
            "Saves module state for the next PowerToys start; it does not live-toggle modules. The current Settings CLI requires PowerToys to be stopped, so this cannot currently enable a module from a PowerToys hotkey. Group Policy is respected.", "none",
            @"WinUI3Apps\PowerToys.Settings.Cli.exe",
            """
            if (-not $ModuleName.Trim()) { throw 'Choose a ModuleName.' }
            $__powerscriptArguments.AddRange([string[]]@($Command, $ModuleName))
            """,
            Text("Command", "Save the module as enabled or disabled for the next PowerToys start.", "enable", "enable", "disable"),
            Text("ModuleName", "Module name to change, such as Awake, FancyZones or LightSwitch.", "Awake")),
        Template("advanced-paste-local", "Advanced Paste: transform text locally",
            "Convert text to plain text, Markdown or JSON using the local transformation engine. Uses standard input/output only; no clipboard, package identity, saved credentials, AI or network access.", "text",
            @"WinUI3Apps\PowerToys.AdvancedPaste.LocalCLI.exe",
            """
            $__powerscriptArguments.AddRange([string[]]@('--format', $Format))
            """,
            Text("Format", "Local text transformation.", "plain-text", "plain-text", "markdown", "json"))
            with { StandardInput = "$Content" },
        Template("advanced-paste-list", "Advanced Paste: list actions or providers",
            "Read-only discovery. Does not read the clipboard or contact AI providers.", "none",
            @"WinUI3Apps\PowerToys.AdvancedPaste.CLI.exe",
            """
            $__powerscriptArguments.AddRange([string[]]@($ListKind, 'list'))
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """,
            Text("ListKind", "Which catalogue to list.", "actions", "actions", "providers"),
            Flag("Json", "Return structured JSON.", true)),
        Template("advanced-paste-transform", "Advanced Paste: transform a file",
            "Transform one selected file. AI actions may contact the configured provider and incur costs. File-producing actions require an output path.", "files",
            @"WinUI3Apps\PowerToys.AdvancedPaste.CLI.exe",
            """
            if (@($Files).Count -ne 1) { throw 'Select exactly one input file.' }
            $__powerscriptArguments.AddRange([string[]]@('transform', '--input', $Files[0]))
            if ($CustomAction) {
                $__powerscriptArguments.AddRange([string[]]@('--custom-action', $CustomAction))
            } else {
                $__powerscriptArguments.AddRange([string[]]@('--action', $Action))
            }
            if ($Action -eq 'paste-with-ai' -and -not $CustomAction -and -not $Prompt.Trim()) { throw 'AI paste requires Prompt.' }
            if ($Prompt) { $__powerscriptArguments.AddRange([string[]]@('--prompt', $Prompt)) }
            if ($Provider) {
                if (-not $CustomAction -and $Action -notin @('paste-with-ai', 'fix-spelling-and-grammar')) { throw 'Provider is only supported for AI or custom actions.' }
                $__powerscriptArguments.AddRange([string[]]@('--provider', $Provider))
            }
            if (-not $CustomAction -and ($Action.StartsWith('paste-as-') -or $Action.StartsWith('transcode-to-')) -and -not $OutputPath.Trim()) { throw 'File-producing actions require OutputPath.' }
            if ($OutputPath) { $__powerscriptArguments.AddRange([string[]]@('--output', $OutputPath)) }
            else { $__powerscriptArguments.Add('--stdout') }
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """,
            Text("Action", "Built-in transformation; ignored when CustomAction is provided.", "plain-text",
                "plain-text", "markdown", "json", "fix-spelling-and-grammar", "image-to-text",
                "paste-as-txt-file", "paste-as-png-file", "paste-as-jpg-file", "paste-as-html-file",
                "transcode-to-mp3", "transcode-to-mp4", "paste-with-ai"),
            Text("CustomAction", "Optional saved action ID or name, instead of a built-in action."),
            Text("Prompt", "Prompt for an AI action; content can be sent to a configured provider."),
            Text("Provider", "Optional configured AI provider ID."),
            Text("OutputPath", "Optional destination file; otherwise use standard output. May overwrite a destination according to CLI behavior."),
            Flag("Json", "Ask the CLI for JSON output.")),
        Template("file-locksmith", "File Locksmith: find processes using a file",
            "Inspect one selected file. Requires host-process visibility. Never terminates processes.", "files",
            "FileLocksmithCLI.exe",
            """
            if (@($Files).Count -ne 1) { throw 'Select exactly one file to inspect.' }
            if ($Json) { $__powerscriptArguments.Add('--json') }
            $__powerscriptArguments.Add([IO.Path]::GetFullPath($Files[0]))
            """,
            Flag("Json", "Return structured process information.", true)),
        Template("fancyzones", "FancyZones: query or manage layouts",
            "Default command lists layouts. Set and remove commands change layout/hotkey settings; open commands launch UI. Requires FancyZones on the host desktop.", "none",
            "FancyZonesCLI.exe",
            """
            $__powerscriptArguments.Add($Command)
            switch ($Command) {
                'set-layout' {
                    if (-not $Layout.Trim()) { throw 'Set-layout requires Layout.' }
                    $__powerscriptArguments.Add($Layout)
                    if ($AllMonitors) { $__powerscriptArguments.Add('--all') }
                    else { $__powerscriptArguments.AddRange([string[]]@('--monitor', $Monitor.ToString([Globalization.CultureInfo]::InvariantCulture))) }
                }
                'set-hotkey' {
                    if (-not $Layout.Trim()) { throw 'Set-hotkey requires a layout UUID.' }
                    $__powerscriptArguments.AddRange([string[]]@($Hotkey.ToString([Globalization.CultureInfo]::InvariantCulture), $Layout))
                }
                'remove-hotkey' { $__powerscriptArguments.Add($Hotkey.ToString([Globalization.CultureInfo]::InvariantCulture)) }
            }
            """,
            Text("Command", "Queries are read-only; set/remove/open commands have side effects.", "get-layouts",
                "get-layouts", "get-monitors", "get-active-layout", "get-hotkeys", "set-layout", "set-hotkey", "remove-hotkey", "open-editor", "open-settings"),
            Text("Layout", "Layout name/UUID for set-layout, or UUID for set-hotkey."),
            Integer("Monitor", "One-based monitor index for set-layout; ignored when AllMonitors is enabled.", 1, 1),
            Flag("AllMonitors", "Apply the layout to every monitor."),
            Integer("Hotkey", "Layout hotkey digit for set/remove-hotkey.", 1, 0, 9)),
        Template("workspaces", "Workspaces: list or launch a workspace",
            "Listing is read-only. Launch starts applications and arranges windows; it is not replay-safe. Requires the host desktop and can show application prompts.", "none",
            "PowerToys.WorkspacesCLI.exe",
            """
            if ($WorkspaceId -and $WorkspaceName) { throw 'Specify WorkspaceId or WorkspaceName, not both.' }
            if ($Command -eq 'launch' -and -not ($WorkspaceId -or $WorkspaceName)) { throw 'Launch requires a workspace selector.' }
            $__powerscriptArguments.Add($Command)
            if ($WorkspaceId) { $__powerscriptArguments.AddRange([string[]]@('--id', $WorkspaceId)) }
            if ($WorkspaceName) { $__powerscriptArguments.AddRange([string[]]@('--name', $WorkspaceName)) }
            if ($Command -eq 'list' -and $Details) { $__powerscriptArguments.Add('--details') }
            if ($Command -eq 'launch') { $__powerscriptArguments.AddRange([string[]]@('--timeout', $TimeoutSeconds.ToString([Globalization.CultureInfo]::InvariantCulture))) }
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """,
            Text("Command", "List saved workspaces or launch applications.", "list", "list", "launch"),
            Text("WorkspaceId", "Optional workspace GUID; mutually exclusive with WorkspaceName."),
            Text("WorkspaceName", "Optional workspace name; mutually exclusive with WorkspaceId."),
            Flag("Details", "Include application details when listing."),
            Integer("TimeoutSeconds", "Launch timeout in seconds.", 120, 1, 600),
            Flag("Json", "Return structured JSON.", true)),
        Template("image-resizer", "Image Resizer: resize selected files",
            "Resize copies of selected images into the execution workspace and return the new files. Originals are never passed to the CLI. The consumer owns saving the results; no host destination is requested. Width/height support invariant decimals.", "files",
            @"WinUI3Apps\PowerToys.ImageResizerCLI.exe",
            """
            if (-not @($Files).Count) { throw 'Select image files.' }
            foreach ($__powerscriptDimension in @($Width, $Height)) {
                $__powerscriptNumber = 0.0
                if (-not [double]::TryParse($__powerscriptDimension, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$__powerscriptNumber) -or
                    -not [double]::IsFinite($__powerscriptNumber) -or $__powerscriptNumber -le 0) { throw 'Width and Height must be positive invariant decimal numbers.' }
            }
            $__powerscriptWorkspace = $env:POWERSCRIPTS_MXC_WORKSPACE
            if (-not $__powerscriptWorkspace) { $__powerscriptWorkspace = [IO.Path]::GetTempPath() }
            $__powerscriptResizeRoot = Join-Path $__powerscriptWorkspace ('powerscript-resize-' + [Guid]::NewGuid().ToString('N'))
            $__powerscriptResizeOutput = Join-Path $__powerscriptResizeRoot 'output'
            [IO.Directory]::CreateDirectory($__powerscriptResizeOutput) | Out-Null
            $__powerscriptArguments.AddRange([string[]]@('--destination', $__powerscriptResizeOutput, '--width', $Width, '--height', $Height, '--fit', $Fit, '--unit', $Unit, '--replace', 'false', '--filename', '%1 (resized)'))
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
            Text("Width", "Positive width, using a decimal point.", "1024"),
            Text("Height", "Positive height, using a decimal point.", "768"),
            Text("Fit", "Resize fit mode.", "Fit", "Fit", "Fill", "Stretch"),
            Text("Unit", "Measurement unit.", "Pixel", "Pixel", "Percent", "Centimeter", "Inch"))
            with
            {
                Output = "files",
                OutputScript = """
                    if (-not [string]::IsNullOrWhiteSpace($__powerscriptOutput)) { [Console]::Error.WriteLine($__powerscriptOutput) }
                    $__powerscriptResized = @([IO.Directory]::GetFiles($__powerscriptResizeOutput))
                    if ($__powerscriptResized.Count -ne @($Files).Count) { throw 'Image Resizer did not return one output file for every input.' }
                    [Console]::Write((@{ files = $__powerscriptResized } | ConvertTo-Json -Compress))
                    """,
            },
        Template("awake", "Awake: keep the machine awake",
            "Starts a keep-awake process for a limited duration by default. An already-running Awake instance prevents another CLI instance from starting. This is a long-running CLI; increase the PowerScripts execution timeout deliberately if needed.", "none",
            "PowerToys.Awake.exe",
            """
            if ($UseConfiguration) { $__powerscriptArguments.Add('--use-pt-config') }
            else {
                if ($ExpireAt) { $__powerscriptArguments.AddRange([string[]]@('--expire-at', $ExpireAt)) }
                else { $__powerscriptArguments.AddRange([string[]]@('--time-limit', $TimeLimitSeconds.ToString([Globalization.CultureInfo]::InvariantCulture))) }
                if ($DisplayOn) { $__powerscriptArguments.Add('--display-on') }
                if ($ParentProcess) {
                    if ($ProcessId -gt 0) { throw 'Choose ParentProcess or ProcessId, not both.' }
                    $__powerscriptArguments.Add('--use-parent-pid')
                } elseif ($ProcessId -gt 0) { $__powerscriptArguments.AddRange([string[]]@('--pid', $ProcessId.ToString([Globalization.CultureInfo]::InvariantCulture))) }
            }
            """,
            Integer("TimeLimitSeconds", "Seconds to remain awake; zero means indefinite and is not the default.", 10),
            Flag("DisplayOn", "Keep the display on too."),
            Text("ExpireAt", "Optional CLI date/time string instead of TimeLimitSeconds."),
            Integer("ProcessId", "Optional process whose lifetime bounds Awake; zero omits it.", 0),
            Flag("ParentProcess", "Bind Awake to its parent script process."),
            Flag("UseConfiguration", "Use existing PowerToys Awake settings instead of the other options.")),
        Template("light-switch", "Light Switch: query or change the theme",
            "Status is read-only. Light/dark/toggle and scheduling commands change the user's theme settings and require the host desktop.", "none",
            "PowerToys.LightSwitch.Cli.exe",
            """
            switch ($Command) {
                'schedule-enable' { $__powerscriptArguments.AddRange([string[]]@('schedule', 'enable', '--mode', $ScheduleMode)) }
                'schedule-disable' { $__powerscriptArguments.AddRange([string[]]@('schedule', 'disable')) }
                default { $__powerscriptArguments.Add($Command) }
            }
            if ($Json) { $__powerscriptArguments.Add('--json') }
            """,
            Text("Command", "Status, theme selection or schedule management.", "status", "status", "light", "dark", "toggle", "schedule-enable", "schedule-disable"),
            Text("ScheduleMode", "Used only by schedule-enable.", "fixed-hours", "fixed-hours", "sunset-to-sunrise", "follow-night-light"),
            Flag("Json", "Return structured JSON.", true)),
        Template("power-display-query", "Power Display: query displays",
            "Read monitor information, supported settings or saved profiles. Requires a compatible monitor and the running Power Display module.", "none",
            @"WinUI3Apps\PowerToys.PowerDisplay.Cli.exe",
            """
            $__powerscriptArguments.Add($Command)
            if ($Command -in @('get', 'capabilities')) {
                if ($MonitorId) { $__powerscriptArguments.AddRange([string[]]@('--monitor-id', $MonitorId)) }
                elseif ($MonitorNumber -gt 0) { $__powerscriptArguments.AddRange([string[]]@('--monitor-number', $MonitorNumber.ToString([Globalization.CultureInfo]::InvariantCulture))) }
                if ($Setting) { $__powerscriptArguments.AddRange([string[]]@('--setting', $Setting)) }
            }
            """,
            Text("Command", "Read-only query.", "list", "list", "capabilities", "get", "profiles"),
            Text("MonitorId", "Optional monitor ID; takes precedence over MonitorNumber."),
            Integer("MonitorNumber", "One-based monitor number; zero lets the CLI select displays.", 0),
            Text("Setting", "Optional setting filter.", "", "", "brightness", "contrast", "volume", "color-temperature", "input-source", "power-state", "orientation")),
        Template("power-display-adjust", "Power Display: adjust brightness, contrast or volume",
            "Changes a continuous monitor setting. Does not power off displays or switch input sources. Requires a compatible monitor and the running Power Display module.", "none",
            @"WinUI3Apps\PowerToys.PowerDisplay.Cli.exe",
            """
            $__powerscriptArguments.Add($Command)
            if ($MonitorId) { $__powerscriptArguments.AddRange([string[]]@('--monitor-id', $MonitorId)) }
            elseif ($MonitorNumber -gt 0) { $__powerscriptArguments.AddRange([string[]]@('--monitor-number', $MonitorNumber.ToString([Globalization.CultureInfo]::InvariantCulture))) }
            $__powerscriptArguments.Add('--' + $Setting)
            if ($Command -eq 'set') { $__powerscriptArguments.Add($Value.ToString([Globalization.CultureInfo]::InvariantCulture)) }
            else { $__powerscriptArguments.AddRange([string[]]@('--step', $Step.ToString([Globalization.CultureInfo]::InvariantCulture))) }
            """,
            Text("Command", "Set an absolute value or adjust by a step.", "set", "set", "up", "down"),
            Text("Setting", "Continuous setting to change.", "brightness", "brightness", "contrast", "volume"),
            Integer("Value", "Absolute value used by set.", 50, 0, 100),
            Integer("Step", "Relative increment used by up/down.", 5, 0, 100),
            Text("MonitorId", "Optional monitor ID; takes precedence over MonitorNumber."),
            Integer("MonitorNumber", "One-based monitor number; zero lets the CLI select displays.", 0)),
        Template("power-display-discrete", "Power Display: change color, input source or orientation",
            "Changes a monitor's discrete setting. Input-source changes may make the desktop disappear from that monitor. Requires the host display; monitor power-off is not offered.", "none",
            @"WinUI3Apps\PowerToys.PowerDisplay.Cli.exe",
            """
            $__powerscriptArguments.Add('set')
            if ($MonitorId) { $__powerscriptArguments.AddRange([string[]]@('--monitor-id', $MonitorId)) }
            elseif ($MonitorNumber -gt 0) { $__powerscriptArguments.AddRange([string[]]@('--monitor-number', $MonitorNumber.ToString([Globalization.CultureInfo]::InvariantCulture))) }
            if ($Setting -eq 'orientation') {
                if ($Value -notin @('0', '90', '180', '270')) { throw 'Orientation must be 0, 90, 180 or 270.' }
            } elseif ($Value -notmatch '^(0[xX])?[0-9a-fA-F]{1,4}$') { throw 'Choose a hexadecimal VCP value supported by the monitor.' }
            $__powerscriptArguments.AddRange([string[]]@(('--' + $Setting), $Value))
            """,
            Text("Setting", "Discrete setting to change; query monitor capabilities first.", "orientation", "color-temperature", "input-source", "orientation"),
            Text("Value", "Orientation angle, or supported hexadecimal VCP value.", "0"),
            Text("MonitorId", "Optional monitor ID; takes precedence over MonitorNumber."),
            Integer("MonitorNumber", "One-based monitor number; zero lets the CLI select displays.", 0)),
    ];
}
