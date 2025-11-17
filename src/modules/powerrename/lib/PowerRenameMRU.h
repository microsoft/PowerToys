#pragma once
#include "pch.h"
#include "PowerRenameInterfaces.h"
#include "MRUListHandler.h"

#include <string>
#include <memory>
#include <vector>

class CPowerRenameMRU :
    public IPowerRenameMRU
{
public:
    // IUnknown
    IFACEMETHODIMP_(ULONG)
    AddRef();
    IFACEMETHODIMP_(ULONG)
    Release();
    IFACEMETHODIMP QueryInterface(_In_ REFIID riid, _Outptr_ void** ppv);

    // IPowerRenameMRU
    IFACEMETHODIMP AddMRUString(_In_ PCWSTR entry);
    IFACEMETHODIMP_(const std::vector<std::wstring>&) GetMRUStrings();

    static HRESULT CPowerRenameMRUSearch_CreateInstance(_Outptr_ IPowerRenameMRU** ppUnk);
    static HRESULT CPowerRenameMRUReplace_CreateInstance(_Outptr_ IPowerRenameMRU** ppUnk);

private:
    static HRESULT CreateInstance(_In_ const std::wstring& filePath, _In_ const std::wstring& regPath, _In_ REFIID iid, _Outptr_ void** resultInterface);
    CPowerRenameMRU(int size, const std::wstring& filePath, const std::wstring& regPath);

    std::unique_ptr<MRUListHandler> mruList;
    unsigned int refCount = 0;
};
