// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;

namespace PowerToys.DSC.Models.KeyboardManager;

/// <summary>
/// Converts between the friendly <see cref="KbmProfileModel"/> used by the DSC
/// profile resource and the <see cref="KeyboardManagerProfile"/> stored in the
/// Keyboard Manager profile file. The conversion mirrors the exact JSON shape
/// written by the C++ editor (MappingConfiguration::SaveSettingsToFile) so
/// that DSC-written profiles are indistinguishable from editor-written ones.
/// </summary>
public static class KbmProfileConverter
{
    // Dummy text written on run-program/open-URI entries for backwards
    // compatibility; see MappingConfiguration::SaveSettingsToFile.
    private const string UnsupportedText = "*Unsupported*";

    private const int OperationTypeRemapShortcut = 0;
    private const int OperationTypeRunProgram = 1;
    private const int OperationTypeOpenUri = 2;

    // Friendly names for the Shortcut.h enums, indexed by their numeric value.
    private static readonly string[] _elevationNames = ["normal", "elevated", "differentUser"];
    private static readonly string[] _ifRunningNames = ["showWindow", "startAnother", "doNothing", "close", "endTask", "closeAndEndTask"];
    private static readonly string[] _windowStyleNames = ["normal", "hidden", "minimized", "maximized"];

    /// <summary>
    /// Validates the friendly model and returns the list of validation
    /// errors; an empty list means the model is valid. The messages are
    /// intentionally not localized: they quote JSON property paths and key
    /// names that must match the configuration document verbatim, and are
    /// presented inside the localized InvalidProfileError message frame.
    /// </summary>
    /// <param name="model">The friendly model to validate.</param>
    /// <returns>The list of validation errors.</returns>
    public static IList<string> Validate(KbmProfileModel model)
    {
        var errors = new List<string>();
        var seenKeys = new HashSet<uint>();
        var seenShortcuts = new HashSet<(string App, string From)>();

        for (var i = 0; i < model.Keys.Count; i++)
        {
            var entry = model.Keys[i];
            var context = $"keys[{i.ToString(CultureInfo.InvariantCulture)}]";

            var targetCount = (entry.To != null ? 1 : 0) + (entry.ToText != null ? 1 : 0);
            if (targetCount != 1)
            {
                errors.Add($"{context} must set exactly one of 'to' or 'toText'");
            }

            if (!KbmShortcutParser.TryParseKey(entry.From, out var from, out var error))
            {
                errors.Add($"{context}.from: {error}");
            }
            else if (from.Keys[0] == KbmKeyNames.VkDisabled)
            {
                errors.Add($"{context}.from: 'Disable' cannot be remapped");
            }
            else if (from.Keys[0] is 16 or 17 or 18 or KbmKeyNames.VkWinBoth)
            {
                errors.Add($"{context}.from: generic modifiers must use a left or right variant");
            }
            else if (!seenKeys.Add(from.Keys[0]))
            {
                errors.Add($"{context}.from: key '{KbmKeyNames.GetName(from.Keys[0])}' is remapped more than once");
            }

            if (entry.To != null && !TryParseTarget(entry.To, out _, out error))
            {
                errors.Add($"{context}.to: {error}");
            }

            if (entry.ToText != null && entry.ToText.Length == 0)
            {
                errors.Add($"{context}.toText must not be empty");
            }
        }

        for (var i = 0; i < model.Shortcuts.Count; i++)
        {
            var entry = model.Shortcuts[i];
            var context = $"shortcuts[{i.ToString(CultureInfo.InvariantCulture)}]";

            var targetCount = (entry.To != null ? 1 : 0) + (entry.ToText != null ? 1 : 0) +
                (entry.RunProgram != null ? 1 : 0) + (entry.OpenUri != null ? 1 : 0);
            if (targetCount != 1)
            {
                errors.Add($"{context} must set exactly one of 'to', 'toText', 'runProgram', or 'openUri'");
            }

            if (!KbmShortcutParser.TryParseKeyOrShortcut(entry.From, out var from, out var error))
            {
                errors.Add($"{context}.from: {error}");
            }
            else if (from.Keys.Count < 2)
            {
                errors.Add($"{context}.from: a shortcut requires at least one modifier and an action key");
            }
            else if (from.Keys.Contains(KbmKeyNames.VkDisabled))
            {
                errors.Add($"{context}.from: 'Disable' cannot be part of a shortcut");
            }
            else if (GetIllegalShortcutName(from) is { } illegalFrom)
            {
                errors.Add($"{context}.from: '{illegalFrom}' is reserved by Windows and cannot be remapped");
            }
            else
            {
                var app = NormalizeTargetApp(entry.TargetApp) ?? string.Empty;
                if (!seenShortcuts.Add((app, KbmShortcutParser.Format(from))))
                {
                    var scope = app.Length == 0 ? "globally" : $"for app '{app}'";
                    errors.Add($"{context}.from: shortcut '{KbmShortcutParser.Format(from)}' is remapped more than once {scope}");
                }
            }

            if (entry.To != null && !TryParseTarget(entry.To, out _, out error))
            {
                errors.Add($"{context}.to: {error}");
            }

            if (entry.ToText != null && entry.ToText.Length == 0)
            {
                errors.Add($"{context}.toText must not be empty");
            }

            if (entry.OpenUri != null && entry.OpenUri.Length == 0)
            {
                errors.Add($"{context}.openUri must not be empty");
            }

            if (entry.RunProgram != null)
            {
                if (string.IsNullOrWhiteSpace(entry.RunProgram.FilePath))
                {
                    errors.Add($"{context}.runProgram.filePath must not be empty");
                }

                ValidateEnumName(entry.RunProgram.Elevation, _elevationNames, $"{context}.runProgram.elevation", errors);
                ValidateEnumName(entry.RunProgram.IfRunning, _ifRunningNames, $"{context}.runProgram.ifRunning", errors);
                ValidateEnumName(entry.RunProgram.WindowStyle, _windowStyleNames, $"{context}.runProgram.windowStyle", errors);
            }
        }

        return errors;
    }

