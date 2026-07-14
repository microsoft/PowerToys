// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Manifest;

/// <summary>
/// Derives the surfaces a PowerScript should appear on from its <see cref="ScriptKind"/> when the
/// author didn't declare any. Per design feedback, script authors shouldn't have to hand-list which
/// PowerToys expose a script — that's brittle and not future-proof — so the ingestion side infers a
/// sensible default from the script's kind:
/// <list type="bullet">
///   <item><description><see cref="ScriptKind.File"/> (needs an input object) =&gt; the right-click context menu.</description></item>
///   <item><description><see cref="ScriptKind.System"/> (an action, no input) =&gt; Keyboard Manager and the Command Palette.</description></item>
/// </list>
/// Inference only fills an <em>empty</em> surface list, so any script that explicitly declares
/// surfaces keeps exactly what it declared and existing samples are unaffected.
/// </summary>
public static class SurfaceInference
{
    public const string ContextMenu = "contextMenu";
    public const string KeyboardManager = "keyboardManager";
    public const string CommandPalette = "commandPalette";

    /// <summary>
    /// Fills <see cref="PowerScriptManifest.Surfaces"/> with kind-derived defaults when it is empty.
    /// No-ops when the author already declared one or more surfaces.
    /// </summary>
    public static void ApplyDefaults(PowerScriptManifest manifest)
    {
        if (manifest.Surfaces.Count > 0)
        {
            return;
        }

        manifest.Surfaces.AddRange(InferSurfaces(manifest.Kind));
    }

    /// <summary>Returns the default surfaces for a script <paramref name="kind"/>.</summary>
    public static IReadOnlyList<string> InferSurfaces(ScriptKind kind) => kind switch
    {
        ScriptKind.File => new[] { ContextMenu },
        ScriptKind.System => new[] { KeyboardManager, CommandPalette },
        _ => Array.Empty<string>(),
    };
}
