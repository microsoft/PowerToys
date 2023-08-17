#include "pch.h"
#include "PowerRenameEnum.h"
#include <ShlGuid.h>
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

IFACEMETHODIMP CPowerRenameEnum::Start(_In_ IEnumShellItems* enumShellItems)
{
    m_canceled = false;
    HRESULT hr = _ParseEnumItems(enumShellItems);

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
            items.push_back(std::move(spsi));
            spsi = nullptr;
        }

        auto cmpShellItems = [](const CComPtr<IShellItem>& l, const CComPtr<IShellItem>& r) {
            int res = 0;
            l->Compare(r, SICHINT_DISPLAY, &res);
            return res < 0;
        };

        // We need to sort only the first layer, because later ones are enumerated correctly
        if (depth == 0)
            std::sort(begin(items), end(items), cmpShellItems);

        for (const auto& item : items)
        {
            if (m_canceled)
            {
                return E_ABORT;
            }

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
        }
    }

    return hr;
}