    /// <summary>
    /// Converts a validated friendly model to the stored profile shape.
    /// </summary>
    /// <param name="model">The friendly model; must have passed <see cref="Validate"/>.</param>
    /// <returns>The stored profile.</returns>
    public static KeyboardManagerProfile ToProfile(KbmProfileModel model)
    {
        var profile = new KeyboardManagerProfile();

        foreach (var entry in model.Keys)
        {
            if (!KbmShortcutParser.TryParseKey(entry.From, out var from, out var error))
            {
                throw new InvalidOperationException(error);
            }

            var stored = new KeysDataModel
            {
                OriginalKeys = from.ToVkString(),
            };

            if (entry.ToText != null)
            {
                stored.NewRemapString = entry.ToText;
                profile.RemapKeysToText.InProcessRemapKeys.Add(stored);
            }
            else
            {
                stored.NewRemapKeys = ParseTargetOrThrow(entry.To!).ToVkString();
                profile.RemapKeys.InProcessRemapKeys.Add(stored);
            }
        }

        foreach (var entry in model.Shortcuts)
        {
            if (!KbmShortcutParser.TryParseKeyOrShortcut(entry.From, out var from, out var error))
            {
                throw new InvalidOperationException(error);
            }

            var app = NormalizeTargetApp(entry.TargetApp);
            var stored = app != null ? new AppSpecificKeysDataModel { TargetApp = app } : new KeysDataModel();
            stored.OriginalKeys = from.ToVkString();
            stored.SecondKeyOfChord = from.SecondKeyOfChord;
            stored.ExactMatch = entry.ExactMatch ?? false;

            var isText = false;
            if (entry.ToText != null)
            {
                stored.NewRemapString = entry.ToText;
                isText = true;
            }
            else if (entry.RunProgram != null)
            {
                stored.OperationType = OperationTypeRunProgram;
                stored.RunProgramFilePath = entry.RunProgram.FilePath;
                stored.RunProgramArgs = entry.RunProgram.Args ?? string.Empty;
                stored.RunProgramStartInDir = entry.RunProgram.StartInDir ?? string.Empty;
                stored.RunProgramElevationLevel = ParseEnumName(entry.RunProgram.Elevation, _elevationNames);
                stored.RunProgramAlreadyRunningAction = ParseEnumName(entry.RunProgram.IfRunning, _ifRunningNames);
                stored.RunProgramStartWindowType = ParseEnumName(entry.RunProgram.WindowStyle, _windowStyleNames);
                stored.NewRemapString = UnsupportedText;
            }
            else if (entry.OpenUri != null)
            {
                stored.OperationType = OperationTypeOpenUri;
                stored.OpenUri = entry.OpenUri;
                stored.RunProgramElevationLevel = 0;
                stored.NewRemapString = UnsupportedText;
            }
            else
            {
                var target = ParseTargetOrThrow(entry.To!);
                stored.NewRemapKeys = target.ToVkString();
                if (!target.IsSingleKey)
                {
                    stored.OperationType = OperationTypeRemapShortcut;
                }
            }

            var section = isText ? profile.RemapShortcutsToText : profile.RemapShortcuts;
            if (stored is AppSpecificKeysDataModel appStored)
            {
                section.AppSpecificRemapShortcuts.Add(appStored);
            }
            else
            {
                section.GlobalRemapShortcuts.Add(stored);
            }
        }

        return profile;
    }

