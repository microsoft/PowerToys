#include "pch.h"
#include "BufferValidationHelpers.h"

#include <common/interop/shared_constants.h>
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/Helpers.h>

#include "KeyboardManagerEditorStrings.h"
#include "KeyDropDownControl.h"
#include "UIHelpers.h"
#include "EditorHelpers.h"
#include "EditorConstants.h"

namespace BufferValidationHelpers
{
    // Helper function to verify if a key is being remapped to/from its combined key
    bool IsKeyRemappingToItsCombinedKey(DWORD keyCode1, DWORD keyCode2)
    {
        return (keyCode1 == Helpers::GetCombinedKey(keyCode1) || keyCode2 == Helpers::GetCombinedKey(keyCode2)) &&
               Helpers::GetCombinedKey(keyCode1) == Helpers::GetCombinedKey(keyCode2);
    }

    // Function to validate and update an element of the key remap buffer when the selection has changed
    ShortcutErrorType ValidateAndUpdateKeyBufferElement(int rowIndex, int colIndex, int selectedKeyCode, RemapBuffer& remapBuffer)
    {
        ShortcutErrorType errorType = ShortcutErrorType::NoError;

        // Check if the element was not found or the index exceeds the known keys
        if (selectedKeyCode != -1)
        {
            // Check if the value being set is the same as the other column
            if (remapBuffer[rowIndex].first[std::abs(colIndex - 1)].index() == 0)
            {
                DWORD otherColumnKeyCode = std::get<DWORD>(remapBuffer[rowIndex].first[std::abs(colIndex - 1)]);
                if (otherColumnKeyCode == selectedKeyCode || IsKeyRemappingToItsCombinedKey(selectedKeyCode, otherColumnKeyCode))
                {
                    errorType = ShortcutErrorType::MapToSameKey;
                }
            }

            // If one column is shortcut and other is key no warning required

            if (errorType == ShortcutErrorType::NoError && colIndex == 0)
            {
                // Check if the key is already remapped to something else
                for (int i = 0; i < remapBuffer.size(); i++)
                {
                    if (i != rowIndex)
                    {
                        if (remapBuffer[i].first[colIndex].index() == 0)
                        {
                            ShortcutErrorType result = EditorHelpers::DoKeysOverlap(std::get<DWORD>(remapBuffer[i].first[colIndex]), selectedKeyCode);
                            if (result != ShortcutErrorType::NoError)
                            {
                                errorType = result;
                                break;
                            }
                        }

                        // If one column is shortcut and other is key no warning required
                    }
                }
            }

            // If there is no error, set the buffer
            if (errorType == ShortcutErrorType::NoError)
            {
                remapBuffer[rowIndex].first[colIndex] = (DWORD)selectedKeyCode;
            }
            else
            {
                remapBuffer[rowIndex].first[colIndex] = (DWORD)0;
            }
        }
        else
        {
            // Reset to null if the key is not found
            remapBuffer[rowIndex].first[colIndex] = (DWORD)0;
        }

        return errorType;
    }

