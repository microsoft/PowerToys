#include "pch.h"

#include "VideoConferenceModule.h"

#include <WinUser.h>

#include <gdiplus.h>

#include <common/common.h>
#include <common/debug_control.h>

#include <CameraStateUpdateChannels.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

VideoConferenceModule* instance = nullptr;

Overlay VideoConferenceModule::overlay;

CVolumeNotification* VideoConferenceModule::volumeNotification;

PowerToysSettings::HotkeyObject VideoConferenceModule::cameraAndMicrophoneMuteHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, 78);
PowerToysSettings::HotkeyObject VideoConferenceModule::microphoneMuteHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, 65);
PowerToysSettings::HotkeyObject VideoConferenceModule::cameraMuteHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, 79);

std::wstring VideoConferenceModule::overlayPositionString;
std::wstring VideoConferenceModule::overlayMonitorString;

std::wstring VideoConferenceModule::selectedCamera;
std::wstring VideoConferenceModule::imageOverlayPath;

std::mutex VideoConferenceModule::keyboardInputMutex;

HHOOK VideoConferenceModule::hook_handle;

IAudioEndpointVolume* endpointVolume = NULL;

bool VideoConferenceModule::isKeyPressed(unsigned int keyCode)
{
    return (GetKeyState(keyCode) & 0x8000);
}

bool VideoConferenceModule::isHotkeyPressed(DWORD code, PowerToysSettings::HotkeyObject& hotkey)
{
    return code == hotkey.get_code() &&
           isKeyPressed(VK_SHIFT) == hotkey.shift_pressed() &&
           isKeyPressed(VK_CONTROL) == hotkey.ctrl_pressed() &&
           isKeyPressed(VK_LWIN) == hotkey.win_pressed() &&
           (isKeyPressed(VK_LMENU)) == hotkey.alt_pressed();
}

void VideoConferenceModule::reverseMicrophoneMute()
{
    IMMDeviceEnumerator* deviceEnumerator = NULL;
    if (CoCreateInstance(__uuidof(MMDeviceEnumerator), NULL, CLSCTX_INPROC_SERVER, __uuidof(IMMDeviceEnumerator), (LPVOID*)&deviceEnumerator) == S_OK)
    {
        IMMDevice* defaultDevice = NULL;
        if (deviceEnumerator->GetDefaultAudioEndpoint(eCapture, eCommunications, &defaultDevice) == S_OK)
        {
            deviceEnumerator->Release();
            deviceEnumerator = NULL;

            IAudioEndpointVolume* microphoneEndpoint = NULL;
            if (defaultDevice->Activate(__uuidof(IAudioEndpointVolume), CLSCTX_INPROC_SERVER, NULL, (LPVOID*)&microphoneEndpoint) == S_OK)
            {
                volumeNotification = new CVolumeNotification();
                microphoneEndpoint->RegisterControlChangeNotify(volumeNotification);

                BOOL currentMute;
                if (microphoneEndpoint->GetMute(&currentMute) == S_OK)
                {
                    if (microphoneEndpoint->SetMute(!currentMute, NULL) == S_OK)
                    {
                        //overlay.setMicrophoneMute(!currentMute);
                    }
                }

                defaultDevice->Release();
                defaultDevice = NULL;

                microphoneEndpoint->Release();
            }
        }
    }
}

bool VideoConferenceModule::getMicrophoneMuteState()
{
    HRESULT hr;

    BOOL currentMute = false;

    IMMDeviceEnumerator* deviceEnumerator = NULL;

    if (CoCreateInstance(__uuidof(MMDeviceEnumerator), NULL, CLSCTX_INPROC_SERVER, __uuidof(IMMDeviceEnumerator), (LPVOID*)&deviceEnumerator) == S_OK)
    {
        IMMDevice* defaultDevice = NULL;

        if (deviceEnumerator->GetDefaultAudioEndpoint(eCapture, eCommunications, &defaultDevice) == S_OK)
        {
            deviceEnumerator->Release();
            deviceEnumerator = NULL;

            IAudioEndpointVolume* endpointVolume = NULL;
            if (defaultDevice->Activate(__uuidof(IAudioEndpointVolume), CLSCTX_INPROC_SERVER, NULL, (LPVOID*)&endpointVolume) == S_OK)
            {
                volumeNotification = new CVolumeNotification();
                hr = endpointVolume->RegisterControlChangeNotify(volumeNotification);
                defaultDevice->Release();
                defaultDevice = NULL;

                endpointVolume->GetMute(&currentMute);

                volumeNotification->Release();
            }
        }
    }
    return currentMute;
}