    /// <summary>
    /// Converts a stored profile to the canonical friendly model. Entries
    /// that cannot be parsed are skipped with a warning, mirroring the
    /// engine's tolerance for malformed entries.
    /// </summary>
    /// <param name="profile">The stored profile.</param>
    /// <param name="warnings">Optional collector for warnings about skipped entries.</param>
    /// <returns>The canonical friendly model.</returns>
    public static KbmProfileModel FromProfile(KeyboardManagerProfile profile, IList<string>? warnings = null)
    {
        var keys = new List<(uint Code, KbmKeyRemapEntry Entry)>();
        var shortcuts = new List<KbmShortcutRemapEntry>();

        foreach (var stored in profile.RemapKeys?.InProcessRemapKeys ?? [])
        {
            if (!KbmShortcutParser.TryParseVkString(stored.OriginalKeys, 0, out var from) || !from.IsSingleKey ||
                !KbmShortcutParser.TryParseVkString(stored.NewRemapKeys, 0, out var to))
            {
                warnings?.Add($"Skipping unparsable key remap entry '{stored.OriginalKeys}'");
                continue;
            }

            keys.Add((from.Keys[0], new KbmKeyRemapEntry
            {
                From = KbmKeyNames.GetName(from.Keys[0]),
                To = KbmShortcutParser.Format(KbmShortcutParser.Canonicalize(to)),
            }));
        }

        foreach (var stored in profile.RemapKeysToText?.InProcessRemapKeys ?? [])
        {
            if (!KbmShortcutParser.TryParseVkString(stored.OriginalKeys, 0, out var from) || !from.IsSingleKey ||
                string.IsNullOrEmpty(stored.NewRemapString))
            {
                warnings?.Add($"Skipping unparsable key-to-text remap entry '{stored.OriginalKeys}'");
                continue;
            }

            keys.Add((from.Keys[0], new KbmKeyRemapEntry
            {
                From = KbmKeyNames.GetName(from.Keys[0]),
                ToText = stored.NewRemapString,
            }));
        }

        foreach (var (stored, app) in EnumerateShortcuts(profile.RemapShortcuts))
        {
            var entry = CreateShortcutEntry(stored, app, warnings);
            if (entry == null)
            {
                continue;
            }

            if (stored.OperationType == OperationTypeRunProgram)
            {
                if (string.IsNullOrWhiteSpace(stored.RunProgramFilePath))
                {
                    warnings?.Add($"Skipping run-program remap entry '{stored.OriginalKeys}' without a program path");
                    continue;
                }

                entry.RunProgram = new KbmRunProgramAction
                {
                    FilePath = stored.RunProgramFilePath,
                    Args = NullIfEmpty(stored.RunProgramArgs),
                    StartInDir = NullIfEmpty(stored.RunProgramStartInDir),
                    Elevation = FormatEnumValue(stored.RunProgramElevationLevel, _elevationNames),
                    IfRunning = FormatEnumValue(stored.RunProgramAlreadyRunningAction, _ifRunningNames),
                    WindowStyle = FormatEnumValue(stored.RunProgramStartWindowType, _windowStyleNames),
                };
            }
            else if (stored.OperationType == OperationTypeOpenUri)
            {
                if (string.IsNullOrEmpty(stored.OpenUri))
                {
                    warnings?.Add($"Skipping open-URI remap entry '{stored.OriginalKeys}' without a URI");
                    continue;
                }

                entry.OpenUri = stored.OpenUri;
            }
            else
            {
                if (!KbmShortcutParser.TryParseVkString(stored.NewRemapKeys, 0, out var to))
                {
                    warnings?.Add($"Skipping unparsable shortcut remap entry '{stored.OriginalKeys}'");
                    continue;
                }

                entry.To = KbmShortcutParser.Format(KbmShortcutParser.Canonicalize(to));
            }

            shortcuts.Add(entry);
        }

        foreach (var (stored, app) in EnumerateShortcuts(profile.RemapShortcutsToText))
        {
            var entry = CreateShortcutEntry(stored, app, warnings);
            if (entry == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(stored.NewRemapString))
            {
                warnings?.Add($"Skipping shortcut-to-text remap entry '{stored.OriginalKeys}' without text");
                continue;
            }

            entry.ToText = stored.NewRemapString;
            shortcuts.Add(entry);
        }

        return new KbmProfileModel
        {
            Keys = keys.OrderBy(k => k.Code).Select(k => k.Entry).ToList(),
            Shortcuts = shortcuts
                .OrderBy(s => s.TargetApp ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(s => s.From, StringComparer.Ordinal)
                .ToList(),
        };
    }

    /// <summary>
    /// Normalizes a friendly model into its canonical form: canonical key
    /// spellings and ordering, default-valued fields omitted, and entries
    /// sorted. Used to compare desired and current state.
    /// </summary>
    /// <param name="model">The friendly model; must have passed <see cref="Validate"/>.</param>
    /// <returns>The canonical friendly model.</returns>
    public static KbmProfileModel Canonicalize(KbmProfileModel model)
    {
        // Round-tripping through the stored shape guarantees that the desired
        // state and the state read back from disk normalize identically.
        return FromProfile(ToProfile(model));
    }

    private static IEnumerable<(KeysDataModel Stored, string? App)> EnumerateShortcuts(ShortcutsKeyDataModel? section)
    {
        foreach (var stored in section?.GlobalRemapShortcuts ?? [])
        {
            yield return (stored, null);
        }

        foreach (var stored in section?.AppSpecificRemapShortcuts ?? [])
        {
            yield return (stored, stored.TargetApp);
        }
    }

    private static KbmShortcutRemapEntry? CreateShortcutEntry(KeysDataModel stored, string? app, IList<string>? warnings)
    {
        if (!KbmShortcutParser.TryParseVkString(stored.OriginalKeys, stored.SecondKeyOfChord, out var from) || from.Keys.Count < 2)
        {
            warnings?.Add($"Skipping unparsable shortcut remap entry '{stored.OriginalKeys}'");
            return null;
        }

        // The chord second key is embedded as the trailing element of the
        // stored key string; detect it even when the secondKeyOfChord
        // property is absent (it is not written by the C++ editor).
        if (from.SecondKeyOfChord == 0 && from.Keys.Count >= 3 &&
            !KbmKeyNames.IsModifier(from.Keys[^1]) && !KbmKeyNames.IsModifier(from.Keys[^2]))
        {
            from = new KbmShortcutParser.ParsedKeys(from.Keys, from.Keys[^1]);
        }

        // Preserve the engine's exact process scope. It lower-cases stored app
        // names but does not trim them, so exporting a whitespace-padded name
        // as a trimmed name would activate the remap for a different process.
        if (app != null && app != app.Trim())
        {
            warnings?.Add($"Skipping app-specific shortcut remap entry '{stored.OriginalKeys}' with surrounding whitespace in its target application");
            return null;
        }

        return new KbmShortcutRemapEntry
        {
            From = KbmShortcutParser.Format(KbmShortcutParser.Canonicalize(from)),
            TargetApp = app?.ToLowerInvariant(),
            ExactMatch = stored.ExactMatch == true ? true : null,
        };
    }

    private static bool TryParseTarget(string input, out KbmShortcutParser.ParsedKeys result, out string error)
    {
        // A remap target may be a single key (including a lone modifier, e.g.
        // remapping CapsLock to LCtrl) or a shortcut; chords are origin-only.
        if (!input.Contains('+', StringComparison.Ordinal) && !input.Contains(',', StringComparison.Ordinal))
        {
            return KbmShortcutParser.TryParseKey(input, out result, out error);
        }

        if (!KbmShortcutParser.TryParseKeyOrShortcut(input, out result, out error))
        {
            return false;
        }

        if (result.SecondKeyOfChord != 0)
        {
            error = $"Chords are not supported in remap targets ('{input.Trim()}')";
            return false;
        }

        if (GetIllegalShortcutName(result) is { } illegal)
        {
            error = $"'{illegal}' is reserved by Windows and cannot be used as a remap target";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Detects the shortcuts the Keyboard Manager editor refuses
    /// (EditorHelpers::IsShortcutIllegal): Win+L and Ctrl+Alt+Delete are
    /// handled by Windows before any application, so remapping them can never
    /// take effect. The chord second key, if any, is ignored because the
    /// primary shortcut is intercepted before the chord completes.
    /// </summary>
    /// <param name="keys">The parsed shortcut.</param>
    /// <returns>The friendly name of the illegal shortcut, or null when the shortcut is legal.</returns>
    private static string? GetIllegalShortcutName(KbmShortcutParser.ParsedKeys keys)
    {
        var modifiers = new HashSet<KbmKeyNames.ModifierClass>();
        uint actionKey = 0;
        foreach (var key in keys.Keys)
        {
            var modifierClass = KbmKeyNames.GetModifierClass(key);
            if (modifierClass != KbmKeyNames.ModifierClass.None)
            {
                modifiers.Add(modifierClass);
            }
            else if (actionKey == 0)
            {
                actionKey = key;
            }
        }

        // Win+L (any Win key, no other modifiers)
        if (actionKey == 0x4C && modifiers.SetEquals([KbmKeyNames.ModifierClass.Win]))
        {
            return "Win+L";
        }

        // Ctrl+Alt+Del (any Ctrl and Alt keys, no Win or Shift)
        if (actionKey == 0x2E && modifiers.SetEquals([KbmKeyNames.ModifierClass.Ctrl, KbmKeyNames.ModifierClass.Alt]))
        {
            return "Ctrl+Alt+Delete";
        }

        return null;
    }

    private static KbmShortcutParser.ParsedKeys ParseTargetOrThrow(string input)
    {
        if (!TryParseTarget(input, out var result, out var error))
        {
            throw new InvalidOperationException(error);
        }

        return result;
    }

    private static void ValidateEnumName(string? name, string[] names, string context, IList<string> errors)
    {
        if (name != null && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"{context}: invalid value '{name}'; allowed values are: {string.Join(", ", names)}");
        }
    }

    private static int ParseEnumName(string? name, string[] names)
    {
        if (name == null)
        {
            return 0;
        }

        var index = Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : throw new InvalidOperationException($"Invalid value '{name}'");
    }

    private static string? FormatEnumValue(int? value, string[] names)
    {
        // Default (0) values are omitted from the canonical form
        return value is > 0 && value < names.Length ? names[value.Value] : null;
    }

    private static string? NormalizeTargetApp(string? app)
    {
        if (string.IsNullOrWhiteSpace(app))
        {
            return null;
        }

        // The engine lower-cases the target app on load; mirror that here
        return app.Trim().ToLowerInvariant();
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
