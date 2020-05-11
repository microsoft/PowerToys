#pragma once
#include <vector>
#include <functional>
#include <keyboardmanager/common/Helpers.h>
#include <set>
#include <winrt/Windows.UI.Xaml.h>

using namespace winrt::Windows::Foundation;

namespace Dialog
{
    template<typename T>
    KeyboardManagerHelper::ErrorType CheckIfRemappingsAreValid(
        const std::vector<std::vector<T>>& remappings,
        std::function<bool(T)> isValid)
    {
        KeyboardManagerHelper::ErrorType isSuccess = KeyboardManagerHelper::ErrorType::NoError;
        std::set<T> ogKeys;
        for (int i = 0; i < remappings.size(); i++)
        {
            T ogKey = remappings[i][0];
            T newKey = remappings[i][1];

            if (isValid(ogKey) && isValid(newKey) && ogKeys.find(ogKey) == ogKeys.end())
            {
                ogKeys.insert(ogKey);
            }
            else if (isValid(ogKey) && isValid(newKey) && ogKeys.find(ogKey) != ogKeys.end())
            {
                isSuccess = KeyboardManagerHelper::ErrorType::RemapUnsuccessful;
            }
            else
            {
                isSuccess = KeyboardManagerHelper::ErrorType::RemapUnsuccessful;
            }
        }
        return isSuccess;
    }

    IAsyncOperation<bool> PartialRemappingConfirmationDialog(winrt::Windows::UI::Xaml::XamlRoot root);
};
