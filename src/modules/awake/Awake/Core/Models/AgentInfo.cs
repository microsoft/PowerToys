// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Awake.Core.Models
{
    /// <summary>
    /// Activity state of a CLI agent session, mirroring Intelligent Terminal's
    /// <c>AgentStatus</c> registry (see <c>tools/wta/src/agent_sessions.rs</c>). Only
    /// <see cref="Working"/> holds the machine awake; every other state lets it sleep.
    /// </summary>
    public enum AgentActivity
    {
        Unknown,
        Idle,
        Working,
        Attention,
        Error,
        Ended,
    }

    /// <summary>
    /// A CLI agent session surfaced in the flyout's "Agents" picker. Populated from the
    /// <c>agent-status.json</c> snapshot that Intelligent Terminal writes; the display-only
    /// members (label, glyph, status brush) are computed on the UI thread during binding.
    /// </summary>
    public sealed class AgentInfo
    {
        public string Id { get; init; } = string.Empty;

        // Raw source id from the snapshot: copilot | claude | codex | gemini | unknown.
        public string Source { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Cwd { get; init; } = string.Empty;

        public AgentActivity Status { get; init; } = AgentActivity.Unknown;

        public string? CurrentTool { get; init; }

        public string? AttentionReason { get; init; }

        public bool IsWorking => Status == AgentActivity.Working;

        public bool IsLive => Status is not (AgentActivity.Ended or AgentActivity.Unknown);

        // Friendly product name for the agent CLI.
        public string SourceLabel => Source?.ToLowerInvariant() switch
        {
            "copilot" => "GitHub Copilot CLI",
            "claude" => "Claude Code",
            "codex" => "Codex CLI",
            "gemini" => "Gemini CLI",
            _ => string.IsNullOrWhiteSpace(Source) ? "Agent" : Source,
        };

        public string DisplayName => string.IsNullOrWhiteSpace(Title) ? SourceLabel : Title;

        // Second line: the project folder, plus the running tool while working.
        public string Subtitle
        {
            get
            {
                string folder = string.IsNullOrWhiteSpace(Cwd) ? SourceLabel : SafeFolderName(Cwd);

                if (Status == AgentActivity.Working && !string.IsNullOrWhiteSpace(CurrentTool))
                {
                    return $"{folder}  •  {CurrentTool}";
                }

                if (Status == AgentActivity.Attention && !string.IsNullOrWhiteSpace(AttentionReason))
                {
                    return $"{folder}  •  {AttentionReason}";
                }

                return folder;
            }
        }

        public string StatusText => Status switch
        {
            AgentActivity.Working => "Working",
            AgentActivity.Attention => "Waiting for you",
            AgentActivity.Error => "Error",
            AgentActivity.Idle => "Idle",
            AgentActivity.Ended => "Ended",
            _ => "Unknown",
        };

        // Segoe Fluent glyph per agent source (generic robot fallback).
        public string Glyph => "\uE99A";

        public Brush StatusBrush => new SolidColorBrush(StatusColor);

        private Color StatusColor => Status switch
        {
            AgentActivity.Working => Color.FromArgb(0xFF, 0x2E, 0xA0, 0x43),   // green
            AgentActivity.Attention => Color.FromArgb(0xFF, 0xE8, 0xA3, 0x17), // amber
            AgentActivity.Error => Color.FromArgb(0xFF, 0xD1, 0x3A, 0x3A),     // red
            AgentActivity.Idle => Colors.Gray,
            _ => Colors.Gray,
        };

        private static string SafeFolderName(string path)
        {
            try
            {
                string trimmed = path.TrimEnd('\\', '/');
                string name = Path.GetFileName(trimmed);
                return string.IsNullOrWhiteSpace(name) ? trimmed : name;
            }
            catch
            {
                return path;
            }
        }

        public static AgentActivity ParseStatus(string? value) => value?.ToLowerInvariant() switch
        {
            "working" => AgentActivity.Working,
            "attention" => AgentActivity.Attention,
            "error" => AgentActivity.Error,
            "idle" => AgentActivity.Idle,
            "ended" => AgentActivity.Ended,
            _ => AgentActivity.Unknown,
        };
    }
}
