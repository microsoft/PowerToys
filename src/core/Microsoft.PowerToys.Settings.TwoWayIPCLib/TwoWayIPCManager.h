// TwoWayIPCManager.h : Declaration of the CTwoWayIPCManager

#pragma once
#include "resource.h" // main symbols

#include "TwoWayIPCLib_i.h"
#include "_ITwoWayIPCManagerEvents_CP.h"
#include "common/two_way_pipe_message_ipc.h"

#if defined(_WIN32_WCE) && !defined(_CE_DCOM) && !defined(_CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA)
#error "Single-threaded COM objects are not properly supported on Windows CE platform, such as the Windows Mobile platforms that do not include full DCOM support. Define _CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA to force ATL to support creating single-thread COM object's and allow use of it's single-threaded COM object implementations. The threading model in your rgs file was set to 'Free' as that is the only threading model supported in non DCOM Windows CE platforms."
#endif

using namespace ATL;

// CTwoWayIPCManager

class ATL_NO_VTABLE CTwoWayIPCManager :
    public CComObjectRootEx<CComSingleThreadModel>,
    public CComCoClass<CTwoWayIPCManager, &CLSID_TwoWayIPCManager>,
    public IConnectionPointContainerImpl<CTwoWayIPCManager>,
    public CProxy_ITwoWayIPCManagerEvents<CTwoWayIPCManager>,
    public IDispatchImpl<ITwoWayIPCManager, &IID_ITwoWayIPCManager, &LIBID_TwoWayIPCLibLib, /*wMajor =*/1, /*wMinor =*/0>
{
public:
    CTwoWayIPCManager();
    ~CTwoWayIPCManager();

    DECLARE_REGISTRY_RESOURCEID(106)

    BEGIN_COM_MAP(CTwoWayIPCManager)
    COM_INTERFACE_ENTRY(ITwoWayIPCManager)
    COM_INTERFACE_ENTRY(IDispatch)
    COM_INTERFACE_ENTRY(IConnectionPointContainer)
    END_COM_MAP()

    BEGIN_CONNECTION_POINT_MAP(CTwoWayIPCManager)
    CONNECTION_POINT_ENTRY(__uuidof(_ITwoWayIPCManagerEvents))
    END_CONNECTION_POINT_MAP()

    DECLARE_PROTECT_FINAL_CONSTRUCT()

    HRESULT FinalConstruct()
    {
        return S_OK;
    }

    void FinalRelease()
    {
    }

    STDMETHOD(SendMessage)
    (BSTR message);

    STDMETHOD(Initialize)
    (BSTR runnerPipeName, BSTR settingsPipeName);

private:
    TwoWayPipeMessageIPC* m_MessagePipe;
    BSTR m_message_from_runner;
};

OBJECT_ENTRY_AUTO(__uuidof(TwoWayIPCManager), CTwoWayIPCManager)
