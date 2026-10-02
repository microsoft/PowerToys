// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
/// Decides how a given replacement should be injected. Deliberately free of Win32 so the rule
/// itself can be tested, which is the only part of injection a test can reach: everything below
/// this is SendInput into whatever window happens to be focused.
/// </summary>
internal static class InjectionPolicy
{
    /// <summary>
    /// Replacements at least this long are pasted rather than typed. A paste is a single Ctrl+V;
    /// typing stays for short text and targets that reject paste.
    /// </summary>
    public const int DefaultClipboardThresholdChars = 5;

    /// <summary>
    /// Escape hatch for applications that refuse a synthetic Ctrl+V — some remote-desktop and
    /// terminal clients swallow it, and a few security-hardened apps ignore WM_PASTE outright.
    /// Set to <c>type</c> to force the per-character path, or <c>clipboard</c> to always paste.
    /// </summary>
    public const string BackendVariable = "POWERTOYS_TEXT_EXPANDER_INJECTION_BACKEND";

    /// <summary>Overrides <see cref="DefaultClipboardThresholdChars"/>.</summary>
    public const string ThresholdVariable = "POWERTOYS_TEXT_EXPANDER_CLIPBOARD_THRESHOLD";

    /// <summary>
    /// Set to <c>1</c> to stop treating PowerToys Keyboard Manager remaps as typing, so a remapped
    /// key can never form part of a trigger. Off by default — see
    /// <see cref="HostInputTags.AcceptRemappedInput"/> for why accepting them is the better
    /// default, and why the choice is nonetheless debatable enough to expose.
    /// </summary>
    public const string IgnoreRemappedVariable = "POWERTOYS_TEXT_EXPANDER_IGNORE_REMAPPED_INPUT";

    /// <summary>
    /// Makes a trigger wait for a word terminator before firing. Off by default: immediate
    /// expansion is what the tool is for, and this trades that feel for protection against a
    /// trigger going off inside a longer word.
    /// </summary>
    public const string RequireWordBoundaryVariable = "POWERTOYS_TEXT_EXPANDER_REQUIRE_WORD_BOUNDARY";

    /// <summary>
    /// Whether the terminator that released a trigger is handed back. On by default; set to
    /// 0 for the email-address and code cases where a trailing space is wrong rather than
    /// merely untidy. Only meaningful with the word-boundary mode on.
    /// </summary>
    public const string WordBoundaryKeepSpaceVariable = "POWERTOYS_TEXT_EXPANDER_WORD_BOUNDARY_KEEP_SPACE";

    /// <summary>
    /// Reads a boolean escape hatch. Anything unrecognised means "leave the default alone": an
    /// override nobody can spell correctly should not quietly change how input is read.
    /// </summary>
    public static bool ParseFlag(string? value, bool fallback)
        => value?.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => fallback,
        };

    /// <summary>
    /// True when <paramref name="text"/> should go via the clipboard. Multi-line text always
    /// does: typing it means synthesising Return presses, which auto-indent and auto-complete
    /// turn into something other than what the snippet said.
    /// </summary>
    public static bool ShouldUseClipboard(string text, int thresholdChars, InjectionBackend backend)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (backend == InjectionBackend.Typing || text.Length == 0)
        {
            return false;
        }

        if (backend == InjectionBackend.Clipboard)
        {
            return true;
        }

        return text.Contains('\n') || text.Length >= Math.Max(1, thresholdChars);
    }

    /// <summary>
    /// Reads a backend name, falling back rather than refusing to start on a typo: an unusable
    /// override should degrade to the default behaviour, not leave the user with no expansion.
    /// </summary>
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
}
