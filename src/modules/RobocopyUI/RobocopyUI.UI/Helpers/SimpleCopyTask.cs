// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RobocopyUI.Models;
using RobocopyUI.Services.AI;

namespace RobocopyUI.Helpers;

/// <summary>
/// Expands Simple-mode jobs into robocopy switches and infers Simple-mode state from a full option list.
/// </summary>
public static class SimpleCopyTask
{
    /// <summary>Default <c>/MT</c> thread count when Copy faster is on.</summary>
    public const int DefaultThreadCount = 8;

    /// <summary>Default <c>/R</c> retry count when Retry failed files is on.</summary>
    public const int DefaultRetryCount = 3;

    /// <summary>Default <c>/W</c> wait in seconds when Retry failed files is on.</summary>
    public const int DefaultRetryWaitSeconds = 5;

    /// <summary>
    /// Switches Simple mode owns. Any other switch is preserved as an Advanced extra when merging.
    /// </summary>
    public static readonly IReadOnlySet<string> OwnedSwitchNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "/E", "/S", "/MIR", "/MOVE", "/MOV",
        "/SEC", "/COPYALL",
        "/XO", "/XF", "/XD",
        "/MT", "/R", "/W",
        "/L", "/LOG",
    };

    /// <summary>
    /// Builds the switch list for a Simple job, then prunes implied duplicates.
    /// </summary>
    public static List<RobocopyPlanOption> Expand(SimpleCopyTaskKind kind, SimpleCopyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var expanded = new List<RobocopyPlanOption>();

        switch (kind)
        {
            case SimpleCopyTaskKind.CopyFilesAndFolders:
                expanded.Add(new RobocopyPlanOption("/E", string.Empty));
                break;
            case SimpleCopyTaskKind.CopySkipEmptyFolders:
                expanded.Add(new RobocopyPlanOption("/S", string.Empty));
                break;
            case SimpleCopyTaskKind.Mirror:
                expanded.Add(new RobocopyPlanOption("/MIR", string.Empty));
                break;
            case SimpleCopyTaskKind.MoveEverything:
                expanded.Add(new RobocopyPlanOption("/MOVE", string.Empty));
                break;
            case SimpleCopyTaskKind.MoveFilesOnly:
                expanded.Add(new RobocopyPlanOption("/MOV", string.Empty));
                break;
            case SimpleCopyTaskKind.CopyThisFolderOnly:
                break;
        }

        if (options.KeepAllProperties)
        {
            expanded.Add(new RobocopyPlanOption("/COPYALL", string.Empty));
        }
        else if (options.KeepPermissions)
        {
            expanded.Add(new RobocopyPlanOption("/SEC", string.Empty));
        }

        if (options.SkipNewerAtDestination)
        {
            expanded.Add(new RobocopyPlanOption("/XO", string.Empty));
        }

        AddTextSwitch(expanded, "/XF", options.ExcludeFileTypes);
        AddTextSwitch(expanded, "/XD", options.ExcludeFolders);

        if (options.CopyFaster)
        {
            expanded.Add(new RobocopyPlanOption("/MT", FormatCount(options.ThreadCount, DefaultThreadCount)));
        }

        if (options.RetryFailedFiles)
        {
            expanded.Add(new RobocopyPlanOption("/R", FormatCount(options.RetryCount, DefaultRetryCount)));
            expanded.Add(new RobocopyPlanOption("/W", FormatCount(options.RetryWaitSeconds, DefaultRetryWaitSeconds)));
        }

        if (options.PreviewOnly)
        {
            expanded.Add(new RobocopyPlanOption("/L", string.Empty));
        }

        if (options.WriteLog)
        {
            AddTextSwitch(expanded, "/LOG", options.LogPath);
        }

        RobocopyCommand.Prune(expanded);
        return expanded;
    }

    /// <summary>
    /// Replaces Simple-owned switches on an existing list, keeping any Advanced extras.
    /// </summary>
    public static List<RobocopyPlanOption> Merge(IEnumerable<RobocopyPlanOption> existing, SimpleCopyTaskKind kind, SimpleCopyOptions options)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var merged = existing
            .Where(option => !OwnedSwitchNames.Contains(option.Name))
            .ToList();

        merged.AddRange(Expand(kind, options));
        RobocopyCommand.Prune(merged);
        return merged;
    }

    /// <summary>
    /// Reads a Simple-mode snapshot back from a full option list.
    /// </summary>
    public static SimpleCopySnapshot Infer(IEnumerable<RobocopyPlanOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var list = options.ToList();
        bool Has(string name) => list.Any(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase));
        string Value(string name) => list.FirstOrDefault(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

        var kind = SimpleCopyTaskKind.CopyThisFolderOnly;
        if (Has("/MIR"))
        {
            kind = SimpleCopyTaskKind.Mirror;
        }
        else if (Has("/MOVE"))
        {
            kind = SimpleCopyTaskKind.MoveEverything;
        }
        else if (Has("/MOV"))
        {
            kind = SimpleCopyTaskKind.MoveFilesOnly;
        }
        else if (Has("/S"))
        {
            kind = SimpleCopyTaskKind.CopySkipEmptyFolders;
        }
        else if (Has("/E"))
        {
            kind = SimpleCopyTaskKind.CopyFilesAndFolders;
        }

        var simple = new SimpleCopyOptions
        {
            KeepAllProperties = Has("/COPYALL"),
            KeepPermissions = Has("/SEC") && !Has("/COPYALL"),
            SkipNewerAtDestination = Has("/XO"),
            ExcludeFileTypes = Value("/XF"),
            ExcludeFolders = Value("/XD"),
            CopyFaster = Has("/MT"),
            ThreadCount = ParseCount(Value("/MT"), DefaultThreadCount),
            RetryFailedFiles = Has("/R") || Has("/W"),
            RetryCount = ParseCount(Value("/R"), DefaultRetryCount),
            RetryWaitSeconds = ParseCount(Value("/W"), DefaultRetryWaitSeconds),
            PreviewOnly = Has("/L"),
            WriteLog = Has("/LOG"),
            LogPath = Value("/LOG"),
        };

        var hasAdditional = list.Any(option => !OwnedSwitchNames.Contains(option.Name));

        return new SimpleCopySnapshot(kind, simple, hasAdditional);
    }

    private static void AddTextSwitch(List<RobocopyPlanOption> options, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            options.Add(new RobocopyPlanOption(name, value.Trim()));
        }
    }

    private static string FormatCount(int value, int fallback)
    {
        var count = value > 0 ? value : fallback;
        return count.ToString(CultureInfo.InvariantCulture);
    }

    private static int ParseCount(string value, int fallback)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }
}
