#pragma once

#include <mmdeviceapi.h>
#include <endpointvolume.h>

#include "Overlay.h"

class CVolumeNotification : public IAudioEndpointVolumeCallback
{
public:
    CVolumeNotification(void);

    STDMETHODIMP_(ULONG)
    AddRef();
    STDMETHODIMP_(ULONG)
    Release();
    STDMETHODIMP QueryInterface(REFIID IID, void** ReturnValue);
    STDMETHODIMP OnNotify(PAUDIO_VOLUME_NOTIFICATION_DATA NotificationData);

private:
    ~CVolumeNotification(void){};
    LONG m_RefCount;
};