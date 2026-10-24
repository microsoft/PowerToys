<#
.SYNOPSIS
    Validates that NOTICE.md reflects top-level NuGet packages in restored project
    inventories.

.DESCRIPTION
    Scans 'project.assets.json' files for managed and native projects without calling
    dotnet.
    
    It extracts top-level package dependencies, filters out System/Microsoft and auto-
    referenced packages, and verifies that every remaining third-party package is
    correctly documented in NOTICE.md.
    
    Use -Verbose to display NOTICE.md and the complete package inventory with owning
    projects.

    Missing NOTICE entries always include their owning projects in the difference report.
    The audit covers existing restore outputs; projects without assets files are not
    discovered.

.PARAMETER path
    The root directory to search for project.assets.json files and the NOTICE.md file.

.OUTPUTS
    Writes a summary report to the console. Exits with code 1 if missing or unexpected
    packages are found, and 0 if they match perfectly (excluding allowed test packages).
#>
#Requires -Version 7
[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True, Position=1)]
    [string]$path
)

$ErrorActionPreference = 'Stop'

trap {
    Write-Host -ForegroundColor Red "FAILED: $($_.Exception.Message)"
    exit 1
}

$root = Get-Item -LiteralPath $path
if ($root -isnot [System.IO.DirectoryInfo]) {
    throw "The project root must be a filesystem directory: $path"
}

$references = @(
    'System.Collections',
    'System.Collections.Concurrent',
    'System.IO.FileSystem',
    'System.Memory',
    'System.Threading.Tasks.Parallel',
    'System.Text.Json'
)

