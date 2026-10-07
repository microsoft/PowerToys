// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Catalog;

namespace Microsoft.CmdPal.Ext.Apps.Win32;

/// <summary>
/// Holds transient file-read metadata before the source captures an immutable catalog payload.
/// </summary>
internal sealed class Win32AppMetadata
{
    /// <summary>Gets or sets the source filename stem retained for released command IDs.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the Shell display name in the current Windows display language.</summary>
    /// <remarks>Consumers fall back to <see cref="Name"/> when this value is empty or whitespace.</remarks>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the native icon location, which may include a resource index.</summary>
    public string IconLocation { get; set; } = string.Empty;

    /// <summary>Gets or sets the shortcut description or the executable's file description, when available.</summary>
    /// <remarks>An explicit shortcut description takes precedence over its target's file description.</remarks>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the discovered entry's path, its resolved shortcut target, or a supported game URL.</summary>
    /// <remarks>Execution aliases retain their own path here; their resolved target is held in <see cref="AppExecutionAlias"/>.</remarks>
    public string TargetPath { get; set; } = string.Empty;

    /// <summary>Gets <see cref="TargetPath"/> with the comparison and normalization rules for its target kind.</summary>
    /// <remarks>Internet shortcut URLs preserve case; file paths use case-insensitive comparison.</remarks>
    internal LaunchTarget Target => AppType == Win32AppType.InternetShortcutApplication
        ? LaunchTarget.Url(TargetPath)
        : LaunchTarget.FilePath(TargetPath);

    /// <summary>Gets or sets the Shell-localized target path used as search metadata.</summary>
    public string LocalizedTargetPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the directory containing the discovered entry, not its target or working directory.</summary>
    public string ParentDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the source filename, including its extension.</summary>
    public string SourceFilename { get; set; } = string.Empty;

    /// <summary>Gets or sets the Shell-localized source filename used as search metadata.</summary>
    public string LocalizedSourceFilename { get; set; } = string.Empty;

    /// <summary>Gets or sets the original shortcut path, retained for activation.</summary>
    /// <remarks>Empty for direct entries and shortcuts without a resolved filesystem target.</remarks>
    public string LnkFilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the resolved shortcut target's filename, including its extension.</summary>
    public string TargetFilename { get; set; } = string.Empty;

    /// <summary>Gets or sets the Shell-localized filename of the resolved shortcut target used as search metadata.</summary>
    public string LocalizedTargetFilename { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the reader accepted this candidate for indexing.</summary>
    /// <remarks>This does not guarantee successful activation or that the target will remain available.</remarks>
    public bool Valid { get; set; }

    /// <summary>Gets whether a transient read failure prevented this candidate from being indexed.</summary>
    /// <remarks>The source retains any previous item and schedules a bounded retry.</remarks>
    internal bool RetryableReadFailure { get; init; }

    /// <summary>Gets whether a read failure requires retaining this candidate's previous catalog item.</summary>
    internal bool Unreadable { get; init; }

    /// <summary>Gets or sets the command-line arguments stored in a resolved shortcut.</summary>
    /// <remarks>Shortcut activation applies these arguments; consumers retain them for identity, matching and diagnostics.</remarks>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>Gets or sets the shortcut's configured working directory, which may be relative or empty.</summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    /// <summary>Gets or sets the packaged identity and target metadata read from an app execution alias reparse point.</summary>
    /// <remarks>Null when the entry is not an execution alias or its alias metadata could not be read.</remarks>
    internal ReparsePoint.AppExecutionAliasInfo? AppExecutionAlias { get; set; }

    /// <summary>Gets or sets the packaged application's AUMID obtained from a shortcut or app execution alias.</summary>
    internal string PackagedAppUserModelId { get; set; } = string.Empty;

    /// <summary>Gets or sets the explicit Windows application ID declared by this shortcut.</summary>
    /// <remarks>This ID can identify an unpackaged desktop app and does not imply packaged activation.</remarks>
    internal string ExplicitAppUserModelId { get; set; } = string.Empty;

    /// <summary>Gets or sets the reader's classification of the launch target, including PATH commands.</summary>
    public Win32AppType AppType { get; set; }
}
