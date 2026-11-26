// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using PowerScripts.Core.Manifest;

namespace PowerScripts.Host;

/// <summary>
/// Shows the trust-on-first-use consent dialog. Because every surface (context menu, Keyboard
/// Manager, agents) funnels through <c>Host run &lt;id&gt;</c>, this single prompt is the one place a
/// user sees, in plain language, exactly what a script is and what it declares it can do before it
/// ever executes. A native top-most MessageBox is used so the prompt is visible even when the Host
/// was launched hidden by a surface.
/// </summary>
internal static class ConsentPrompt
{
    private const uint MBYESNO = 0x00000004;
    private const uint MBICONWARNING = 0x00000030;
    private const uint MBDEFBUTTON2 = 0x00000100;
    private const uint MBTOPMOST = 0x00040000;
    private const uint MBSETFOREGROUND = 0x00010000;
    private const int IDYES = 6;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    /// <summary>
    /// Returns true if the user approves running this script. Presents the script's identity,
    /// provenance and declared capabilities so the decision is informed.
    /// </summary>
    public static bool Confirm(PowerScriptManifest manifest)
    {
        var capabilities = manifest.Capabilities.Count > 0
            ? string.Join(", ", manifest.Capabilities)
            : "(none declared)";

        var publisher = string.IsNullOrWhiteSpace(manifest.Publisher) ? "(unknown)" : manifest.Publisher;
        var source = string.IsNullOrWhiteSpace(manifest.Source) ? "(local)" : manifest.Source;
        var hostCliNotice = manifest.HostCli is { } hostCli
            ? $"Approved host CLI recipe: {hostCli.Id}\n" +
                "With MXC on, script code stays contained, but this approved action runs on the host with standard-user permissions, " +
                "outside the script's filesystem, network and desktop restrictions. Review the descriptor's hostCli bindings before approval.\n\n"
            : string.Empty;

        var text =
            $"A PowerScript is about to run for the first time (or its contents changed).\n\n" +
            $"Name:          {manifest.Name}\n" +
            $"Id:               {manifest.Id}\n" +
            $"Publisher:     {publisher}\n" +
            $"Source:         {source}\n" +
            $"Runtime:       {manifest.Runtime}\n" +
            $"Declares:      {capabilities}\n" +
            $"Script file:    {manifest.EntryFullPath}\n\n" +
            hostCliNotice +
            "Only allow scripts you trust. Allow this script to run?";

        var result = MessageBoxW(
            IntPtr.Zero,
            text,
            "PowerScripts — allow this script to run?",
            MBYESNO | MBICONWARNING | MBDEFBUTTON2 | MBTOPMOST | MBSETFOREGROUND);

        return result == IDYES;
    }
}
