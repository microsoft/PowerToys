#pragma once
#include <string>
#include <vector>
#include <memory>
#include <Windows.h>

class LayoutMap
{
public:
    LayoutMap();
    ~LayoutMap();
    void UpdateLayout();
    std::wstring GetKeyName(DWORD key);
    DWORD GetKeyFromName(const std::wstring& name);
    std::vector<DWORD> GetKeyCodeList(const bool isShortcut = false);
    std::vector<std::pair<DWORD, std::wstring>> GetKeyNameList(const bool isShortcut = false);

private:
    class LayoutMapImpl;
    LayoutMapImpl* impl;
};
