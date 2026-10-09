#pragma once

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
    winrt::Windows::Foundation::IAsyncOperation<bool> PartialRemappingConfirmationDialog(winrt::Windows::UI::Xaml::XamlRoot root, std::wstring dialogTitle);
};
