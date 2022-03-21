#pragma once

#include <MMDeviceAPI.h>

struct AudioDeviceNotificationClient : IMMNotificationClient
{
    AudioDeviceNotificationClient();
    ~AudioDeviceNotificationClient();

    bool PullPendingNotifications()
    {
        const bool result = _deviceConfigurationChanged;
        _deviceConfigurationChanged = false;
        return result;
    }

private:
    ULONG AddRef() override;
    ULONG Release() override;
    HRESULT QueryInterface(REFIID, void**) override;
    HRESULT OnPropertyValueChanged(LPCWSTR, const PROPERTYKEY) override;
    HRESULT OnDeviceAdded(LPCWSTR) override;
    HRESULT OnDeviceRemoved(LPCWSTR) override;
    HRESULT OnDeviceStateChanged(LPCWSTR, DWORD) override;
    HRESULT OnDefaultDeviceChanged(EDataFlow flow, ERole role, LPCWSTR) override;

    IMMDeviceEnumerator* _deviceEnumerator = nullptr;

    bool _deviceConfigurationChanged = false;
};
