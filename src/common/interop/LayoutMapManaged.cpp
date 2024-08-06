#include "pch.h"
#include "LayoutMapManaged.h"
#include "LayoutMapManaged.g.cpp"

namespace winrt::PowerToys::Interop::implementation
{
    hstring LayoutMapManaged::GetKeyName(uint32_t key)
    {
        return hstring{ _map->GetKeyName(key) };
    }
    uint32_t LayoutMapManaged::GetKeyValue(hstring const& name)
    {
        return _map->GetKeyFromName(std::wstring(name));
    }
    void LayoutMapManaged::Updatelayout()
    {
        _map->UpdateLayout();
    }
}