void VideoConferenceModule::reverseVirtualCameraMuteState()
{
    if (!instance->_settingsUpdateChannel.has_value())
    {
        return;
    }
    instance->_settingsUpdateChannel->access([](auto settingsMemory) {
        auto settings = reinterpret_cast<CameraSettingsUpdateChannel*>(settingsMemory.data());
        settings->useOverlayImage = !settings->useOverlayImage;
    });
}

bool VideoConferenceModule::getVirtualCameraMuteState()
{
    bool disabled = false;
    if (!instance->_settingsUpdateChannel.has_value())
    {
        return disabled;
    }
    instance->_settingsUpdateChannel->access([&disabled](auto settingsMemory) {
        auto settings = reinterpret_cast<CameraSettingsUpdateChannel*>(settingsMemory.data());
        disabled = settings->useOverlayImage;
    });
    return disabled;
}

LRESULT CALLBACK VideoConferenceModule::LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
{

    if (nCode == HC_ACTION)
    {
        switch (wParam)
        {
        case WM_KEYDOWN:
            KBDLLHOOKSTRUCT* kbd = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);

            if (isHotkeyPressed(kbd->vkCode, cameraAndMicrophoneMuteHotkey))
            {
                reverseMicrophoneMute();
                if (overlay.getCameraMute() != overlay.getMicrophoneMute())
                {
                    reverseVirtualCameraMuteState();
                    overlay.setCameraMute(getVirtualCameraMuteState());
                }
            }
            else if (isHotkeyPressed(kbd->vkCode, microphoneMuteHotkey))
            {
                reverseMicrophoneMute();
            }
            else if (isHotkeyPressed(kbd->vkCode, cameraMuteHotkey))
            {
                reverseVirtualCameraMuteState();
                overlay.setCameraMute(getVirtualCameraMuteState());
            }
        }
    }

    return CallNextHookEx(hook_handle, nCode, wParam, lParam);
}

VideoConferenceModule::VideoConferenceModule()
{
    init_settings();
    _settingsUpdateChannel =
        SerializedSharedMemory::create(CameraSettingsUpdateChannel::endpoint(), sizeof(CameraSettingsUpdateChannel), false);
    if (_settingsUpdateChannel)
    {
        _settingsUpdateChannel->access([](auto memory) {
            auto updatesChannel = new (memory.data()) CameraSettingsUpdateChannel{};
        });
    }
    sendSourceCameraNameUpdate();
    sendOverlayImageUpdate();
}

inline VideoConferenceModule::~VideoConferenceModule()
{
    overlay.hideOverlay();
}

const wchar_t* VideoConferenceModule::get_name()
{
    return L"Video Conference";
}

const wchar_t** VideoConferenceModule::get_events()
{
    return nullptr;
}

bool VideoConferenceModule::get_config(wchar_t* buffer, int* buffer_size)
{
    return true;
}

