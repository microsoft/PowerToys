// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// Decides which synthetic keystrokes this engine must ignore when it shares a machine with
/// PowerToys.
///
/// <para>
/// Windows lets any number of <c>WH_KEYBOARD_LL</c> hooks coexist — they form a chain — and
/// PowerToys already ships several: the runner's centralized hotkey hook, Keyboard Manager's
/// engine, the Keyboard Manager editor, Quick Accent and Command Palette. Coexisting is therefore
/// normal, but it is only safe if everyone agrees on a way to say "this keystroke is mine, do not
/// re-process it". That convention is a tag written into <c>KBDLLHOOKSTRUCT.dwExtraInfo</c>, and
/// PowerToys' values live in <c>src/common/interop/shared_constants.h</c> and
/// <c>powertoy_module_interface.h</c>.
/// </para>
///
/// <para>
/// Deliberately free of Win32 so the rule is reachable from tests, exactly as
/// <see cref="ClipboardFormats"/> is: this is a policy decision that changes what the user sees
/// expanded, while everything around it is a hook callback that cannot be unit tested.
/// </para>
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
    /// Gets or sets a value indicating whether whether a Keyboard Manager remap should count as typing.
    ///
    /// <para>
    /// <b>True (the default) is a considered choice, not an oversight.</b> When the user remaps a
    /// key, Keyboard Manager swallows the physical keystroke and injects the replacement tagged
    /// with <see cref="KeyboardManagerInjected"/>. That injected character is the one the user
    /// meant to type, and it is the only one anybody downstream will ever see. Ignoring it would
    /// mean a remapped key could never form part of a trigger, so a user who remaps — the exact
    /// user who has Keyboard Manager enabled — would find their snippets silently stop matching,
    /// with nothing on screen to explain why.
    /// </para>
    ///
    /// <para>
    /// The opposite choice is defensible if a remap is understood as "machinery", not typing, so
    /// this is left switchable rather than baked in. See
    /// <see cref="InjectionPolicy.IgnoreRemappedVariable"/>.
    /// </para>
    ///
    /// <para>
    /// <b>Known limitation either way:</b> the hook chain is LIFO, so whichever hook was installed
    /// last runs first. If this engine's hook runs before Keyboard Manager's, it sees the original
    /// keystroke and never learns it was remapped. Keyboard Manager's own documentation notes the
    /// same fragility about other hook-based apps, and it cannot be fixed from this side.
    /// </para>
    /// <para>
    /// <b>Volatile because this is now written from a settings-watcher thread</b> and read inside
    /// the keyboard hook on every keystroke. It used to be written only on the message-pump thread
    /// before the hook was installed, which needed no synchronisation; live settings reload
    /// removed that guarantee.
    /// </para>
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
