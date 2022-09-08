// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved

// ExplorerCommandState handlers are not a verb implementation method, it is a way to give a dynamic
// behavior to a static verb implementation, that is show or not. This handler is run on a background thread
// to avoid UI hangs. So, for example you can combine it with ExecuteCommand, DropTarget or CreateProcess
// verb to give them dynamic behavior. Only use this method if you cannot express your dynamic behavior
// using an AQS expression. There is a limitation to this in that this cannot be used on the default verb.

#include "Dll.h"

class CExplorerCommandStateHandler
    : public IExplorerCommandState, public IInitializeCommand
{
public:
    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void **ppv)
    {
        static const QITAB qit[] =
        {
            QITABENT(CExplorerCommandStateHandler, IExplorerCommandState), // required
            QITABENT(CExplorerCommandStateHandler, IInitializeCommand),    // optional
            // QITABENT(CExplorerCommandStateHandler, IObjectWithSite),    // optional. the site can be used to get the explorer browser or view. not implemented in this sample
            { 0 },
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef()
    {
        return InterlockedIncrement(&_cRef);
    }

    IFACEMETHODIMP_(ULONG) Release()
    {
        long cRef = InterlockedDecrement(&_cRef);
        if (!cRef)
        {
            delete this;
        }
        return cRef;
    }

    // IExplorerCommandState

    // compute the visibility of the verb here, respect "fOkToBeSlow" if this is slow
    // when called with fOkToBeSlow == FALSE return E_PENDING and this object will be called
    // back on a background thread with fOkToBeSlow == TRUE
    IFACEMETHODIMP GetState(IShellItemArray * /* psiItemArray */, BOOL fOkToBeSlow, EXPCMDSTATE *pCmdState)
    {
        HRESULT hr;
        if (fOkToBeSlow)
        {
            Sleep(4 * 1000);    // simulate expensive work
            *pCmdState = ECS_ENABLED;
            hr = S_OK;
        }
        else
        {
            *pCmdState = ECS_DISABLED;
            // returning E_PENDING requests that a new instance of this object be called back
            // on a background thread so that it can do work that might be slow
            hr = E_PENDING;
        }
        return hr;
    }

    //  IInitializeCommand
    IFACEMETHODIMP Initialize(PCWSTR pszCommandName, IPropertyBag *ppb)
    {
        SetInterface(&_pPropBag, ppb);
        return SHStrDup(pszCommandName, &_pszCommandName);
    }

    CExplorerCommandStateHandler() : _cRef(1), _pPropBag(NULL), _pszCommandName(NULL)
    {
        DllAddRef();
    }

private:
    virtual ~CExplorerCommandStateHandler()
    {
        SafeRelease(&_pPropBag);
        CoTaskMemFree(_pszCommandName);
        DllRelease();
    }
    long _cRef;
    IPropertyBag *_pPropBag;
    PWSTR _pszCommandName;
};

HRESULT CExplorerCommandStateHandler_CreateInstance(REFIID riid, void **ppv)
{
    *ppv = NULL;
    CExplorerCommandStateHandler *pVerbState = new (std::nothrow) CExplorerCommandStateHandler();
    HRESULT hr = pVerbState ? S_OK : E_OUTOFMEMORY;
    if (SUCCEEDED(hr))
    {
        pVerbState->QueryInterface(riid, ppv);
        pVerbState->Release();
    }
    return hr;
}

static WCHAR const c_szVerbDisplayName[] = L"CommandState Handler Verb";
static WCHAR const c_szVerbName[] = L"Sample.ExplorerCommandStateHandlerVerb";
static WCHAR const c_szProgID[] = L"txtfile";

HRESULT CExplorerCommandStateHandler_RegisterUnRegister(bool fRegister)
{
    HRESULT hr;
    if (fRegister)
    {
        // register a create process based verb. this could also be a delegate execute
        // or drop target verb
        CRegisterExtension registerCreateProcess(CLSID_NULL);
        hr = registerCreateProcess.RegisterCreateProcessVerb(c_szProgID, c_szVerbName, L"notepad.exe %1", c_szVerbDisplayName);
        if (SUCCEEDED(hr))
        {
            hr = registerCreateProcess.RegisterVerbAttribute(c_szProgID, c_szVerbName, L"NeverDefault");
            if (SUCCEEDED(hr))
            {
                // now register the command state handler, this computes if this verb is enabled or not

                CRegisterExtension re(__uuidof(CExplorerCommandStateHandler));

                hr = re.RegisterInProcServer(c_szVerbDisplayName, L"Apartment");
                if (SUCCEEDED(hr))
                {
                    hr = re.RegisterExplorerCommandStateHandler(c_szProgID, c_szVerbName);
                }
            }
        }
    }
    else
    {
        // best effort
        CRegisterExtension registerCreateProcess(CLSID_NULL);
        hr = registerCreateProcess.UnRegisterVerb(c_szProgID, c_szVerbName);

        CRegisterExtension re(__uuidof(CExplorerCommandStateHandler));
        hr = re.UnRegisterObject();
    }
    return hr;
}
