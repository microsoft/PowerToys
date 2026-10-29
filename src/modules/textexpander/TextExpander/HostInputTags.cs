// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>What the keyboard hook makes of a keystroke, going by its <c>dwExtraInfo</c> tag.</summary>
internal enum HostInputKind
{
    /// <summary>The user typing: matched against triggers.</summary>
    Typing,

    /// <summary>
    /// Input carrying Keyboard Manager's bit, such as a remap, while remaps don't count as typing. It isn't
    /// matched, but it reaches the app and can move the caret.
    /// </summary>
    Unmatched,

    /// <summary>Not the user's input: our own, the runner's replay, or a key event Keyboard Manager swallows.</summary>
    Ignored,
}

/// <summary>
/// Decides what this engine makes of a synthetic keystroke, using the <c>KBDLLHOOKSTRUCT.dwExtraInfo</c>
/// tags PowerToys' low-level hooks agree on (<c>src/common/interop/shared_constants.h</c> and
/// <c>powertoy_module_interface.h</c>). Free of Win32 so the rule is unit-testable.
/// </summary>
internal static class HostInputTags
{
    /// <summary>
    /// The bit Keyboard Manager sets on the input it injects, and tests to leave that input alone
    /// (<c>CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG</c>). Its remaps carry it within 0x11 (a single
    /// key) or 0x101 (a shortcut or text).
    /// </summary>
    public const int KeyboardManagerInjected = 0x1;

    /// <summary>
    /// Tags key events Keyboard Manager injects only to swallow them again in its own hook, such as its NumLock
    /// fix-ups (<c>KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG</c>). No app receives them.
    /// </summary>
    public const int KeyboardManagerSuppressed = 0x111;

    /// <summary>
    /// Tags input replayed by the runner's centralized keyboard hook
    /// (<c>PowertoyModuleIface::CENTRALIZED_KEYBOARD_HOOK_DONT_TRIGGER_FLAG</c>).
    /// </summary>
    public const int CentralizedHookDontTrigger = 0x110;

    /// <summary>
    /// Gets or sets a value indicating whether a Keyboard Manager remap counts as typing. True by default: the
    /// remapped character is the one the user meant, so ignoring it would stop remapped keys from forming
    /// triggers (<see cref="InjectionPolicy.IgnoreRemappedVariable"/> flips it). Hooks run LIFO, so if this
    /// hook runs before Keyboard Manager's it never learns a key was remapped. Volatile: written by the
    /// settings watcher, read in the hook.
    /// </summary>
    public static bool AcceptRemappedInput
    {
        get => _acceptRemappedInput;
        set => _acceptRemappedInput = value;
    }

    private static volatile bool _acceptRemappedInput = true;

    /// <summary>Sorts a keystroke carrying <paramref name="extraInfo"/> into typing, input that isn't matched, and input to ignore.</summary>
    /// <param name="extraInfo">The hook event's <c>dwExtraInfo</c>.</param>
    /// <param name="ownSignature">This process's own injection tag.</param>
    public static HostInputKind Classify(IntPtr extraInfo, IntPtr ownSignature)
    {
        // Our own injection, matched exactly: it also carries Keyboard Manager's bit. Without this an expansion
        // re-enters the matcher and can retrigger itself, which is the classic runaway loop for an expander.
        if (extraInfo == ownSignature)
        {
            return HostInputKind.Ignored;
        }

        long tag = extraInfo.ToInt64();

        // The runner consumed a hotkey chord, decided not to act on it, and put the keystrokes
        // back. That is a replay of an event we have already been offered, not new typing.
        if (tag == CentralizedHookDontTrigger)
        {
            return HostInputKind.Ignored;
        }

        // Keyboard Manager swallows these itself, but a hook that runs before its own still sees them.
        if (tag == KeyboardManagerSuppressed)
        {
            return HostInputKind.Ignored;
        }

        // A bit, not a value, as Keyboard Manager tests it.
        if ((tag & KeyboardManagerInjected) != 0 && !AcceptRemappedInput)
        {
            return HostInputKind.Unmatched;
        }

        return HostInputKind.Typing;
    }
}
