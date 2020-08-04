#include "pch.h"
#include "BufferValidationHelpers.h"

namespace BufferValidationHelpers
{
    // Function to validate an element of the key remap buffer when the selection has changed
    KeyboardManagerHelper::ErrorType ValidateAndUpdateKeyBufferElement(int rowIndex, int colIndex, int selectedKeyIndex, std::vector<DWORD>& keyCodeList, std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>>& remapBuffer)
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
}
