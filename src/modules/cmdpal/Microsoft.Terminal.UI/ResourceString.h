// #pragma once

// #include "ResourceString.g.h"

// namespace winrt::Microsoft::Terminal::UI::implementation
// {
//     struct ResourceString : ResourceStringT<ResourceString>
//     {
//         ResourceString() noexcept = default;

//         hstring Tree()
//         {
//             return tree_;
//         }

//         void Tree(hstring const& value)
//         {
//             tree_ = value;
//         }

//         hstring Name()
//         {
//             return name_;
//         }

//         void Name(hstring const& value)
//         {
//             name_ = value;
//         }

//         winrt::Windows::Foundation::IInspectable ProvideValue();

//     private:
//         winrt::hstring tree_;
//         winrt::hstring name_;
//     };
// }
// namespace winrt::Microsoft::Terminal::UI::factory_implementation
// {
//     struct ResourceString : ResourceStringT<ResourceString, implementation::ResourceString>
//     {
//     };
// }
