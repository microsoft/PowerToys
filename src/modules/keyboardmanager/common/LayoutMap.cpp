#include "pch.h"
#include "LayoutMap.h"

std::wstring LayoutMap::GetKeyName(DWORD key)
{
    std::wstring result = L"Undefined";
    std::lock_guard<std::mutex> lock(keyboardLayoutMap_mutex);
    auto it = keyboardLayoutMap.find(key);
    if (it != keyboardLayoutMap.end())
    {
        result = it->second;
    }
    return result;
}
