#include "pch.h"

#include "AudioDeviceNotificationClient.h"

AudioDeviceNotificationClient::AudioDeviceNotificationClient()
{
    (void)CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&_deviceEnumerator));
    if (!_deviceEnumerator)
    {
        return;
    }

    if (FAILED(_deviceEnumerator->RegisterEndpointNotificationCallback(this)))
    {
        _deviceEnumerator->Release();
        _deviceEnumerator = nullptr;
    }
}

AudioDeviceNotificationClient::~AudioDeviceNotificationClient()
{
    if (!_deviceEnumerator)
    {
        return;
    }

    _deviceEnumerator->UnregisterEndpointNotificationCallback(this);
    _deviceEnumerator->Release();
}

ULONG AudioDeviceNotificationClient::AddRef()
{
    return 1;
}

ULONG AudioDeviceNotificationClient::Release()
{
    return 1;
}

HRESULT AudioDeviceNotificationClient::QueryInterface(REFIID, void**)
{
    return S_OK;
}

HRESULT AudioDeviceNotificationClient::OnPropertyValueChanged(LPCWSTR, const PROPERTYKEY)
{
    _deviceConfigurationChanged = true;
    return S_OK;
}

HRESULT AudioDeviceNotificationClient::OnDeviceAdded(LPCWSTR)
{
    _deviceConfigurationChanged = true;
    return S_OK;
}

HRESULT AudioDeviceNotificationClient::OnDeviceRemoved(LPCWSTR)
{
    _deviceConfigurationChanged = true;
    return S_OK;
}

HRESULT AudioDeviceNotificationClient::OnDeviceStateChanged(LPCWSTR, DWORD)
{
    _deviceConfigurationChanged = true;
    return S_OK;
}

HRESULT AudioDeviceNotificationClient::OnDefaultDeviceChanged(EDataFlow flow, ERole role, LPCWSTR)
{
    if (role == eConsole && (flow == eCapture || flow == eAll))
    {
        _deviceConfigurationChanged = true;
    }

    return S_OK;
}
