#pragma once

#include <windows.h>
#include <string>
#include <vector>

namespace PowerRenameLib
{
    HRESULT LoadNumericRenameMappingFromCsv(_In_ PCWSTR path, _Out_ std::vector<std::wstring>& names);
    HRESULT LoadNumericRenameMappingFromXlsx(_In_ PCWSTR path, _Out_ std::vector<std::wstring>& names);
    bool IsValidNumericRenameName(_In_ PCWSTR name);
    bool TryGetNumericFileStem(_In_ PCWSTR fileName, _Out_ unsigned long long& number);
}
