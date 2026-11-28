// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

namespace Microsoft.PowerToys.TextExpander;

/// <summary>How a replacement reaches the focused window.</summary>
internal enum InjectionBackend
{
    /// <summary>Paste anything long enough to be at risk, type the rest.</summary>
    Auto,

    /// <summary>Always paste. For targets that lose synthetic keystrokes under load.</summary>
    Clipboard,

    /// <summary>Always type. For targets that refuse a paste, or when the clipboard is precious.</summary>
    Typing,
}

/// <summary>
/// Decides how a given replacement should be injected; free of Win32 so the rule can be tested. Text Expander and
/// Command Palette's extension share it, so Settings' Insertion method means the same in both.
/// </summary>
internal static class InjectionPolicy
{
    /// <summary>
    /// Windows Terminal's top-level window class, which its stable, Preview and Canary builds share, and which it keeps
    /// when it hosts console apps as the default terminal.
    /// </summary>
    public const string WindowsTerminalClass = "CASCADIA_HOSTING_WINDOW_CLASS";

    /// <summary>The console host's window class (conhost), the terminal console apps get when Windows Terminal isn't the default.</summary>
    public const string ConsoleWindowClass = "ConsoleWindowClass";

    /// <summary>
    /// Replacements at least this long are pasted rather than typed. A paste is a single Ctrl+V;
    /// typing stays for short text and targets that reject paste.
    /// </summary>
    public const int DefaultClipboardThresholdChars = 5;

    /// <summary>
    /// Escape hatch for apps that refuse a synthetic Ctrl+V: <c>type</c> forces typing, <c>clipboard</c> always pastes.
    /// </summary>
    public const string BackendVariable = "POWERTOYS_TEXT_EXPANDER_INJECTION_BACKEND";

    /// <summary>Overrides <see cref="DefaultClipboardThresholdChars"/>.</summary>
    public const string ThresholdVariable = "POWERTOYS_TEXT_EXPANDER_CLIPBOARD_THRESHOLD";

    /// <summary>
    /// Set to <c>1</c> to stop treating Keyboard Manager remaps as typing (see the engine's <c>HostInputTags.AcceptRemappedInput</c>).
    /// </summary>
    public const string IgnoreRemappedVariable = "POWERTOYS_TEXT_EXPANDER_IGNORE_REMAPPED_INPUT";

    /// <summary>
    /// Makes a trigger wait for a word terminator before firing. On by default to prevent a
    /// trigger from going off while the user is still typing a longer word.
    /// </summary>
    public const string RequireWordBoundaryVariable = "POWERTOYS_TEXT_EXPANDER_REQUIRE_WORD_BOUNDARY";

    /// <summary>
    /// Whether the terminator that released a trigger is handed back (on by default). Only meaningful with
    /// the word-boundary mode on.
    /// </summary>
    public const string WordBoundaryKeepSpaceVariable = "POWERTOYS_TEXT_EXPANDER_WORD_BOUNDARY_KEEP_SPACE";

    /// <summary>Reads a boolean escape hatch; anything unrecognized leaves the default alone.</summary>
    public static bool ParseFlag(string? value, bool fallback)
        => value?.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => fallback,
        };

    /// <summary>
    /// True when <paramref name="text"/> should go via the clipboard. Multi-line text always does: synthesized
    /// Return presses trigger auto-indent and auto-complete. Its last <paramref name="givenBack"/> characters, the
    /// user's own keystrokes given back, such as the space that released the trigger, don't count toward
    /// <paramref name="thresholdChars"/>, and text that is only those, as after a cancelled question, is always typed.
    /// In Windows Terminal (<paramref name="windowsTerminal"/>), Automatic types one line however long it is: Terminal
    /// trims trailing whitespace from a one-line paste, which would drop the space given back. Lines still paste there,
    /// so Terminal's multi-line paste warning still guards them, and Always paste still pastes. So does text with any
    /// control character or with text from a copy (<paramref name="fromClipboard"/>): Terminal filters what's pasted,
    /// but acts on control characters that are typed, and losing the space is the lesser harm.
    /// </summary>
    public static bool ShouldUseClipboard(string text, int thresholdChars, InjectionBackend backend, int givenBack = 0, bool windowsTerminal = false, bool fromClipboard = false)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (backend == InjectionBackend.Typing || text.Length <= givenBack)
        {
            return false;
        }

        if (backend == InjectionBackend.Clipboard || text.Contains('\n'))
        {
            return true;
        }

        return windowsTerminal
            ? fromClipboard || ControlCharacters.HasAny(text)
            : text.Length - givenBack >= Math.Max(1, thresholdChars);
    }

    /// <summary>Reads a backend name, falling back to <paramref name="fallback"/> on a typo rather than refusing to start.</summary>
    public static InjectionBackend ParseBackend(string? value, InjectionBackend fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "auto" => InjectionBackend.Auto,
            "clipboard" or "paste" => InjectionBackend.Clipboard,
            "type" or "typing" or "inject" or "keyboard" => InjectionBackend.Typing,
            _ => fallback,
        };
    }

    /// <summary>
    /// Reads a threshold, rejecting anything below one character. Zero would mean pasting even
    /// an empty replacement; "never paste" is spelled <see cref="InjectionBackend.Typing"/>.
    /// </summary>
    public static int ParseThreshold(string? value, int fallback)
        => int.TryParse(value?.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed) && parsed >= 1
            ? parsed
            : fallback;

    /// <summary>True for <see cref="WindowsTerminalClass"/>. Window classes ignore case.</summary>
    public static bool IsWindowsTerminalClass(string? className)
        => string.Equals(className, WindowsTerminalClass, StringComparison.OrdinalIgnoreCase);

    /// <summary>True for <see cref="ConsoleWindowClass"/>. Window classes ignore case.</summary>
    public static bool IsConsoleClass(string? className)
        => string.Equals(className, ConsoleWindowClass, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True when a snippet with lines mustn't be typed into a terminal: its line breaks would be Enter presses, each running
    /// a line, past the warning Windows Terminal shows for a paste with lines. Automatic and Always paste type lines only
    /// when the paste was refused, and then never into a terminal. Always type (<see cref="InjectionBackend.Typing"/>)
    /// refuses them there only when part of them came from a copy (<paramref name="fromClipboard"/>): lines the user wrote
    /// are typed as the setting says.
    /// </summary>
    public static bool RefusesTypedLines(string text, bool terminal, InjectionBackend backend, bool fromClipboard)
    {
        ArgumentNullException.ThrowIfNull(text);
        return terminal && text.Contains('\n') && (backend != InjectionBackend.Typing || fromClipboard);
    }
}
