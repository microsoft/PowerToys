#include "pch.h"
#include "CVolumeNotification.h"

CVolumeNotification::CVolumeNotification(void) :
    m_RefCount(1)
{
}

STDMETHODIMP_(ULONG __stdcall) CVolumeNotification::AddRef()
{
    return InterlockedIncrement(&m_RefCount);
}

STDMETHODIMP_(ULONG __stdcall) CVolumeNotification::Release()
{
    LONG ref = InterlockedDecrement(&m_RefCount);
    if (ref == 0)
        delete this;
    return ref;
}

STDMETHODIMP_(HRESULT __stdcall) CVolumeNotification::QueryInterface(REFIID IID, void** ReturnValue)
{
    if (IID == IID_IUnknown || IID == __uuidof(IAudioEndpointVolumeCallback))
    {
        *ReturnValue = static_cast<IUnknown*>(this);
        AddRef();
        return S_OK;
    }
    *ReturnValue = NULL;
    return E_NOINTERFACE;
}

STDMETHODIMP_(HRESULT __stdcall) CVolumeNotification::OnNotify(PAUDIO_VOLUME_NOTIFICATION_DATA NotificationData)
{
    Overlay::setMicrophoneMute(NotificationData->bMuted);

    return S_OK;
}
