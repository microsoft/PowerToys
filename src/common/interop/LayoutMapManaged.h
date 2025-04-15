#pragma once
#include "LayoutMapManaged.g.h"
#include "keyboard_layout.h"

namespace winrt::PowerToys::Interop::implementation
{
    struct LayoutMapManaged : LayoutMapManagedT<LayoutMapManaged>
    {
        LayoutMapManaged() = default;

        hstring GetKeyName(uint32_t key);
        uint32_t GetKeyValue(hstring const& name);
        void Updatelayout();

        private:
        std::unique_ptr<LayoutMap> _map = std::make_unique<LayoutMap>();
    };
}
namespace winrt::PowerToys::Interop::factory_implementation
{
    struct LayoutMapManaged : LayoutMapManagedT<LayoutMapManaged, implementation::LayoutMapManaged>
    {
    };
}