if (-not ('PowerToysNoticeAssetsAudit' -as [type])) {
    Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

// Audits managed and native restore inventories without accepting partial or conflicting
// package sets.
public static class PowerToysNoticeAssetsAudit
{
    // Never return a partial inventory, which could hide missing notices behind
    // unreadable restore output.
    public static HashSet<string> CollectTopLevelPackages(
        string root,
        ConcurrentDictionary<string, HashSet<string>> inventories)
    {
        root = Path.GetFullPath(root);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Hidden | FileAttributes.System
        };
        var files = new List<string>();
        foreach (var f in Directory.EnumerateFiles(root, "project.assets.json", options))
        {
            files.Add(f);
        }

        if (files.Count == 0)
        {
            throw new InvalidDataException(
                "No project.assets.json files were found. Restore the intended projects before auditing.");
        }

        var packages = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var errors = new ConcurrentBag<string>();
        Parallel.ForEach(files, path =>
        {
            try
            {
                using (var stream = File.OpenRead(path))
                using (var doc = JsonDocument.Parse(stream))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                        !doc.RootElement.TryGetProperty("project", out var project) ||
                        project.ValueKind != JsonValueKind.Object ||
                        !project.TryGetProperty("restore", out var restore) ||
                        restore.ValueKind != JsonValueKind.Object ||
                        !restore.TryGetProperty("projectPath", out var owner) ||
                        owner.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(owner.GetString()) ||
                        !project.TryGetProperty("frameworks", out var frameworks) ||
                        frameworks.ValueKind != JsonValueKind.Object ||
                        !frameworks.EnumerateObject().MoveNext())
                    {
                        throw new InvalidDataException(
                            "Missing project ownership or target-framework metadata.");
                    }

                    string projectPath = Path.GetFullPath(owner.GetString(), root);
                    ValidateProjectPath(root, projectPath);
                    if (doc.RootElement.TryGetProperty("logs", out var logs))
                    {
                        if (logs.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidDataException("Invalid restore diagnostics.");
                        }
                        foreach (var log in logs.EnumerateArray())
                        {
                            if (log.TryGetProperty("level", out var level) &&
                                string.Equals(level.GetString(), "Error", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new InvalidDataException(
                                    "The assets file contains a restore error: " + log.ToString());
                            }
                        }
                    }

                    var projectPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var tfm in frameworks.EnumerateObject())
                    {
                        if (tfm.Value.ValueKind != JsonValueKind.Object)
                        {
                            throw new InvalidDataException("Invalid target-framework metadata.");
                        }
                        if (!tfm.Value.TryGetProperty("dependencies", out var deps))
                        {
                            continue;
                        }
                        if (deps.ValueKind != JsonValueKind.Object)
                        {
                            throw new InvalidDataException("Invalid dependencies metadata.");
                        }
                        foreach (var dep in deps.EnumerateObject())
                        {
                            if (string.IsNullOrWhiteSpace(dep.Name) ||
                                dep.Value.ValueKind != JsonValueKind.Object ||
                                !dep.Value.TryGetProperty("target", out var target) ||
                                target.ValueKind != JsonValueKind.String)
                            {
                                throw new InvalidDataException("Invalid dependency metadata for '" + dep.Name + "'.");
                            }
                            // Project references are not packages, even when their names resemble package IDs.
                            if (string.Equals(target.GetString(), "Project", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            if (!string.Equals(target.GetString(), "Package", StringComparison.OrdinalIgnoreCase))
                            {
                                throw new InvalidDataException(
                                    "Unsupported dependency target for '" + dep.Name + "': " + target.GetString());
                            }
                            if (dep.Value.TryGetProperty("autoReferenced", out var autoRef))
                            {
                                if (autoRef.ValueKind == JsonValueKind.True)
                                {
                                    continue;
                                }
                                if (autoRef.ValueKind != JsonValueKind.False)
                                {
                                    throw new InvalidDataException(
                                        "Invalid autoReferenced flag for '" + dep.Name + "'.");
                                }
                            }
                            string id = dep.Name;
                            if (id.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                                id.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            projectPackages.Add(id);
                        }
                    }
                    if (!inventories.TryAdd(projectPath, projectPackages) &&
                        !inventories[projectPath].SetEquals(projectPackages))
                    {
                        throw new InvalidDataException(
                            "Conflicting restored package inventories for '" + projectPath + "'. Clean and restore before auditing.");
                    }
                    foreach (string id in projectPackages)
                    {
                        packages.TryAdd(id, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(path + ": " + ex.Message);
            }
        });
        if (inventories.IsEmpty)
        {
            errors.Add("No valid restored project inventories remained.");
        }
        if (!errors.IsEmpty)
        {
            var sortedErrors = new List<string>(errors);
            sortedErrors.Sort(StringComparer.OrdinalIgnoreCase);
            throw new InvalidDataException("Could not collect a complete restored package inventory:" + Environment.NewLine +
                string.Join(Environment.NewLine, sortedErrors));
        }
        return new HashSet<string>(packages.Keys, StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateProjectPath(string root, string projectPath)
    {
        string relativePath = Path.GetRelativePath(root, projectPath);
        if (Path.IsPathRooted(relativePath) || relativePath == ".." ||
            relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The owning project is outside the audit root: " + projectPath);
        }
        if (!File.Exists(projectPath))
        {
            throw new InvalidDataException("The owning project no longer exists: " + projectPath +
                ". Remove the stale obj folder containing this assets file, then rerun the audit.");
        }
    }
}
'@
}

$noticePath = Join-Path $root.FullName 'NOTICE.md'
$noticeFile = Get-Content -LiteralPath $noticePath -Raw
Write-Verbose "NOTICE.md contents (${noticePath}):$([Environment]::NewLine)$noticeFile"

$noticePattern = '(?ms)^## NuGet Packages used by PowerToys[ \t]*\r?\n(?<packages>.*?)(?=^## |\z)'
$noticeMatches = [regex]::Matches($noticeFile, $noticePattern)
if ($noticeMatches.Count -ne 1) {
    throw 'Expected exactly one ''NuGet Packages used by PowerToys'' section in NOTICE.md.'
}

$comparer = [System.StringComparer]::OrdinalIgnoreCase
$noticePackages = [System.Collections.Generic.HashSet[string]]::new($comparer)

foreach ($line in ($noticeMatches[0].Groups['packages'].Value -split '\r?\n')) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }
    if ($line -notmatch '^- (?<id>\S+)[ \t]*$') {
        throw "Invalid package entry in NOTICE.md: $line"
    }
    [void]$noticePackages.Add($Matches['id'])
}

Write-Host 'Extracting NuGet dependencies from restored managed and native project inventories...'
$projectInventories = [System.Collections.Concurrent.ConcurrentDictionary[string, System.Collections.Generic.HashSet[string]]]::new($comparer)

$generatedPackages = [PowerToysNoticeAssetsAudit]::CollectTopLevelPackages($root.FullName, $projectInventories)

$packageProjects = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[string]]]::new($comparer)
foreach ($project in $projectInventories.GetEnumerator()) {
    $projectPath = [System.IO.Path]::GetRelativePath($root.FullName, $project.Key)
    foreach ($pkg in $project.Value) {
        if (!$packageProjects.ContainsKey($pkg)) {
            $packageProjects.Add($pkg, [System.Collections.Generic.List[string]]::new())
        }
        $packageProjects[$pkg].Add($projectPath)
    }
}

if ($PSBoundParameters.ContainsKey('Verbose')) {
    Write-Verbose 'Restored package inventory with owning projects:'
    foreach ($pkg in ($generatedPackages | Sort-Object)) {
        Write-Verbose "Package: $pkg"
        foreach ($projectPath in ($packageProjects[$pkg] | Sort-Object)) {
            Write-Verbose "  Project: $projectPath"
        }
    }
}

# Calculate differences.
$allowedExtraPackages = [System.Collections.Generic.HashSet[string]]::new([string[]]@('Moq', 'MSTest'), $comparer)

$missingFromNotice = [System.Collections.Generic.HashSet[string]]::new($generatedPackages, $comparer)
$missingFromNotice.ExceptWith($noticePackages)

$extraInNotice = [System.Collections.Generic.HashSet[string]]::new($noticePackages, $comparer)
$extraInNotice.ExceptWith($generatedPackages)

$allowedExtra = [System.Collections.Generic.HashSet[string]]::new($extraInNotice, $comparer)
$allowedExtra.IntersectWith($allowedExtraPackages)

$unexpectedExtra = [System.Collections.Generic.HashSet[string]]::new($extraInNotice, $comparer)
$unexpectedExtra.ExceptWith($allowedExtraPackages)

# Build report instead of outputting line-by-line.
$report = [System.Text.StringBuilder]::new()
$totalFailures = 0

if ($missingFromNotice.Count -gt 0 -or $unexpectedExtra.Count -gt 0) {
    [void]$report.AppendLine('FAILED: NOTICE.md mismatch detected.')
    [void]$report.AppendLine('=== DETAILED DIFFERENCE ANALYSIS ===')
    [void]$report.AppendLine('')

    if ($missingFromNotice.Count -gt 0) {
        [void]$report.AppendLine('MissingFromNotice (ERROR - these must be added to NOTICE.md):')
        foreach ($pkg in ($missingFromNotice | Sort-Object)) {
            [void]$report.AppendLine("  - $pkg")
            foreach ($projectPath in ($packageProjects[$pkg] | Sort-Object)) {
                [void]$report.AppendLine("    Project: $projectPath")
            }
        }
        [void]$report.AppendLine('')
        $totalFailures++
    }

    if ($unexpectedExtra.Count -gt 0) {
        [void]$report.AppendLine('ExtraInNotice (ERROR - unexpected packages in NOTICE.md):')
        foreach ($pkg in ($unexpectedExtra | Sort-Object)) {
            [void]$report.AppendLine("  - $pkg")
        }
        [void]$report.AppendLine('')
        $totalFailures++
    }
}

[void]$report.AppendLine('Summary:')
[void]$report.AppendLine("  Restored packages found:     $($generatedPackages.Count)")
[void]$report.AppendLine("  NOTICE.md packages:          $($noticePackages.Count)")
[void]$report.AppendLine("  MissingFromNotice:           $($missingFromNotice.Count)")
[void]$report.AppendLine("  ExtraInNotice (allowed):     $($allowedExtra.Count)")
[void]$report.AppendLine("  ExtraInNotice (unexpected):  $($unexpectedExtra.Count)")

if ($totalFailures -gt 0) {
    Write-Host -ForegroundColor Red $report.ToString()
    exit 1
} else {
    if ($allowedExtra.Count -gt 0) {
        [void]$report.AppendLine('')
        [void]$report.AppendLine('ExtraInNotice (OK - allowed test-only packages):')
        foreach ($pkg in ($allowedExtra | Sort-Object)) {
            [void]$report.AppendLine("  - $pkg")
        }
    }
    [void]$report.AppendLine('')
    [void]$report.AppendLine('PASSED: NOTICE.md matches (with allowed test-only packages).')
    Write-Host -ForegroundColor Green $report.ToString()
    exit 0
}