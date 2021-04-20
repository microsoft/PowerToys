#pragma once

namespace KeyboardManagerHelper
{
    // Type to store codes for different errors
    enum class ErrorType
    {
        NoError,
        SameKeyPreviouslyMapped,
        MapToSameKey,
        ConflictingModifierKey,
        SameShortcutPreviouslyMapped,
        MapToSameShortcut,
        ConflictingModifierShortcut,
        WinL,
        CtrlAltDel,
        RemapUnsuccessful,
        SaveFailed,
        ShortcutStartWithModifier,
        ShortcutCannotHaveRepeatedModifier,
        ShortcutAtleast2Keys,
        ShortcutOneActionKey,
        ShortcutNotMoreThanOneActionKey,
        ShortcutMaxShortcutSizeOneActionKey,
        ShortcutDisableAsActionKey
    };
}