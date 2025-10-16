#include "pch.h"
#include "MicrophoneDevice.h"

#include "Logging.h"

#include <Functiondiscoverykeys_devpkey.h>

MicrophoneDevice::MicrophoneDevice(wil::com_ptr_nothrow<IMMDevice> device, wil::com_ptr_nothrow<IAudioEndpointVolume> endpoint) :
    _device{ std::move(device) },
    _endpoint{ std::move(endpoint) }
{
    if (!_device || !_endpoint)
    {
        throw std::logic_error("MicrophoneDevice was initialized with null objects");
    }
    _device->GetId(&_id);
    wil::com_ptr_nothrow<IPropertyStore> props;
    _device->OpenPropertyStore(
        STGM_READ, &props);
    if (props)
    {
        props->GetValue(PKEY_Device_FriendlyName, &_friendly_name);
    }
    else
    {
        LOG("MicrophoneDevice::MicrophoneDevice couldn't open property store");
    }
}

MicrophoneDevice::~MicrophoneDevice()
{
    if (_notifier)
    {
        _endpoint->UnregisterControlChangeNotify(_notifier.get());
    }
}

bool MicrophoneDevice::active() const noexcept
{
    DWORD state = 0;
    _device->GetState(&state);
    return state == DEVICE_STATE_ACTIVE;
}

void MicrophoneDevice::set_muted(const bool muted) noexcept
{
    _endpoint->SetMute(muted, nullptr);
}

bool MicrophoneDevice::muted() const noexcept
{
    BOOL muted = FALSE;
    _endpoint->GetMute(&muted);
    return muted;
}

std::wstring_view MicrophoneDevice::id() const noexcept
{
    return _id ? _id.get() : FALLBACK_ID;
}

std::wstring_view MicrophoneDevice::name() const noexcept
{
    return _friendly_name.pwszVal ? _friendly_name.pwszVal : FALLBACK_NAME;
}

void MicrophoneDevice::set_mute_changed_callback(mute_changed_cb_t callback) noexcept
{
    if (_notifier)
    {
        _endpoint->UnregisterControlChangeNotify(_notifier.get());
    }
    _mute_changed_callback = std::move(callback);
    _notifier = winrt::make<VolumeNotifier>(this);

    _endpoint->RegisterControlChangeNotify(_notifier.get());
}

std::unique_ptr<MicrophoneDevice> MicrophoneDevice::getDefault()
{
    auto deviceEnumerator = wil::CoCreateInstanceNoThrow<MMDeviceEnumerator, IMMDeviceEnumerator>();
    if (!deviceEnumerator)
    {
        LOG("MicrophoneDevice::getDefault MMDeviceEnumerator returned null");
        return nullptr;
    }
    wil::com_ptr_nothrow<IMMDevice> captureDevice;
    deviceEnumerator->GetDefaultAudioEndpoint(eCapture, eCommunications, &captureDevice);
    if (!captureDevice)
    {
        LOG("MicrophoneDevice::getDefault captureDevice is null");
        return nullptr;
    }
    wil::com_ptr_nothrow<IAudioEndpointVolume> microphoneEndpoint;
    captureDevice->Activate(__uuidof(IAudioEndpointVolume), CLSCTX_INPROC_SERVER, nullptr, reinterpret_cast<LPVOID*>(&microphoneEndpoint));
    if (!microphoneEndpoint)
    {
        LOG("MicrophoneDevice::getDefault captureDevice is null");
        return nullptr;
    }
    return std::make_unique<MicrophoneDevice>(std::move(captureDevice), std::move(microphoneEndpoint));
}

std::vector<std::unique_ptr<MicrophoneDevice>> MicrophoneDevice::getAllActive()
{
    std::vector<std::unique_ptr<MicrophoneDevice>> microphoneDevices;
    auto deviceEnumerator = wil::CoCreateInstanceNoThrow<MMDeviceEnumerator, IMMDeviceEnumerator>();
    if (!deviceEnumerator)
    {
        LOG("MicrophoneDevice::getAllActive MMDeviceEnumerator returned null");
        return microphoneDevices;
    }

    wil::com_ptr_nothrow<IMMDeviceCollection> captureDevices;
    deviceEnumerator->EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE, &captureDevices);
    if (!captureDevices)
    {
        LOG("MicrophoneDevice::getAllActive EnumAudioEndpoints returned null");
        return microphoneDevices;
    }
    UINT nDevices = 0;
    captureDevices->GetCount(&nDevices);
    microphoneDevices.reserve(nDevices);
    for (UINT i = 0; i < nDevices; ++i)
    {
        wil::com_ptr_nothrow<IMMDevice> device;
        captureDevices->Item(i, &device);
        if (!device)
        {
            continue;
        }
        wil::com_ptr_nothrow<IAudioEndpointVolume> microphoneEndpoint;
        device->Activate(__uuidof(IAudioEndpointVolume), CLSCTX_INPROC_SERVER, nullptr, reinterpret_cast<LPVOID*>(&microphoneEndpoint));
        if (!microphoneEndpoint)
        {
            continue;
        }
        microphoneDevices.push_back(std::make_unique<MicrophoneDevice>(std::move(device), std::move(microphoneEndpoint)));
    }
    return microphoneDevices;
}

MicrophoneDevice::VolumeNotifier::VolumeNotifier(MicrophoneDevice* subscribedDevice) :
    _subscribedDevice{ subscribedDevice }
{
}

HRESULT __stdcall MicrophoneDevice::VolumeNotifier::OnNotify(PAUDIO_VOLUME_NOTIFICATION_DATA data)
{
    if (_subscribedDevice && _subscribedDevice->_mute_changed_callback)
        _subscribedDevice->_mute_changed_callback(data->bMuted);

    return S_OK;
}
