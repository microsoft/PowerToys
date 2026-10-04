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
[CmdletBinding()]
Param(
    [Parameter(Mandatory = $True, Position = 1)]
    [string]$targetDir
)

$references = @(
    'System.Runtime',
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
                        if (!entry.Name.EndsWith(".dll", StringComparison.Ordinal))
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

    // Returns: DllName > fileVersion > deps.json file names that reference it.
    public static SortedDictionary<string, SortedDictionary<string, List<string>>> Collect(string root)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true };
        var files = new List<string>();
        foreach (var f in Directory.EnumerateFiles(root, "*.deps.json", options))
        {
            if (!IsExcluded(f, Path.GetFileName(f))) files.Add(f);
        }

        // Parse in parallel, then merge sequentially in file order so output is deterministic.
        var perFile = new List<KeyValuePair<string, string>>[files.Count];
        Parallel.For(0, files.Count, i => perFile[i] = ReadFile(files[i]));

        var all = new SortedDictionary<string, SortedDictionary<string, List<string>>>(StringComparer.Ordinal);
        for (int i = 0; i < files.Count; i++)
        {
            string fileName = Path.GetFileName(files[i]);
            foreach (var kv in perFile[i])
            {
                if (!all.TryGetValue(kv.Key, out var versions))
                {
                    versions = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
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

        return all;
    }
}
'@
}

$referencedFileVersionsPerDll = [DepsJsonAudit]::Collect((Resolve-Path $targetDir).Path)
$totalFailures = 0

# Report dlls referenced with more than one version.
foreach ($dll in $referencedFileVersionsPerDll.GetEnumerator()) {
    if ($dll.Value.Count -gt 1) {
        Write-Host $dll.Key
        foreach ($version in $dll.Value.GetEnumerator()) {
            Write-Host "`t" $version.Key
            foreach ($file in $version.Value) {
                Write-Host "`t`t" $file
            }
        }
        $totalFailures++
    }
}

if ($totalFailures -gt 0) {
    Write-Host -ForegroundColor Red "Detected $totalFailures libraries that are mentioned with different version across the dependencies.`r`n"
    exit 1
}

Write-Host -ForegroundColor Green "All $($referencedFileVersionsPerDll.Count) libraries are mentioned with the same version across the dependencies.`r`n"
exit 0
