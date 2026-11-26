// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using static PowerScripts.Core.Templates.CliRecipeBuilder;

namespace PowerScripts.Core.Templates.CliModules;

internal sealed class FileLocksmithCliModule : PowerShellCliModule
{
    private const string FileLocksmithCliModuleScript = """
                $Files = @($Content -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
                foreach ($__powerscriptPath in $Files) {
                    if (-not [IO.Path]::IsPathFullyQualified($__powerscriptPath)) { throw 'Supply absolute file or directory paths.' }
                }

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

    public override string Id => "file-locksmith";

    public override string Name => "File Locksmith";

    public override string IconName => "FileLocksmith";

    public override string Executable => @"FileLocksmithCLI.exe";

    public override int Order => 20;

    public override IReadOnlyList<PowerShellCliTemplate> CreateRecipes() =>
    [
        Describe(
            FileLocksmith(wait: false, paths: false),
            "[--json] <paths...>"),
        Describe(
            FileLocksmith(wait: true, paths: false),
            "--wait --timeout <milliseconds> <paths...>"),
        Describe(
            FileLocksmith(wait: false, paths: true),
            "[--json] <newline-separated absolute files/directories>"),
        Describe(
            FileLocksmith(wait: true, paths: true),
            "--wait --timeout <milliseconds> <newline-separated absolute files/directories>"),
    ];

    private PowerShellCliTemplate FileLocksmith(bool wait, bool paths) =>
        Template(
            "file-locksmith-" + (paths ? "paths-" : string.Empty) + (wait ? "wait" : "query"),
            "File Locksmith: " + (wait ? "wait for paths to unlock" : "find processes holding paths") + (paths ? " (path text)" : " (selected files)"),
            paths ? "Supply one absolute file/directory path per line. Directory queries are recursive. Never terminates processes; waiting is bounded by both native timeout and Host budget." : "Inspect selected-file batches or wait for them to unlock. Never terminates processes; waiting is bounded by both native timeout and Host budget.",
            paths ? "text" : "files",
            (paths ? FileLocksmithCliModuleScript : string.Empty) + (wait ? "$Wait = $true\n" : "$Wait = $false\n") + FileLocksmithArguments,
            wait ? [Integer("TimeoutMilliseconds", "Milliseconds to wait before native failure; Host timeout can expire earlier.", 10000, 0, 600000)] : [Flag("Json", "Return structured process information.", true)])
            with
        { SupportsFileBatch = !paths, Output = wait ? "none" : "text", OutputScript = wait ? ActionOutput : null };
}