void VideoConferenceModule::set_config(const wchar_t* config)
{
    try
    {
        PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(config);
        values.save_to_settings_file();
        //Trace::SettingsChanged(pressTime.value, overlayOpacity.value, theme.value);

        if (_enabled)
        {
            if (const auto val = values.get_json(L"mute_camera_and_microphone_hotkey"))
            {
                cameraAndMicrophoneMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
            }
            if (const auto val = values.get_json(L"mute_microphone_hotkey"))
            {
                microphoneMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
            }
            if (const auto val = values.get_json(L"mute_camera_hotkey"))
            {
                cameraMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
            }
            if (const auto val = values.get_string_value(L"overlay_position"))
            {
                overlayPositionString = val.value();
            }
            if (const auto val = values.get_string_value(L"overlay_monitor"))
            {
                overlayMonitorString = val.value();
            }
            if (const auto val = values.get_string_value(L"selected_camera"); val && val != selectedCamera)
            {
                selectedCamera = val.value();
                sendSourceCameraNameUpdate();
            }
            if (const auto val = values.get_string_value(L"camera_overlay_image_path"); val && val != imageOverlayPath)
            {
                imageOverlayPath = val.value();
                sendOverlayImageUpdate();
            }
            overlay.showOverlay(overlayPositionString, overlayMonitorString);
        }
    }
    catch (...)
    {
        // Improper JSON. TODO: handle the error.
    }
}

void VideoConferenceModule::init_settings()
{
    try
    {
        PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::load_from_settings_file(L"Video Conference");

        if (const auto val = settings.get_json(L"mute_camera_and_microphone_hotkey"))
        {
            cameraAndMicrophoneMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
        }
        if (const auto val = settings.get_json(L"mute_microphone_hotkey"))
        {
            microphoneMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
        }
        if (const auto val = settings.get_json(L"mute_camera_hotkey"))
        {
            cameraMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
        }
        if (const auto val = settings.get_string_value(L"overlay_position"))
        {
            overlayPositionString = val.value();
        }
        if (const auto val = settings.get_string_value(L"overlay_monitor"))
        {
            overlayMonitorString = val.value();
        }
        if (const auto val = settings.get_string_value(L"selected_camera"))
        {
            selectedCamera = val.value();
        }
        if (const auto val = settings.get_string_value(L"camera_overlay_image_path"))
        {
            imageOverlayPath = val.value();
        }
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Just let default values stay as they are.
    }
}

void VideoConferenceModule::enable()
{
    if (!_enabled)
    {
        overlay.setMicrophoneMute(getMicrophoneMuteState());
        overlay.setCameraMute(getVirtualCameraMuteState());

        overlay.showOverlay(overlayPositionString, overlayMonitorString);

        _enabled = true;

#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
        if (IsDebuggerPresent())
        {
            return;
        }
#endif
        hook_handle = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandle(NULL), NULL);
    }
}

void VideoConferenceModule::disable()
{
    if (_enabled)
    {
        if (hook_handle)
        {
            bool success = UnhookWindowsHookEx(hook_handle);
            if (success)
            {
                hook_handle = nullptr;
            }
        }

        overlay.hideOverlay();

        _enabled = false;
    }
}

bool VideoConferenceModule::is_enabled()
{
    return _enabled;
}

intptr_t VideoConferenceModule::signal_event(const wchar_t* name, intptr_t data)
{
    return 0;
}

void VideoConferenceModule::destroy()
{
    delete this;
    instance = nullptr;
}

void VideoConferenceModule::sendSourceCameraNameUpdate()
{
    if (!_settingsUpdateChannel.has_value() || selectedCamera.empty())
    {
        return;
    }
    _settingsUpdateChannel->access([](auto memory) {
        auto updatesChannel = new (memory.data()) CameraSettingsUpdateChannel{};
        updatesChannel->sourceCameraName.emplace();
        std::copy(begin(selectedCamera), end(selectedCamera), begin(*updatesChannel->sourceCameraName));
    });
}

void VideoConferenceModule::sendOverlayImageUpdate()
{
    if (!_settingsUpdateChannel.has_value())
    {
        return;
    }
    _imageOverlayChannel.reset();

    _imageOverlayChannel = SerializedSharedMemory::create_readonly(CameraOverlayImageChannel::endpoint(), imageOverlayPath);
    if (!_imageOverlayChannel)
    {
        return;
    }
    const size_t imageSize = _imageOverlayChannel->size();
    _settingsUpdateChannel->access([imageSize](auto memory) {
        auto updatesChannel = reinterpret_cast<CameraSettingsUpdateChannel*>(memory.data());
        updatesChannel->overlayImageSize.emplace(imageSize);
        updatesChannel->newOverlayImagePosted = true;
    });
}
