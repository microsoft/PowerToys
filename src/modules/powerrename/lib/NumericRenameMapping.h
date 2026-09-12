#pragma once

#include <windows.h>
#include <map>
#include <string>

namespace PowerRenameLib
{
    using NumericRenameMapping = std::map<unsigned long long, std::wstring>;

    HRESULT LoadNumericRenameMappingFromCsv(_In_ PCWSTR path, _Out_ NumericRenameMapping& mappings);
    HRESULT LoadNumericRenameMappingFromText(_In_ PCWSTR path, _Out_ NumericRenameMapping& mappings);
    HRESULT LoadNumericRenameMappingFromXlsx(_In_ PCWSTR path, _Out_ NumericRenameMapping& mappings);
    bool IsValidNumericRenameName(_In_ PCWSTR name);
    bool TryGetNumericFileStem(_In_ PCWSTR fileName, _Out_ unsigned long long& number);
}
