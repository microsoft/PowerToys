#include "pch.h"
#include "BufferValidationHelpers.h"
#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <common\shared_constants.h>

namespace BufferValidationHelpers
{
    // Function to validate and update an element of the key remap buffer when the selection has changed
    KeyboardManagerHelper::ErrorType ValidateAndUpdateKeyBufferElement(int rowIndex, int colIndex, int selectedKeyIndex, const std::vector<DWORD>& keyCodeList, RemapBuffer& remapBuffer)
    {
        KeyboardManagerHelper::ErrorType errorType = KeyboardManagerHelper::ErrorType::NoError;

        // Check if the element was not found or the index exceeds the known keys
        if (selectedKeyIndex != -1 && keyCodeList.size() > selectedKeyIndex)
        {
            // Check if the value being set is the same as the other column
            if (remapBuffer[rowIndex].first[std::abs(int(colIndex) - 1)].index() == 0)
            {
                if (std::get<DWORD>(remapBuffer[rowIndex].first[std::abs(int(colIndex) - 1)]) == keyCodeList[selectedKeyIndex])
                {
                    errorType = KeyboardManagerHelper::ErrorType::MapToSameKey;
                }
            }

            // If one column is shortcut and other is key no warning required

            if (errorType == KeyboardManagerHelper::ErrorType::NoError && colIndex == 0)
            {
                // Check if the key is already remapped to something else
                for (int i = 0; i < remapBuffer.size(); i++)
                {
                    if (i != rowIndex)
                    {
                        if (remapBuffer[i].first[colIndex].index() == 0)
                        {
                            KeyboardManagerHelper::ErrorType result = KeyboardManagerHelper::DoKeysOverlap(std::get<DWORD>(remapBuffer[i].first[colIndex]), keyCodeList[selectedKeyIndex]);
                            if (result != KeyboardManagerHelper::ErrorType::NoError)
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
            if (errorType == KeyboardManagerHelper::ErrorType::NoError)
            {
                remapBuffer[rowIndex].first[colIndex] = keyCodeList[selectedKeyIndex];
            }
            else
            {
                remapBuffer[rowIndex].first[colIndex] = NULL;
            }
        }
        else
        {
            // Reset to null if the key is not found
            remapBuffer[rowIndex].first[colIndex] = NULL;
        }

        return errorType;
    }

    // Function to validate an element of the shortcut remap buffer when the selection has changed
    std::pair<KeyboardManagerHelper::ErrorType, DropDownAction> ValidateShortcutBufferElement(int rowIndex, int colIndex, uint32_t dropDownIndex, const std::vector<int32_t>& selectedIndices, std::wstring appName, bool isHybridControl, const std::vector<DWORD>& keyCodeList, const RemapBuffer& remapBuffer, bool dropDownFound)
    {
        BufferValidationHelpers::DropDownAction dropDownAction = BufferValidationHelpers::DropDownAction::NoAction;
        KeyboardManagerHelper::ErrorType errorType = KeyboardManagerHelper::ErrorType::NoError;
        size_t dropDownCount = selectedIndices.size();
        std::vector<DWORD> selectedKeyCodes = KeyboardManagerHelper::GetKeyCodesFromSelectedIndices(selectedIndices, keyCodeList);
        int selectedKeyIndex = dropDownFound ? selectedIndices[dropDownIndex] : -1;

        if (selectedKeyIndex != -1 && keyCodeList.size() > selectedKeyIndex && dropDownFound)
        {
            // If only 1 drop down and action key is chosen: Warn that a modifier must be chosen (if the drop down is not for a hybrid scenario)
            if (dropDownCount == 1 && !KeyboardManagerHelper::IsModifierKey(keyCodeList[selectedKeyIndex]) && !isHybridControl)
            {
                // warn and reset the drop down
                errorType = KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier;
            }
            // If it is the last drop down
            else if (dropDownIndex == dropDownCount - 1)
            {
                // If last drop down and a modifier is selected: add a new drop down (max drop down count should be enforced)
                if (KeyboardManagerHelper::IsModifierKey(keyCodeList[selectedKeyIndex]) && dropDownCount < KeyboardManagerConstants::MaxShortcutSize)
                {
                    // If it matched any of the previous modifiers then reset that drop down
                    if (KeyboardManagerHelper::CheckRepeatedModifier(selectedKeyCodes, selectedKeyIndex, keyCodeList))
                    {
                        // warn and reset the drop down
                        errorType = KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier;
                    }
                    // If not, add a new drop down
                    else
                    {
                        dropDownAction = BufferValidationHelpers::DropDownAction::AddDropDown;
                    }
                }
                // If last drop down and a modifier is selected but there are already max drop downs: warn the user
                else if (KeyboardManagerHelper::IsModifierKey(keyCodeList[selectedKeyIndex]) && dropDownCount >= KeyboardManagerConstants::MaxShortcutSize)
                {
                    // warn and reset the drop down
                    errorType = KeyboardManagerHelper::ErrorType::ShortcutMaxShortcutSizeOneActionKey;
                }
                // If None is selected but it's the last index: warn
                else if (keyCodeList[selectedKeyIndex] == 0)
                {
                    // If it is a hybrid control and there are 2 drop downs then deletion is allowed
                    if (isHybridControl && dropDownCount == KeyboardManagerConstants::MinShortcutSize)
                    {
                        // set delete drop down flag
                        dropDownAction = BufferValidationHelpers::DropDownAction::DeleteDropDown;
                        // do not delete the drop down now since there may be some other error which would cause the drop down to be invalid after removal
                    }
                    else
                    {
                        // warn and reset the drop down
                        errorType = KeyboardManagerHelper::ErrorType::ShortcutOneActionKey;
                    }
                }
                // Disable can not be selected if one modifier key has already been selected
                else if (keyCodeList[selectedKeyIndex] == CommonSharedConstants::VK_DISABLED && dropDownIndex)
                {
                    errorType = KeyboardManagerHelper::ErrorType::ShortcutDisableAsActionKey;
                }
                // If none of the above, then the action key will be set
            }
            // If it is not the last drop down
            else
            {
                if (KeyboardManagerHelper::IsModifierKey(keyCodeList[selectedKeyIndex]))
                {
                    // If it matched any of the previous modifiers then reset that drop down
                    if (KeyboardManagerHelper::CheckRepeatedModifier(selectedKeyCodes, selectedKeyIndex, keyCodeList))
                    {
                        // warn and reset the drop down
                        errorType = KeyboardManagerHelper::ErrorType::ShortcutCannotHaveRepeatedModifier;
                    }
                    // If not, the modifier key will be set
                }
                // If None is selected and there are more than 2 drop downs
                else if (keyCodeList[selectedKeyIndex] == 0 && dropDownCount > KeyboardManagerConstants::MinShortcutSize)
                {
                    // set delete drop down flag
                    dropDownAction = BufferValidationHelpers::DropDownAction::DeleteDropDown;
                    // do not delete the drop down now since there may be some other error which would cause the drop down to be invalid after removal
                }
                else if (keyCodeList[selectedKeyIndex] == 0 && dropDownCount <= KeyboardManagerConstants::MinShortcutSize)
                {
                    // If it is a hybrid control and there are 2 drop downs then deletion is allowed
                    if (isHybridControl && dropDownCount == KeyboardManagerConstants::MinShortcutSize)
                    {
                        // set delete drop down flag
                        dropDownAction = BufferValidationHelpers::DropDownAction::DeleteDropDown;
                        // do not delete the drop down now since there may be some other error which would cause the drop down to be invalid after removal
                    }
                    else
                    {
                        // warn and reset the drop down
                        errorType = KeyboardManagerHelper::ErrorType::ShortcutAtleast2Keys;
                    }
                }
                // Allow selection of VK_DISABLE only in first dropdown
                else if (keyCodeList[selectedKeyIndex] == CommonSharedConstants::VK_DISABLED && dropDownIndex)
                {
                    errorType = KeyboardManagerHelper::ErrorType::ShortcutDisableAsActionKey;
                }
                // If the user tries to set an action key check if all drop down menus after this are empty if it is not the first key. If it is a hybrid control, this can be done even on the first key
                else if (dropDownIndex != 0 || isHybridControl)
                {
                    bool isClear = true;
                    for (int i = dropDownIndex + 1; i < (int)dropDownCount; i++)
                    {
                        if (selectedIndices[i] != -1)
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
                        // warn and reset the drop down
                        errorType = KeyboardManagerHelper::ErrorType::ShortcutNotMoreThanOneActionKey;
                    }
                }
                // If there an action key is chosen on the first drop down and there are more than one drop down menus
                else
                {
                    // warn and reset the drop down
                    errorType = KeyboardManagerHelper::ErrorType::ShortcutStartWithModifier;
                }
            }
        }

        // After validating the shortcut, now for errors like remap to same shortcut, remap shortcut more than once, Win L and Ctrl Alt Del
        if (errorType == KeyboardManagerHelper::ErrorType::NoError)
        {
            KeyShortcutUnion tempShortcut;
            if (isHybridControl && selectedKeyCodes.size() == 1)
            {
                tempShortcut = selectedKeyCodes[0];
            }
            else
            {
                tempShortcut = Shortcut();
                std::get<Shortcut>(tempShortcut).SetKeyCodes(selectedKeyCodes);
            }

            // Convert app name to lower case
            std::transform(appName.begin(), appName.end(), appName.begin(), towlower);
            std::wstring lowercaseDefAppName = KeyboardManagerConstants::DefaultAppName;
            std::transform(lowercaseDefAppName.begin(), lowercaseDefAppName.end(), lowercaseDefAppName.begin(), towlower);
            if (appName == lowercaseDefAppName)
            {
                appName = L"";
            }

            // Check if the value being set is the same as the other column - index of other column does not have to be checked since only one column is hybrid
            if (tempShortcut.index() == 1)
            {
                // If shortcut to shortcut
                if (remapBuffer[rowIndex].first[std::abs(int(colIndex) - 1)].index() == 1)
                {
                    if (std::get<Shortcut>(remapBuffer[rowIndex].first[std::abs(int(colIndex) - 1)]) == std::get<Shortcut>(tempShortcut) && std::get<Shortcut>(remapBuffer[rowIndex].first[std::abs(int(colIndex) - 1)]).IsValidShortcut() && std::get<Shortcut>(tempShortcut).IsValidShortcut())
                    {
                        errorType = KeyboardManagerHelper::ErrorType::MapToSameShortcut;
                    }
                }

                // If one column is shortcut and other is key no warning required
            }
            else
            {
                // If key to key
                if (remapBuffer[rowIndex].first[std::abs(int(colIndex) - 1)].index() == 0)
                {
                    if (std::get<DWORD>(remapBuffer[rowIndex].first[std::abs(int(colIndex) - 1)]) == std::get<DWORD>(tempShortcut) && std::get<DWORD>(remapBuffer[rowIndex].first[std::abs(int(colIndex) - 1)]) != NULL && std::get<DWORD>(tempShortcut) != NULL)
                    {
                        errorType = KeyboardManagerHelper::ErrorType::MapToSameKey;
                    }
                }

                // If one column is shortcut and other is key no warning required
            }

            if (errorType == KeyboardManagerHelper::ErrorType::NoError && colIndex == 0)
            {
                // Check if the key is already remapped to something else for the same target app
                for (int i = 0; i < remapBuffer.size(); i++)
                {
                    std::wstring currAppName = remapBuffer[i].second;
                    std::transform(currAppName.begin(), currAppName.end(), currAppName.begin(), towlower);

                    if (i != rowIndex && currAppName == appName)
                    {
                        KeyboardManagerHelper::ErrorType result = KeyboardManagerHelper::ErrorType::NoError;
                        if (!isHybridControl)
                        {
                            result = Shortcut::DoKeysOverlap(std::get<Shortcut>(remapBuffer[i].first[colIndex]), std::get<Shortcut>(tempShortcut));
                        }
                        else
                        {
                            if (tempShortcut.index() == 0 && remapBuffer[i].first[colIndex].index() == 0)
                            {
                                if (std::get<DWORD>(tempShortcut) != NULL && std::get<DWORD>(remapBuffer[i].first[colIndex]) != NULL)
                                {
                                    result = KeyboardManagerHelper::DoKeysOverlap(std::get<DWORD>(remapBuffer[i].first[colIndex]), std::get<DWORD>(tempShortcut));
                                }
                            }
                            else if (tempShortcut.index() == 1 && remapBuffer[i].first[colIndex].index() == 1)
                            {
                                if (std::get<Shortcut>(tempShortcut).IsValidShortcut() && std::get<Shortcut>(remapBuffer[i].first[colIndex]).IsValidShortcut())
                                {
                                    result = Shortcut::DoKeysOverlap(std::get<Shortcut>(remapBuffer[i].first[colIndex]), std::get<Shortcut>(tempShortcut));
                                }
                            }
                            // Other scenarios not possible since key to shortcut is with key to key, and shortcut to key is with shortcut to shortcut
                        }
                        if (result != KeyboardManagerHelper::ErrorType::NoError)
                        {
                            errorType = result;
                            break;
                        }
                    }
                }
            }

            if (errorType == KeyboardManagerHelper::ErrorType::NoError && tempShortcut.index() == 1)
            {
                errorType = std::get<Shortcut>(tempShortcut).IsShortcutIllegal();
            }
        }

        return std::make_pair(errorType, dropDownAction);
    }
}
