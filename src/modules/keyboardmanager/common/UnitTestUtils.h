#include <string>
#include <Windows.h>
#include <keyboardmanager/KeyboardManagerEditorLibrary/ShortcutErrorType.h>

// Redefine VK_* macros with DWORD values to reduce casts.
#undef VK_SHIFT
#undef VK_CONTROL
#undef VK_MENU
#undef VK_CAPITAL
#undef VK_LCONTROL
constexpr auto VK_NULL = static_cast<DWORD>(0);
constexpr auto VK_SHIFT = static_cast<DWORD>(0x10);
constexpr auto VK_CONTROL = static_cast<DWORD>(0x11);
constexpr auto VK_MENU = static_cast<DWORD>(0x12);
constexpr auto VK_CAPITAL = static_cast<DWORD>(0x14);
constexpr auto VK_A = static_cast<DWORD>('A');
constexpr auto VK_B = static_cast<DWORD>('B');
constexpr auto VK_C = static_cast<DWORD>('C');
constexpr auto VK_D = static_cast<DWORD>('D');
constexpr auto VK_E = static_cast<DWORD>('E');
constexpr auto VK_LCONTROL = static_cast<DWORD>(0xA2);

namespace Microsoft::VisualStudio::CppUnitTestFramework
{
    template<>
    constexpr std::wstring ToString<ShortcutErrorType>(const ShortcutErrorType& value)
    {
        switch (value)
        {
        case ShortcutErrorType::NoError:
            return L"NoError";
        case ShortcutErrorType::SameKeyPreviouslyMapped:
            return L"SameKeyPreviouslyMapped";
        case ShortcutErrorType::MapToSameKey:
            return L"MapToSameKey";
        case ShortcutErrorType::ConflictingModifierKey:
            return L"ConflictingModifierKey";
        case ShortcutErrorType::SameShortcutPreviouslyMapped:
            return L"SameShortcutPreviouslyMapped";
        case ShortcutErrorType::MapToSameShortcut:
            return L"MapToSameShortcut";
        case ShortcutErrorType::ConflictingModifierShortcut:
            return L"ConflictingModifierShortcut";
        case ShortcutErrorType::WinL:
            return L"WinL";
        case ShortcutErrorType::CtrlAltDel:
            return L"CtrlAltDel";
        case ShortcutErrorType::RemapUnsuccessful:
            return L"RemapUnsuccessful";
        case ShortcutErrorType::SaveFailed:
            return L"SaveFailed";
        case ShortcutErrorType::ShortcutStartWithModifier:
            return L"ShortcutStartWithModifier";
        case ShortcutErrorType::ShortcutCannotHaveRepeatedModifier:
            return L"ShortcutCannotHaveRepeatedModifier";
        case ShortcutErrorType::ShortcutAtleast2Keys:
            return L"ShortcutAtleast2Keys";
        case ShortcutErrorType::ShortcutOneActionKey:
            return L"ShortcutOneActionKey";
        case ShortcutErrorType::ShortcutNotMoreThanOneActionKey:
            return L"ShortcutNotMoreThanOneActionKey";
        case ShortcutErrorType::ShortcutMaxShortcutSizeOneActionKey:
            return L"ShortcutMaxShortcutSizeOneActionKey";
        case ShortcutErrorType::ShortcutDisableAsActionKey:
            return L"ShortcutDisableAsActionKey";
        default:
            return L"Unknown (" + std::to_wstring(static_cast<int>(value)) + L")";
        }
    }
}
