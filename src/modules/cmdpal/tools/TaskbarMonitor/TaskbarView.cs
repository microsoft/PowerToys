// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace TaskbarMonitor;

/// <summary>
/// Renders <see cref="TaskbarSnapshot"/> data to the alternate screen buffer.
/// </summary>
public static class TaskbarView
{
    private const string Esc = "\x1b";

    public static void EnterAlternateScreen()
    {
        Console.Write($"{Esc}[?1049h"); // enter alt buffer
        Console.Write($"{Esc}[?25l");   // hide cursor
        Console.CursorVisible = false;
    }

    public static void LeaveAlternateScreen()
    {
        Console.Write($"{Esc}[?25h");   // show cursor
        Console.Write($"{Esc}[?1049l"); // leave alt buffer
    }

    public static void Render(List<TaskbarSnapshot> snapshots, List<TaskbarSnapshot>? previous)
    {
        if (previous != null && snapshots.SequenceEqual(previous))
        {
            return;
        }

        var sb = new StringBuilder(1024);

        sb.Append($"{Esc}[H");   // home
        sb.Append($"{Esc}[2J");  // clear

        sb.Append($"{Esc}[1m┌──────────────────────────────────────────┐{Esc}[0m\n");
        sb.Append($"{Esc}[1m│         Taskbar Monitor                 │{Esc}[0m\n");
        sb.Append($"{Esc}[1m└──────────────────────────────────────────┘{Esc}[0m\n");
        sb.AppendLine();

        if (snapshots.Count == 0)
        {
            sb.Append($"  {Esc}[33mNo taskbar found.{Esc}[0m\n");
        }
        else
        {
            for (var i = 0; i < snapshots.Count; i++)
            {
                RenderOne(sb, snapshots[i], i);
            }
        }

        if (previous != null && !snapshots.SequenceEqual(previous))
        {
            sb.Append($"  {Esc}[33m● Changed{Esc}[0m\n");
        }
        else
        {
            sb.Append($"  {Esc}[32m● Up to date{Esc}[0m\n");
        }

        sb.AppendLine();
        sb.Append($"  {Esc}[2mEvent-driven (SetWinEventHook). Press Ctrl+C to exit.{Esc}[0m\n");

        Console.Write(sb);
    }

    private static void RenderOne(StringBuilder sb, TaskbarSnapshot s, int index)
    {
        var label = s.IsPrimary ? "Primary" : $"Secondary #{index}";
        sb.Append($"  {Esc}[1m── {label} ──{Esc}[0m\n");
        sb.AppendLine();

        var scale = s.ScaleFactor;
        sb.Append($"  {Esc}[36mSize:{Esc}[0m      {s.TaskbarWidth} × {s.TaskbarHeight} px");
        sb.Append($"  ({s.TaskbarWidth / scale:F0} × {s.TaskbarHeight / scale:F0} DIPs)\n");
        sb.Append($"  {Esc}[36mDPI:{Esc}[0m       {s.Dpi} ({scale:F2}x)\n");
        sb.Append($"  {Esc}[36mPosition:{Esc}[0m  ");

        if (s.IsBottom)
        {
            sb.Append($"{Esc}[32mBottom{Esc}[0m ✓\n");
        }
        else
        {
            sb.Append($"{Esc}[33mNot bottom{Esc}[0m (side/top metrics skipped)\n");
            sb.AppendLine();
            return;
        }

        sb.AppendLine();

        sb.Append($"  {Esc}[36mButtons:{Esc}[0m    {s.ButtonsWidth} px");
        sb.Append($"  ({s.ButtonsWidth / scale:F0} DIPs)");
        sb.Append($"  [{s.ButtonCount} children]\n");

        sb.Append($"  {Esc}[36mTray:{Esc}[0m       {s.TrayWidth} px");
        sb.Append($"  ({s.TrayWidth / scale:F0} DIPs)\n");

        sb.AppendLine();

        // Visual bar
        var barWidth = Math.Min(60, Console.WindowWidth - 4);
        if (barWidth > 10 && s.TaskbarWidth > 0)
        {
            var btnCols = (int)Math.Round((double)s.ButtonsWidth / s.TaskbarWidth * barWidth);
            var trayCols = (int)Math.Round((double)s.TrayWidth / s.TaskbarWidth * barWidth);
            var midCols = Math.Max(0, barWidth - btnCols - trayCols);

            sb.Append("  ");
            sb.Append($"{Esc}[44m{new string('█', btnCols)}{Esc}[0m");
            sb.Append($"{Esc}[100m{new string('░', midCols)}{Esc}[0m");
            sb.Append($"{Esc}[45m{new string('█', trayCols)}{Esc}[0m");
            sb.AppendLine();

            sb.Append($"  {Esc}[34mbuttons{Esc}[0m");
            var pad = barWidth - 7 - 4;
            if (pad > 0)
            {
                sb.Append(new string(' ', pad));
            }

            sb.Append($"{Esc}[35mtray{Esc}[0m");
            sb.AppendLine();
        }

        sb.AppendLine();
    }
}
