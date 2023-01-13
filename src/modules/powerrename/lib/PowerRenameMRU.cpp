#include "pch.h"
#include "PowerRenameMRU.h"

#include "Settings.h"

namespace
{
    const wchar_t c_searchMRUListFilePath[] = L"\\search-mru.json";
    const wchar_t c_replaceMRUListFilePath[] = L"\\replace-mru.json";
    const wchar_t c_mruSearchRegPath[] = L"\\SearchMRU";
    const wchar_t c_mruReplaceRegPath[] = L"\\ReplaceMRU";
}

CPowerRenameMRU::CPowerRenameMRU(int size, const std::wstring& filePath, const std::wstring& regPath) :
    refCount(1)
{
    mruList = std::make_unique<MRUListHandler>(size, filePath, regPath);
}

HRESULT CPowerRenameMRU::CreateInstance(_In_ const std::wstring& filePath, _In_ const std::wstring& regPath, _In_ REFIID iid, _Outptr_ void** resultInterface)
{
    *resultInterface = nullptr;
    unsigned int maxMRUSize = CSettingsInstance().GetMaxMRUSize();
    HRESULT hr = E_FAIL;
    if (maxMRUSize > 0)
    {
        CPowerRenameMRU* renameMRU = new CPowerRenameMRU(maxMRUSize, filePath, regPath);
        hr = E_OUTOFMEMORY;
        if (renameMRU)
        {
            renameMRU->QueryInterface(iid, resultInterface);
            renameMRU->Release();
            hr = S_OK;
        }
    }

    return hr;
}

IFACEMETHODIMP_(ULONG)
CPowerRenameMRU::AddRef()
{
    return InterlockedIncrement(&refCount);
}

IFACEMETHODIMP_(ULONG)
CPowerRenameMRU::Release()
{
    unsigned int cnt = InterlockedDecrement(&refCount);

    if (cnt == 0)
    {
        delete this;
    }
    return cnt;
}

IFACEMETHODIMP CPowerRenameMRU::QueryInterface(_In_ REFIID riid, _Outptr_ void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(CPowerRenameMRU, IEnumString),
        QITABENT(CPowerRenameMRU, IPowerRenameMRU),
        { 0 }
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP_(const std::vector<std::wstring>&) CPowerRenameMRU::GetMRUStrings()
{
    return mruList->GetItems();
}

IFACEMETHODIMP CPowerRenameMRU::AddMRUString(_In_ PCWSTR entry)
{
    mruList->Push(entry);
    return S_OK;
}

HRESULT CPowerRenameMRU::CPowerRenameMRUSearch_CreateInstance(_Outptr_ IPowerRenameMRU** ppUnk)
{
    return CPowerRenameMRU::CreateInstance(c_searchMRUListFilePath, c_mruSearchRegPath, IID_PPV_ARGS(ppUnk));
}

HRESULT CPowerRenameMRU::CPowerRenameMRUReplace_CreateInstance(_Outptr_ IPowerRenameMRU** ppUnk)
{
    return CPowerRenameMRU::CreateInstance(c_replaceMRUListFilePath, c_mruReplaceRegPath, IID_PPV_ARGS(ppUnk));
}
