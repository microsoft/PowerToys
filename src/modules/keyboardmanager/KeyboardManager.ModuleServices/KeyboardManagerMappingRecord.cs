// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace KeyboardManager.ModuleServices;

public enum KeyboardManagerMappingKind
{
    SingleKeyToKey,
    SingleKeyToShortcut,
    SingleKeyToText,
    ShortcutToKey,
    ShortcutToShortcut,
    ShortcutToText,
    ShortcutToProgram,
    ShortcutToUri,
}

public sealed class KeyboardManagerMappingRecord
{
    public string Id { get; init; } = string.Empty;

    public KeyboardManagerMappingKind Kind { get; init; }

    public string TriggerDisplay { get; init; } = string.Empty;

    public string TargetDisplay { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public bool IsAppSpecific { get; init; }

    public string TargetApp { get; init; } = string.Empty;

    public string OriginalKeys { get; init; } = string.Empty;

    public string TargetKeys { get; init; } = string.Empty;

    public string TargetText { get; init; } = string.Empty;

    public string ProgramPath { get; init; } = string.Empty;

    public string ProgramArgs { get; init; } = string.Empty;

    public string StartInDirectory { get; init; } = string.Empty;

    public int Elevation { get; init; }

    public int IfRunningAction { get; init; }

    public int Visibility { get; init; }

    public string UriToOpen { get; init; } = string.Empty;

    public bool IsExecutable => Kind is KeyboardManagerMappingKind.ShortcutToProgram or KeyboardManagerMappingKind.ShortcutToUri;
}
