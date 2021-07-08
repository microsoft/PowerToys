#include "pch.h"
#include "KeyboardManagerEditorStrings.h"

// Function to return the error message
winrt::hstring KeyboardManagerEditorStrings::GetErrorMessage(ShortcutErrorType errorType)
{
    switch (errorType)
    {
    case ShortcutErrorType::NoError:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_REMAPSUCCESSFUL).c_str();
    case ShortcutErrorType::SameKeyPreviouslyMapped:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SAMEKEYPREVIOUSLYMAPPED).c_str();
    case ShortcutErrorType::MapToSameKey:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_MAPPEDTOSAMEKEY).c_str();
    case ShortcutErrorType::ConflictingModifierKey:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_CONFLICTINGMODIFIERKEY).c_str();
    case ShortcutErrorType::SameShortcutPreviouslyMapped:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SAMESHORTCUTPREVIOUSLYMAPPED).c_str();
    case ShortcutErrorType::MapToSameShortcut:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_MAPTOSAMESHORTCUT).c_str();
    case ShortcutErrorType::ConflictingModifierShortcut:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_CONFLICTINGMODIFIERSHORTCUT).c_str();
    case ShortcutErrorType::WinL:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_WINL).c_str();
    case ShortcutErrorType::CtrlAltDel:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_CTRLALTDEL).c_str();
    case ShortcutErrorType::RemapUnsuccessful:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_REMAPUNSUCCESSFUL).c_str();
    case ShortcutErrorType::SaveFailed:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SAVEFAILED).c_str();
    case ShortcutErrorType::ShortcutStartWithModifier:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SHORTCUTSTARTWITHMODIFIER).c_str();
    case ShortcutErrorType::ShortcutCannotHaveRepeatedModifier:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SHORTCUTNOREPEATEDMODIFIER).c_str();
    case ShortcutErrorType::ShortcutAtleast2Keys:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SHORTCUTATLEAST2KEYS).c_str();
    case ShortcutErrorType::ShortcutOneActionKey:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SHORTCUTONEACTIONKEY).c_str();
    case ShortcutErrorType::ShortcutNotMoreThanOneActionKey:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_SHORTCUTMAXONEACTIONKEY).c_str();
    case ShortcutErrorType::ShortcutMaxShortcutSizeOneActionKey:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_MAXSHORTCUTSIZE).c_str();
    case ShortcutErrorType::ShortcutDisableAsActionKey:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_DISABLEASACTIONKEY).c_str();
    default:
        return GET_RESOURCE_STRING(IDS_ERRORMESSAGE_DEFAULT).c_str();
    }
}
