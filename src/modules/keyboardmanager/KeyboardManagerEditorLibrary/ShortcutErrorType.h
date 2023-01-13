#pragma once

// Type to store codes for different errors
enum class ShortcutErrorType
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