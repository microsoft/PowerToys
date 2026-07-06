// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32;
using PowerScripts.Core.Manifest;
using PowerScripts.Core.Registry;

namespace PowerScripts.Host;

/// <summary>
/// Registers / unregisters the Explorer right-click "PowerScript" cascading submenu for file
/// PowerScripts. For each file extension declared by a script, it writes a per-user shell verb under
/// <c>HKCU\Software\Classes\SystemFileAssociations\&lt;ext&gt;\shell\PowerScripts</c> whose nested
/// sub-verbs (one per matching script) invoke <c>PowerScripts.Host.exe run &lt;id&gt; --files "%1"</c>.
///
/// This is the prototype's context-menu surface: it needs no COM DLL and is driven entirely by the
/// script registry, so right-click works immediately and reflects the installed scripts. The
/// PowerScripts module (runner) calls <c>shell-install</c> on enable and <c>shell-uninstall</c> on
/// disable.
/// </summary>
internal static class ShellRegistration
{
    private const string RootVerb = "PowerScripts";
    private const string MenuLabel = "PowerScripts";
    private const string ClassesRoot = @"Software\Classes\SystemFileAssociations";

    /// <summary>Marker value so uninstall only removes keys this tool created.</summary>
    private const string OwnerMarkerName = "PowerScriptsOwned";

    public static int Install(ScriptRegistry registry, string hostExePath)
    {
        // Group file scripts by each declared extension (skip the "*" wildcard for the static menu).
        var byExtension = new Dictionary<string, List<PowerScriptManifest>>(StringComparer.OrdinalIgnoreCase);
        foreach (var script in registry.Scripts.Where(s => s.Kind == ScriptKind.File && s.Input is not null))
        {
            foreach (var rawExt in script.Input!.Extensions)
            {
                if (rawExt == "*")
                {
                    continue;
                }

                var ext = rawExt.StartsWith('.') ? rawExt : "." + rawExt;
                if (!byExtension.TryGetValue(ext, out var list))
                {
                    list = new List<PowerScriptManifest>();
                    byExtension[ext] = list;
                }

                list.Add(script);
            }
        }

        if (byExtension.Count == 0)
        {
            Console.WriteLine("shell-install: no file scripts with concrete extensions to register.");
            return 0;
        }

        foreach (var (ext, scripts) in byExtension)
        {
            RemoveVerbForExtension(ext);

            var verbPath = $@"{ClassesRoot}\{ext}\shell\{RootVerb}";
            using var verbKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(verbPath)!;
            verbKey.SetValue("MUIVerb", MenuLabel);
            verbKey.SetValue(OwnerMarkerName, 1, RegistryValueKind.DWord);

            // Presence of "SubCommands" makes Explorer render the nested \shell verbs as a submenu.
            verbKey.SetValue("SubCommands", string.Empty);

            using var subShell = verbKey.CreateSubKey("shell")!;
            foreach (var script in scripts)
            {
                using var item = subShell.CreateSubKey(script.Id)!;
                item.SetValue("MUIVerb", script.Name);
                using var command = item.CreateSubKey("command")!;
                command.SetValue(null, $"\"{hostExePath}\" run {script.Id} --files \"%1\"");
            }

            Console.WriteLine($"  registered {scripts.Count} script(s) for {ext}");
        }

        Console.WriteLine($"shell-install: done ({byExtension.Count} extension(s)).");
        return 0;
    }

    public static int Uninstall(ScriptRegistry registry)
    {
        // Remove for every extension currently declared, plus best-effort sweep is unnecessary since
        // we only ever create owned keys.
        var extensions = registry.Scripts
            .Where(s => s.Kind == ScriptKind.File && s.Input is not null)
            .SelectMany(s => s.Input!.Extensions)
            .Where(e => e != "*")
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var ext in extensions)
        {
            RemoveVerbForExtension(ext);
        }

        Console.WriteLine("shell-uninstall: done.");
        return 0;
    }

    private static void RemoveVerbForExtension(string ext)
    {
        var verbParent = $@"{ClassesRoot}\{ext}\shell";
        using var shellKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(verbParent, writable: true);
        if (shellKey is null)
        {
            return;
        }

        // Only delete the verb if we own it.
        using (var verbKey = shellKey.OpenSubKey(RootVerb))
        {
            if (verbKey is null)
            {
                return;
            }

            if (verbKey.GetValue(OwnerMarkerName) is null)
            {
                return;
            }
        }

        shellKey.DeleteSubKeyTree(RootVerb, throwOnMissingSubKey: false);
    }
}
