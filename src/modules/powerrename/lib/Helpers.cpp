#include "stdafx.h"
#include "Helpers.h"
#include <ShlGuid.h>

HRESULT _ParseEnumItems(_In_ IEnumShellItems* pesi, _In_ IPowerRenameManager* psrm, _In_ int depth = 0)
{
    HRESULT hr = E_INVALIDARG;

    // We shouldn't get this deep since we only enum the contents of
    // regular folders but adding just in case
    if ((pesi) && (depth < (MAX_PATH / 2)))
    {
        hr = S_OK;

        ULONG celtFetched;
        CComPtr<IShellItem> spsi;
        while ((S_OK == pesi->Next(1, &spsi, &celtFetched)) && (SUCCEEDED(hr)))
        {
            CComPtr<IPowerRenameItemFactory> spsrif;
            hr = psrm->get_renameItemFactory(&spsrif);
            if (SUCCEEDED(hr))
            {
                CComPtr<IPowerRenameItem> spNewItem;
                hr = spsrif->Create(spsi, &spNewItem);
                if (SUCCEEDED(hr))
                {
                    spNewItem->put_depth(depth);
                    hr = psrm->AddItem(spNewItem);
                }

                if (SUCCEEDED(hr))
                {
                    bool isFolder = false;
                    if (SUCCEEDED(spNewItem->get_isFolder(&isFolder)) && isFolder)
                    {
                        // Bind to the IShellItem for the IEnumShellItems interface
                        CComPtr<IEnumShellItems> spesiNext;
                        hr = spsi->BindToHandler(nullptr, BHID_EnumItems, IID_PPV_ARGS(&spesiNext));
                        if (SUCCEEDED(hr))
                        {
                            // Parse the folder contents recursively
                            hr = _ParseEnumItems(spesiNext, psrm, depth + 1);
                        }
                    }
                }
            }

            spsi = nullptr;
        }
    }

    return hr;
}

// Iterate through the data source and add paths to the rotation manager
HRESULT EnumerateDataObject(_In_ IUnknown* dataSource, _In_ IPowerRenameManager* psrm)
{
    CComPtr<IShellItemArray> spsia;
    IDataObject* dataObj{};
    HRESULT hr;
    if (SUCCEEDED(dataSource->QueryInterface(IID_IDataObject, reinterpret_cast<void**>(&dataObj))))
    {
        hr = SHCreateShellItemArrayFromDataObject(dataObj, IID_PPV_ARGS(&spsia));
    }
    else
    {
        hr = dataSource->QueryInterface(IID_IShellItemArray, reinterpret_cast<void**>(&spsia));
    }
    if (SUCCEEDED(hr))
    {
        CComPtr<IEnumShellItems> spesi;
        hr = spsia->EnumItems(&spesi);
        if (SUCCEEDED(hr))
        {
            hr = _ParseEnumItems(spesi, psrm);
        }
    }

    return hr;
}

BOOL GetEnumeratedFileName(__out_ecount(cchMax) PWSTR pszUniqueName, UINT cchMax, __in PCWSTR pszTemplate, __in_opt PCWSTR pszDir, unsigned long ulMinLong, __inout unsigned long* pulNumUsed)
{
    PWSTR pszName = nullptr;
    HRESULT hr = S_OK;
    BOOL fRet = FALSE;
    int cchDir = 0;

    if (0 != cchMax && pszUniqueName)
    {
        *pszUniqueName = 0;
        if (pszDir)
        {
            hr = StringCchCopy(pszUniqueName, cchMax, pszDir);
            if (SUCCEEDED(hr))
            {
                hr = PathCchAddBackslashEx(pszUniqueName, cchMax, &pszName, nullptr);
                if (SUCCEEDED(hr))
                {
                    cchDir = lstrlen(pszDir);
                }
            }
        }
        else
        {
            cchDir = 0;
            pszName = pszUniqueName;
        }
    }
    else
    {
        hr = E_INVALIDARG;
    }

    int cchTmp = 0;
    int cchStem = 0;
    PCWSTR pszStem = nullptr;
    PCWSTR pszRest = nullptr;
    wchar_t szFormat[MAX_PATH] = { 0 };

    if (SUCCEEDED(hr))
    {
        pszStem = pszTemplate;

        pszRest = StrChr(pszTemplate, L'(');
        while (pszRest)
        {
            PCWSTR pszEndUniq = CharNext(pszRest);
            while (*pszEndUniq && *pszEndUniq >= L'0' && *pszEndUniq <= L'9')
            {
                pszEndUniq++;
            }

            if (*pszEndUniq == L')')
            {
                break;
            }

            pszRest = StrChr(CharNext(pszRest), L'(');
        }

        if (!pszRest)
        {
            pszRest = PathFindExtension(pszTemplate);
            cchStem = (int)(pszRest - pszTemplate);

            hr = StringCchCopy(szFormat, ARRAYSIZE(szFormat), L" (%lu)");
        }
        else
        {
            pszRest++;

            cchStem = (int)(pszRest - pszTemplate);

            while (*pszRest && *pszRest >= L'0' && *pszRest <= L'9')
            {
                pszRest++;
            }

            hr = StringCchCopy(szFormat, ARRAYSIZE(szFormat), L"%lu");
        }
    }

    unsigned long ulMax = 0;
    unsigned long ulMin = 0;
    if (SUCCEEDED(hr))
    {
        int cchFormat = lstrlen(szFormat);
        if (cchFormat < 3)
        {
            *pszUniqueName = L'\0';
            return FALSE;
        }
        ulMin = ulMinLong;
        cchTmp = cchMax - cchDir - cchStem - (cchFormat - 3);
        switch (cchTmp)
        {
        case 1:
            ulMax = 10;
            break;
        case 2:
            ulMax = 100;
            break;
        case 3:
            ulMax = 1000;
            break;
        case 4:
            ulMax = 10000;
            break;
        case 5:
            ulMax = 100000;
            break;
        default:
            if (cchTmp <= 0)
            {
                ulMax = ulMin;
            }
            else
            {
                ulMax = 1000000;
            }
            break;
        }
    }

    if (SUCCEEDED(hr))
    {
        hr = StringCchCopyN(pszName, pszUniqueName + cchMax - pszName, pszStem, cchStem);
        if (SUCCEEDED(hr))
        {
            PWSTR pszDigit = pszName + cchStem;

            for (unsigned long ul = ulMin; ((ul < ulMax) && (!fRet)); ul++)
            {
                wchar_t szTemp[MAX_PATH] = { 0 };
                hr = StringCchPrintf(szTemp, ARRAYSIZE(szTemp), szFormat, ul);
                if (SUCCEEDED(hr))
                {
                    hr = StringCchCat(szTemp, ARRAYSIZE(szTemp), pszRest);
                    if (SUCCEEDED(hr))
                    {
                        hr = StringCchCopy(pszDigit, pszUniqueName + cchMax - pszDigit, szTemp);
                        if (SUCCEEDED(hr))
                        {
                            if (!PathFileExists(pszUniqueName))
                            {
                                (*pulNumUsed) = ul;
                                fRet = TRUE;
                            }
                        }
                    }
                }
            }
        }
    }

    if (!fRet)
    {
        *pszUniqueName = L'\0';
    }

    return fRet;
}
