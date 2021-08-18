#include "pch.h"
#include "PowerRenameEnum.h"
#include <algorithm>
#include <ShlGuid.h>
#include <string>
#include <helpers.h>

IFACEMETHODIMP_(ULONG) CPowerRenameEnum::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG) CPowerRenameEnum::Release()
{
    long refCount = InterlockedDecrement(&m_refCount);

    if (refCount == 0)
    {
        delete this;
    }
    return refCount;
}

IFACEMETHODIMP CPowerRenameEnum::QueryInterface(_In_ REFIID riid, _Outptr_ void** ppv)
{
    static const QITAB qit[] = {
        QITABENT(CPowerRenameEnum, IPowerRenameEnum),
        { 0 }
    };
    return QISearch(this, qit, riid, ppv);
}

IFACEMETHODIMP CPowerRenameEnum::Start()
{
    m_canceled = false;
    CComPtr<IShellItemArray> spsia;
    HRESULT hr = GetShellItemArrayFromDataObject(m_spdo, &spsia);
    if (SUCCEEDED(hr))
    {
        CComPtr<IEnumShellItems> spesi;
        hr = spsia->EnumItems(&spesi);
        if (SUCCEEDED(hr))
        {
            hr = _ParseEnumItems(spesi);
        }
    }

    return hr;
}

IFACEMETHODIMP CPowerRenameEnum::Cancel()
{
    m_canceled = true;
    return S_OK;
}

HRESULT CPowerRenameEnum::s_CreateInstance(_In_ IUnknown* pdo, _In_ IPowerRenameManager* pManager, _In_ REFIID iid, _Outptr_ void** resultInterface)
{
    *resultInterface = nullptr;

    CPowerRenameEnum* newRenameEnum = new CPowerRenameEnum();
    HRESULT hr = newRenameEnum ? S_OK : E_OUTOFMEMORY;
    if (SUCCEEDED(hr))
    {
        hr = newRenameEnum->_Init(pdo, pManager);
        if (SUCCEEDED(hr))
        {
            hr = newRenameEnum->QueryInterface(iid, resultInterface);
        }

        newRenameEnum->Release();
    }
    return hr;
}

CPowerRenameEnum::CPowerRenameEnum() :
    m_refCount(1)
{
}

CPowerRenameEnum::~CPowerRenameEnum()
{
}

HRESULT CPowerRenameEnum::_Init(_In_ IUnknown* pdo, _In_ IPowerRenameManager* pManager)
{
    m_spdo = pdo;
    m_spsrm = pManager;
    return S_OK;
}

HRESULT CPowerRenameEnum::_ParseEnumItems(_In_ IEnumShellItems* pesi, _In_ int depth)
{
    HRESULT hr = E_INVALIDARG;

    // We shouldn't get this deep since we only enum the contents of
    // regular folders but adding just in case
    if ((pesi) && (depth < (MAX_PATH / 2)))
    {
        hr = S_OK;

        ULONG celtFetched;
        CComPtr<IShellItem> spsi;
        std::vector<CComPtr<IShellItem>> items;

        while ((S_OK == pesi->Next(1, &spsi, &celtFetched)))
        {
            if (m_canceled)
            {
                return E_ABORT;
            }

            items.push_back(spsi);
        }

        auto cmpShellItems = [](CComPtr<IShellItem> l, CComPtr<IShellItem> r) {
            std::wstring lName, rName;
            PWSTR path, name;

            l->GetDisplayName(SIGDN_FILESYSPATH, &path);
            SHStrDup(PathFindFileName(path), &name);
            lName = std::wstring{ name };

            r->GetDisplayName(SIGDN_FILESYSPATH, &path);
            SHStrDup(PathFindFileName(path), &name);
            rName = std::wstring{ name };

            return lName < rName;
        };
        std::sort(items.begin(), items.end(), cmpShellItems);

        for (const auto& item : items)
        {
            CComPtr<IPowerRenameItemFactory> spFactory;
            hr = m_spsrm->GetRenameItemFactory(&spFactory);
            if (SUCCEEDED(hr))
            {
                CComPtr<IPowerRenameItem> spNewItem;
                // Failure may be valid if we come across a shell item that does
                // not support a file system path.  In that case we simply ignore
                // the item.
                if (SUCCEEDED(spFactory->Create(item, &spNewItem)))
                {
                    spNewItem->PutDepth(depth);
                    hr = m_spsrm->AddItem(spNewItem);

                    if (SUCCEEDED(hr))
                    {
                        bool isFolder = false;
                        if (SUCCEEDED(spNewItem->GetIsFolder(&isFolder)) && isFolder)
                        {
                            // Bind to the IShellItem for the IEnumShellItems interface
                            CComPtr<IEnumShellItems> spesiNext;
                            hr = item->BindToHandler(nullptr, BHID_EnumItems, IID_PPV_ARGS(&spesiNext));
                            if (SUCCEEDED(hr))
                            {
                                // Parse the folder contents recursively
                                hr = _ParseEnumItems(spesiNext, depth + 1);
                            }
                        }
                    }
                }
            }
            if (FAILED(hr))
            {
                break;
            }

            spsi = nullptr;
        }
    }

    return hr;
}
