// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Decides which synthetic keystrokes this engine must ignore, using the <c>KBDLLHOOKSTRUCT.dwExtraInfo</c>
/// tags PowerToys' low-level hooks agree on (<c>src/common/interop/shared_constants.h</c> and
/// <c>powertoy_module_interface.h</c>). Free of Win32 so the rule is unit-testable.
/// </summary>
internal static class HostInputTags
{
    /// <summary>
    /// Tags input injected by Keyboard Manager when it performs a remap
    /// (<c>CommonSharedConstants::KEYBOARDMANAGER_INJECTED_FLAG</c>).
    /// </summary>
    public const int KeyboardManagerInjected = 0x1;

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

    /// <summary>
    /// True when a keystroke carrying <paramref name="extraInfo"/> must not be treated as the user
    /// typing.
    /// </summary>
    /// <param name="extraInfo">The hook event's <c>dwExtraInfo</c>.</param>
    /// <param name="ownSignature">This process's own injection tag.</param>
    public static bool ShouldIgnore(IntPtr extraInfo, IntPtr ownSignature)
    {
        // Our own injection. Without this an expansion re-enters the matcher and can retrigger
        // itself, which is the classic runaway loop for an expander.
        if (extraInfo == ownSignature)
        {
            return true;
        }

        long tag = extraInfo.ToInt64();

        // The runner consumed a hotkey chord, decided not to act on it, and put the keystrokes
        // back. That is a replay of an event we have already been offered, not new typing.
        if (tag == CentralizedHookDontTrigger)
        {
            return true;
        }

        if (tag == KeyboardManagerInjected)
        {
            return !AcceptRemappedInput;
        }

        return false;
    }
}
