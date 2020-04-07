#include "pch.h"
#include "LayoutMap.h"

std::wstring LayoutMap::GetKeyName(DWORD key)
{
    std::lock_guard<std::mutex> lock(keyboardLayoutMap_mutex);
    if (keyboardLayoutMap.find(key) != keyboardLayoutMap.end())
    {
        return keyboardLayoutMap[key];
    }
    else
    {
        return L"Undefined";
    }
}