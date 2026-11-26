// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Templates.CliModules;

internal sealed class ImageResizerCliModule : PowerShellCliModule
{
    private const string ImageResizerOptionsScript = """
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
                """;

    public override string Id => "image-resizer";

    public override string Name => "Image Resizer";

    public override string IconName => "ImageResizer";

    public override string Executable => @"WinUI3Apps\PowerToys.ImageResizerCLI.exe";

    public override int Order => 50;

    public override IReadOnlyList<PowerShellCliTemplate> CreateRecipes() =>
    [
        Describe(
            Query(
                "image-resizer-config",
                "Image Resizer: inspect saved configuration",
                "Read saved presets and resize/encoding defaults; does not resize or replace files.",
                "none",
                CliArgument.Literal("--show-config")),
            "--show-config"),
        Describe(
            ImageResizerOptions(),
            "--width/--height --fit --unit | --size; --shrink-only --ignore-orientation --remove-metadata --keep-date-modified --quality --filename; copy-only destination"),
    ];

    private PowerShellCliTemplate ImageResizerOptions() =>
        Template(
            "image-resizer-options",
            "Image Resizer: resize files using dimensions or a saved preset",
            "Resize copies and return new workspace files. Choose dimensions (one can be zero for automatic sizing) or a saved preset. Quality, metadata and orientation options apply to copies; originals and host destinations are never replaced.",
            "files",
            ImageResizerOptionsScript,
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
            Text("FileNamePattern", "Filename-only pattern; paths are not allowed. Native %1-style substitutions are supported.", "%1 (resized)")) with
        {
            Output = "files",
            SupportsFileBatch = true,
            OutputScript = """
                    if (-not [string]::IsNullOrWhiteSpace($__powerscriptOutput)) { [Console]::Error.WriteLine($__powerscriptOutput) }
                    $__powerscriptResized = @([IO.Directory]::GetFiles($__powerscriptResizeOutput))
                    if ($__powerscriptResized.Count -ne @($Files).Count) { throw 'Image Resizer did not return one output file for every input.' }
                    [Console]::Write((@{ files = $__powerscriptResized } | ConvertTo-Json -Compress))
                    """,
        };
}
