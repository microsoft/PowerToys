#pragma once
#include "LayoutMapManaged.g.h"
#include "keyboard_layout.h"

namespace winrt::interop::implementation
{
    struct LayoutMapManaged : LayoutMapManagedT<LayoutMapManaged>
    {
        LayoutMapManaged() = default;

        hstring GetKeyName(int32_t key);
        int32_t GetKeyValue(hstring const& name);
        void Updatelayout();

        private:
        std::unique_ptr<LayoutMap> _map = std::make_unique<LayoutMap>();
    };
}
namespace winrt::interop::factory_implementation
{
    struct LayoutMapManaged : LayoutMapManagedT<LayoutMapManaged, implementation::LayoutMapManaged>
    {
    };
}