    // Function to validate an element of the shortcut remap buffer when the selection has changed
    std::pair<ShortcutErrorType, DropDownAction> ValidateShortcutBufferElement(int rowIndex, int colIndex, uint32_t dropDownIndex, const std::vector<int32_t>& selectedCodes, std::wstring appName, bool isHybridControl, const RemapBuffer& remapBuffer, bool dropDownFound)
    {
        BufferValidationHelpers::DropDownAction dropDownAction = BufferValidationHelpers::DropDownAction::NoAction;
        ShortcutErrorType errorType = ShortcutErrorType::NoError;
        size_t dropDownCount = selectedCodes.size();
        DWORD selectedKeyCode = dropDownFound ? selectedCodes[dropDownIndex] : -1;

        if (selectedKeyCode != -1 && dropDownFound)
        {
            // If only 1 drop down and action key is chosen: Warn that a modifier must be chosen (if the drop down is not for a hybrid scenario)
            if (dropDownCount == 1 && !Helpers::IsModifierKey(selectedKeyCode) && !isHybridControl)
            {
                // warn and reset the drop down
                errorType = ShortcutErrorType::ShortcutStartWithModifier;
            }
            else if (dropDownIndex == dropDownCount - 1)
            {
                // If it is the last drop down
                // If last drop down and a modifier is selected: add a new drop down (max drop down count should be enforced)
                if (Helpers::IsModifierKey(selectedKeyCode) && dropDownCount < EditorConstants::MaxShortcutSize)
                {
                    // If it matched any of the previous modifiers then reset that drop down
                    if (EditorHelpers::CheckRepeatedModifier(selectedCodes, selectedKeyCode))
                    {
                        // warn and reset the drop down
                        errorType = ShortcutErrorType::ShortcutCannotHaveRepeatedModifier;
                    }
                    else
                    {
                        // If not, add a new drop down
                        dropDownAction = BufferValidationHelpers::DropDownAction::AddDropDown;
                    }
                }
                else if (Helpers::IsModifierKey(selectedKeyCode) && dropDownCount >= EditorConstants::MaxShortcutSize)
                {
                    // If last drop down and a modifier is selected but there are already max drop downs: warn the user
                    // warn and reset the drop down
                    errorType = ShortcutErrorType::ShortcutMaxShortcutSizeOneActionKey;
                }
                else if (selectedKeyCode == 0)
                {
                    // If None is selected but it's the last index: warn
                    // If it is a hybrid control and there are 2 drop downs then deletion is allowed
                    if (isHybridControl && dropDownCount == EditorConstants::MinShortcutSize)
                    {
                        // set delete drop down flag
                        dropDownAction = BufferValidationHelpers::DropDownAction::DeleteDropDown;
                        // do not delete the drop down now since there may be some other error which would cause the drop down to be invalid after removal
                    }
                    else
                    {
                        // warn and reset the drop down
                        errorType = ShortcutErrorType::ShortcutOneActionKey;
                    }
                }
                else if (selectedKeyCode == CommonSharedConstants::VK_DISABLED && dropDownIndex)
                {
                    // Disable can not be selected if one modifier key has already been selected
                    errorType = ShortcutErrorType::ShortcutDisableAsActionKey;
                }
                // If none of the above, then the action key will be set
            }
            else
            {
                // If it is not the last drop down
                if (Helpers::IsModifierKey(selectedKeyCode))
                {
                    // If it matched any of the previous modifiers then reset that drop down
                    if (EditorHelpers::CheckRepeatedModifier(selectedCodes, selectedKeyCode))
                    {
                        // warn and reset the drop down
                        errorType = ShortcutErrorType::ShortcutCannotHaveRepeatedModifier;
                    }
                    // If not, the modifier key will be set
                }
                else if (selectedKeyCode == 0 && dropDownCount > EditorConstants::MinShortcutSize)
                {
                    // If None is selected and there are more than 2 drop downs
                    // set delete drop down flag
                    dropDownAction = BufferValidationHelpers::DropDownAction::DeleteDropDown;
                    // do not delete the drop down now since there may be some other error which would cause the drop down to be invalid after removal
                }
                else if (selectedKeyCode == 0 && dropDownCount <= EditorConstants::MinShortcutSize)
                {
                    // If it is a hybrid control and there are 2 drop downs then deletion is allowed
                    if (isHybridControl && dropDownCount == EditorConstants::MinShortcutSize)
                    {
                        // set delete drop down flag
                        dropDownAction = BufferValidationHelpers::DropDownAction::DeleteDropDown;
                        // do not delete the drop down now since there may be some other error which would cause the drop down to be invalid after removal
                    }
                    else
                    {
                        // warn and reset the drop down
                        errorType = ShortcutErrorType::ShortcutAtleast2Keys;
                    }
                }
                else if (selectedKeyCode == CommonSharedConstants::VK_DISABLED && dropDownIndex)
                {
                    // Allow selection of VK_DISABLE only in first dropdown
                    errorType = ShortcutErrorType::ShortcutDisableAsActionKey;
                }
                else if (dropDownIndex != 0 || isHybridControl)
                {
                    // If the user tries to set an action key check if all drop down menus after this are empty if it is not the first key.
                    // If it is a hybrid control, this can be done even on the first key
                    bool isClear = true;
                    for (int i = dropDownIndex + 1; i < static_cast<int>(dropDownCount); i++)
                    {
                        if (selectedCodes[i] != -1)
                        {
                            isClear = false;
                            break;
                        }
                    }

                    if (isClear)
                    {
                        dropDownAction = BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns;
                    }
                    else
                    {
                        // this used to "warn and reset the drop down" but for now, since we will allow Chords, we do allow this
                        // leaving the here and commented out for posterity, for now.
                        // errorType = ShortcutErrorType::ShortcutNotMoreThanOneActionKey;
                    }
                }
                else
                {
                    // If there an action key is chosen on the first drop down and there are more than one drop down menus
                    // warn and reset the drop down
                    errorType = ShortcutErrorType::ShortcutStartWithModifier;
                }
            }
        }

        // After validating the shortcut, now for errors like remap to same shortcut, remap shortcut more than once, Win L and Ctrl Alt Del
        if (errorType == ShortcutErrorType::NoError)
        {
            KeyShortcutTextUnion tempShortcut;
            if (isHybridControl && KeyDropDownControl::GetNumberOfSelectedKeys(selectedCodes) == 1)
            {
                tempShortcut = (DWORD)*std::find_if(selectedCodes.begin(), selectedCodes.end(), [](int32_t a) { return a != -1 && a != 0; });
            }
            else
            {
                tempShortcut = Shortcut();
                std::get<Shortcut>(tempShortcut).SetKeyCodes(selectedCodes);
            }

            // Convert app name to lower case
            std::transform(appName.begin(), appName.end(), appName.begin(), towlower);
            std::wstring lowercaseDefAppName = KeyboardManagerEditorStrings::DefaultAppName();
            std::transform(lowercaseDefAppName.begin(), lowercaseDefAppName.end(), lowercaseDefAppName.begin(), towlower);
            if (appName == lowercaseDefAppName)
            {
                appName = L"";
            }

            // Check if the value being set is the same as the other column - index of other column does not have to be checked since only one column is hybrid
            if (tempShortcut.index() == 1)
            {
                // If shortcut to shortcut
                if (remapBuffer[rowIndex].first[std::abs(colIndex - 1)].index() == 1)
                {
                    auto& shortcut = std::get<Shortcut>(remapBuffer[rowIndex].first[std::abs(colIndex - 1)]);
                    if (shortcut == std::get<Shortcut>(tempShortcut) && EditorHelpers::IsValidShortcut(shortcut) && EditorHelpers::IsValidShortcut(std::get<Shortcut>(tempShortcut)))
                    {
                        errorType = ShortcutErrorType::MapToSameShortcut;
                    }
                }

                // If one column is shortcut and other is key no warning required
            }
            else
            {
                // If key to key
                if (remapBuffer[rowIndex].first[std::abs(colIndex - 1)].index() == 0)
                {
                    DWORD otherColumnKeyCode = std::get<DWORD>(remapBuffer[rowIndex].first[std::abs(colIndex - 1)]);
                    DWORD shortcutKeyCode = std::get<DWORD>(tempShortcut);
                    if ((otherColumnKeyCode == shortcutKeyCode || IsKeyRemappingToItsCombinedKey(otherColumnKeyCode, shortcutKeyCode)) && otherColumnKeyCode != NULL && shortcutKeyCode != NULL)
                    {
                        errorType = ShortcutErrorType::MapToSameKey;
                    }
                }

                // If one column is shortcut and other is key no warning required
            }

            if (errorType == ShortcutErrorType::NoError && colIndex == 0)
            {
                // Check if the key is already remapped to something else for the same target app
                for (int i = 0; i < remapBuffer.size(); i++)
                {
                    std::wstring currAppName = remapBuffer[i].second;
                    std::transform(currAppName.begin(), currAppName.end(), currAppName.begin(), towlower);

                    if (i != rowIndex && currAppName == appName)
                    {
                        ShortcutErrorType result = ShortcutErrorType::NoError;
                        if (!isHybridControl)
                        {
                            result = EditorHelpers::DoShortcutsOverlap(std::get<Shortcut>(remapBuffer[i].first[colIndex]), std::get<Shortcut>(tempShortcut));
                        }
                        else
                        {
                            if (tempShortcut.index() == 0 && remapBuffer[i].first[colIndex].index() == 0)
                            {
                                if (std::get<DWORD>(tempShortcut) != NULL && std::get<DWORD>(remapBuffer[i].first[colIndex]) != NULL)
                                {
                                    result = EditorHelpers::DoKeysOverlap(std::get<DWORD>(remapBuffer[i].first[colIndex]), std::get<DWORD>(tempShortcut));
                                }
                            }
                            else if (tempShortcut.index() == 1 && remapBuffer[i].first[colIndex].index() == 1)
                            {
                                auto& shortcut = std::get<Shortcut>(remapBuffer[i].first[colIndex]);
                                if (EditorHelpers::IsValidShortcut(std::get<Shortcut>(tempShortcut)) && EditorHelpers::IsValidShortcut(shortcut))
                                {
                                    result = EditorHelpers::DoShortcutsOverlap(std::get<Shortcut>(remapBuffer[i].first[colIndex]), std::get<Shortcut>(tempShortcut));
                                }
                            }
                            // Other scenarios not possible since key to shortcut is with key to key, and shortcut to key is with shortcut to shortcut
                        }
                        if (result != ShortcutErrorType::NoError)
                        {
                            errorType = result;
                            break;
                        }
                    }
                }
            }

            if (errorType == ShortcutErrorType::NoError && tempShortcut.index() == 1)
            {
                errorType = EditorHelpers::IsShortcutIllegal(std::get<Shortcut>(tempShortcut));
            }
        }

        return std::make_pair(errorType, dropDownAction);
    }
}
