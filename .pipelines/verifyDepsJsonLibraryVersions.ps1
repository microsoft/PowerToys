<#
.SYNOPSIS
    Validates that all *.deps.json files under the target directory reference the same
    fileVersion for each dll.

.DESCRIPTION
    Recursively searches for all *.deps.json files under the target directory, and checks
    that each dll is referenced with the same fileVersion across the solution. The goal is
    to prevent different versions of the same module being copied into one directory at
    build time, which can cause build issues.

    If any dll is referenced with different versions, the script will exit with a non-zero
    exit code and print the dlls and their versions.

    Requires PowerShell 7+ (System.Text.Json).

.PARAMETER targetDir
    The root directory to search for *.deps.json files.

.OUTPUTS
    Writes the name of any dlls with mismatched versions, along with the versions and the
    deps.json files that reference them. Exits with code 1 if any mismatches are found,
    and with 0 if all dlls are referenced with the same version.
#>
#Requires -Version 7
[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$targetDir
)

$ErrorActionPreference = 'Stop'
$references = @(
    'System.Collections',
    'System.Memory',
    'System.Linq',
    'System.IO.FileSystem',
    'System.Threading.Tasks.Parallel',
    'System.Text.Json'
)

if (-not ("DepsJsonAudit" -as [type])) {
    Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

// Custom comparer to ensure versions like "10.0.0" sort after "9.0.0".
public class SemanticVersionComparer : IComparer<string>
{
    public int Compare(string x, string y)
    {
        if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (x == null)
        {
            return -1;
        }

        if (y == null)
        {
            return 1;
        }

        // Strip pre-release suffixes for proper System.Version parsing (e.g. "8.0.0-preview" becomes "8.0.0").
        int dashX = x.IndexOf('-');
        int dashY = y.IndexOf('-');
        string cleanX = dashX > 0 ? x.Substring(0, dashX) : x;
        string cleanY = dashY > 0 ? y.Substring(0, dashY) : y;

        if (Version.TryParse(cleanX, out var vx) && Version.TryParse(cleanY, out var vy))
        {
            int cmp = vx.CompareTo(vy);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        // Fallback to alphabetical if base versions are identical, or if they couldn't be parsed.
        return StringComparer.OrdinalIgnoreCase.Compare(x, y);
    }
}

public class AuditResult
{
    public int ScannedFilesCount { get; set; }
    public SortedDictionary<string, SortedDictionary<string, List<string>>> Versions { get; set; }
}

public static class DepsJsonAudit
{
    // Name-based excludes. UI/Fuzzer tests are skipped because of Appium.WebDriver dependencies.
    // MouseJump.Common.UnitTests and EnvironmentVariablesUILib.UnitTests are self-contained WinUI (CsWinRT)
    // unit tests: each bundles its full runtime closure into an isolated output folder, so its private
    // dll copies cannot collide with product binaries.
    private static bool IsExcluded(string path, string name)
    {
        var cmp = StringComparison.OrdinalIgnoreCase;

        if (name.IndexOf("UITest", cmp) >= 0 ||
            name.StartsWith("MouseJump.Common.UnitTests", cmp) ||
            name.StartsWith("EnvironmentVariablesUILib.UnitTests", cmp) ||
            name.IndexOf(".FuzzTests", cmp) >= 0)
        {
            return true;
        }

        // CmdPal / CommandPalette are skipped based on the full path.
        return path.IndexOf("CmdPal", cmp) >= 0 || path.IndexOf("CommandPalette", cmp) >= 0;
    }

    private static List<KeyValuePair<string, string>> ReadFile(string path)
    {
        try
        {
            return ReadFileCore(path);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to parse {path}: {ex.Message}", ex);
        }
    }

    private static List<KeyValuePair<string, string>> ReadFileCore(string path)
    {
        var result = new List<KeyValuePair<string, string>>();

        // File.OpenRead pipes a stream directly to JsonDocument without allocating a byte[] buffer.
        using (var stream = File.OpenRead(path))
        using (var doc = JsonDocument.Parse(stream))
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("targets", out var targets) ||
                targets.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            // targets.<tfm>.<package>.runtime.<dll>.fileVersion
            foreach (var target in targets.EnumerateObject())
            {
                if (target.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var package in target.Value.EnumerateObject())
                {
                    if (package.Value.ValueKind != JsonValueKind.Object ||
                        !package.Value.TryGetProperty("runtime", out var runtime) ||
                        runtime.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    foreach (var entry in runtime.EnumerateObject())
                    {
                        if (!entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (entry.Value.ValueKind != JsonValueKind.Object ||
                            !entry.Value.TryGetProperty("fileVersion", out var fv))
                        {
                            continue;
                        }

                        string version = fv.ValueKind == JsonValueKind.String ? fv.GetString() : string.Empty;
                        string dllName = Path.GetFileName(entry.Name);

                        // After VS 17.11 some PowerToys dlls have no fileVersion in deps.json even though the
                        // version is set correctly; after VS 17.13 they appear as 0.0.0.0. All our dlls share
                        // one version across dependencies, so skip them.
                        if ((string.IsNullOrEmpty(version) || version == "0.0.0.0") &&
                            dllName.StartsWith("PowerToys.", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        result.Add(new KeyValuePair<string, string>(dllName, version));
                    }
                }
            }
        }

        return result;
    }

    // Returns: AuditResult containing total parsed files and DllName > fileVersion > deps.json file names that reference it.
    public static AuditResult Collect(string root)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var files = new List<string>();
        foreach (var f in Directory.EnumerateFiles(root, "*.deps.json", options))
        {
            if (!IsExcluded(f, Path.GetFileName(f))) files.Add(f);
        }

        // Parse in parallel, then merge sequentially in file order so output is deterministic.
        var perFile = new List<KeyValuePair<string, string>>[files.Count];
        Parallel.For(0, files.Count, i => perFile[i] = ReadFile(files[i]));

        var all = new SortedDictionary<string, SortedDictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Count; i++)
        {
            string fileName = Path.GetFileName(files[i]);
            foreach (var kv in perFile[i])
            {
                if (!all.TryGetValue(kv.Key, out var versions))
                {
                    versions = new SortedDictionary<string, List<string>>(new SemanticVersionComparer());
                    all[kv.Key] = versions;
                }
                if (!versions.TryGetValue(kv.Value, out var list))
                {
                    list = new List<string>();
                    versions[kv.Value] = list;
                }
                list.Add(fileName);
            }
        }

        // Sort the file lists alphabetically before returning.
        foreach (var versionDict in all.Values)
        {
            foreach (var list in versionDict.Values)
            {
                list.Sort(StringComparer.OrdinalIgnoreCase);
            }
        }

        return new AuditResult { ScannedFilesCount = files.Count, Versions = all };
    }
}
'@
}

$auditResult = [DepsJsonAudit]::Collect((Resolve-Path $targetDir).Path)

# Check ScannedFilesCount, allowing empty valid manifests to pass.
if ($auditResult.ScannedFilesCount -eq 0) {
    Write-Host -ForegroundColor Yellow "No *.deps.json files found under $targetDir; check the path."
    exit 1
}

$referencedFileVersionsPerDll = $auditResult.Versions
$totalFailures = 0

# Report dlls referenced with more than one version.
$report = [System.Text.StringBuilder]::new()

foreach ($dll in $referencedFileVersionsPerDll.GetEnumerator()) {
    if ($dll.Value.Count -gt 1) {
        [void]$report.AppendLine($dll.Key)
        foreach ($version in $dll.Value.GetEnumerator()) {
            [void]$report.AppendLine("`t" + $version.Key)
            foreach ($file in $version.Value) {
                [void]$report.AppendLine("`t`t" + $file)
            }
        }
        $totalFailures++
    }
}

if ($report.Length -gt 0) {
    Write-Host $report.ToString() -NoNewline
}

if ($totalFailures -gt 0) {
    Write-Host -ForegroundColor Red "Detected  $totalFailures  libraries that are mentioned with different version across the dependencies.`r`n"
    exit 1
}

Write-Host -ForegroundColor Green "All  $($referencedFileVersionsPerDll.Count)  libraries are mentioned with the same version across the dependencies.`r`n"
exit 0