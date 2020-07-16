#pragma once
#include <vector>
#include <keyboardmanager/common/Helpers.h>
#include <variant>

namespace winrt::Windows::UI::Xaml
{
    namespace Foundation
    {
        template<typename T>
        struct IAsyncOperation;
    }
    namespace UI::Xaml
    {
        struct XamlRoot;
    }
}

namespace Dialog
{
    KeyboardManagerHelper::ErrorType CheckIfRemappingsAreValid(const std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>>& remappings);

    winrt::Windows::Foundation::IAsyncOperation<bool> PartialRemappingConfirmationDialog(winrt::Windows::UI::Xaml::XamlRoot root, std::wstring dialogTitle);
};
