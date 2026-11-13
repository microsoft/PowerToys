// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

/// <summary>Windows and a keyboard that record what was sent. Command Palette is window 1; the user's app is window 2.</summary>
internal sealed class FakeDesktop : IDesktop
{
    public const nint CommandPaletteWindow = 1;
    public const nint AppWindow = 2;
    public const nint OtherWindow = 3;
    public const uint CommandPaletteProcess = 100;
    public const uint AppProcess = 200;
    public const uint OtherProcess = 300;

    public nint ForegroundWindow { get; set; } = CommandPaletteWindow;

    public uint CurrentProcessId => CommandPaletteProcess;

    /// <summary>Gets or sets the window that comes to the front once Command Palette hides.</summary>
    public nint WindowAfterHide { get; set; } = AppWindow;

    public List<ushort> Held { get; } = new();

    public bool CanDriveTarget { get; set; } = true;

    public bool TakingMessages { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the user's app is Windows Terminal.</summary>
    public bool AppIsWindowsTerminal { get; set; }

    /// <summary>Gets or sets a value indicating whether the user's app is a console window (conhost).</summary>
    public bool AppIsConsole { get; set; }

    /// <summary>Gets the accepted event counts to return for the next sends. Later sends accept the whole batch.</summary>
    public Queue<uint> SendCounts { get; } = new();

    /// <summary>Gets every requested batch, including events a partial send didn't accept.</summary>
    public List<IReadOnlyList<KeyStroke>> Sent { get; } = new();

    /// <summary>Gets the prefixes accepted by this fake, separately from the requested batches.</summary>
    public List<IReadOnlyList<KeyStroke>> Accepted { get; } = new();

    public int HideRequests { get; private set; }

    public IReadOnlyList<ushort> HeldModifiers() => Held.ToArray();

    public uint ProcessIdOf(nint window) => window switch
    {
        CommandPaletteWindow => CommandPaletteProcess,
        AppWindow => AppProcess,
        OtherWindow => OtherProcess,
        _ => 0,
    };

    public bool CanDrive(nint window) => CanDriveTarget;

    public bool IsTakingMessages(nint window) => TakingMessages;

    public bool IsWindowsTerminal(nint window) => window == AppWindow && AppIsWindowsTerminal;

    public bool IsConsole(nint window) => window == AppWindow && AppIsConsole;

    public uint Send(IReadOnlyList<KeyStroke> strokes)
    {
        Sent.Add(strokes);
        var sent = SendCounts.TryDequeue(out var count) ? count : (uint)strokes.Count;
        Accepted.Add(strokes.Take((int)sent).ToArray());
        return sent;
    }

    public void HideCommandPalette()
    {
        HideRequests++;
        ForegroundWindow = WindowAfterHide;
    }

    /// <summary>Gets a value indicating whether Ctrl+V was sent.</summary>
    public bool Pasted => Accepted.Any(batch =>
        batch.Contains(KeyStroke.KeyDown(KeyStrokes.Control)) && batch.Contains(KeyStroke.KeyDown(KeyStrokes.V)));

    /// <summary>Gets the text typed: Unicode characters, Enter as a line break and Tab as a tab.</summary>
    public string Typed()
    {
        var typed = new StringBuilder();
        foreach (var stroke in Accepted.SelectMany(batch => batch).Where(stroke => !stroke.Up))
        {
            if (stroke.Character != '\0')
            {
                typed.Append(stroke.Character);
            }
            else if (stroke.VirtualKey == KeyStrokes.Return)
            {
                typed.Append('\n');
            }
            else if (stroke.VirtualKey == KeyStrokes.Tab)
            {
                typed.Append('\t');
            }
        }

        return typed.ToString();
    }
}
