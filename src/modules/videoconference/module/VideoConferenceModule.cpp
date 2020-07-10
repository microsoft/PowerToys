#include "pch.h"

#include "VideoConferenceModule.h"

#include <WinUser.h>

#include <gdiplus.h>

#include "common/common.h"

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

std::mutex VideoConferenceModule::keyboardInputMutex;

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
    const wchar_t shmemEndpoint[] = L"Global\\PowerToysWebcamMuteSwitch";

    auto hMapFile = OpenFileMappingW(
        FILE_MAP_ALL_ACCESS, // read/write access
        FALSE, // do not inherit the name
        shmemEndpoint); // name of mapping object
    if (!hMapFile)
    {
        return;
    }
    auto pBuf = (uint8_t*)MapViewOfFile(hMapFile, // handle to map object
                                        FILE_MAP_ALL_ACCESS, // read/write permission
                                        0,
                                        0,
                                        1);

    if (!pBuf)
    {
        return;
    }

    *pBuf = ~(*pBuf);
    overlay.setCameraMute(*pBuf);

    FlushViewOfFile(pBuf, 1);
}

bool VideoConferenceModule::getVirtualCameraMuteState()
{
    const wchar_t shmemEndpoint[] = L"Global\\PowerToysWebcamMuteSwitch";

    auto hMapFile = OpenFileMappingW(
        FILE_MAP_ALL_ACCESS, // read/write access
        FALSE, // do not inherit the name
        shmemEndpoint); // name of mapping object
    if (!hMapFile)
    {
        return false;
    }
    auto pBuf = (uint8_t*)MapViewOfFile(hMapFile, // handle to map object
                                        FILE_MAP_ALL_ACCESS, // read/write permission
                                        0,
                                        0,
                                        1);

    if (!pBuf)
    {
        return false;
    }

    return *pBuf;

    FlushViewOfFile(pBuf, 1);
}

LRESULT CALLBACK VideoConferenceModule::LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    std::lock_guard lock{ keyboardInputMutex };

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
                }
            }
            else if (isHotkeyPressed(kbd->vkCode, microphoneMuteHotkey))
            {
                reverseMicrophoneMute();
            }
            else if (isHotkeyPressed(kbd->vkCode, cameraMuteHotkey))
            {
                reverseVirtualCameraMuteState();
            }
        }
    }

    return CallNextHookEx(NULL, nCode, wParam, lParam);
}

VideoConferenceModule::VideoConferenceModule()
{
    init_settings();
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
            if (const auto val = values.get_string_value(L"selected_camera"))
            {
                selectedCamera = val.value();
            }

            overlay.hideOverlay(overlayPositionString, overlayMonitorString);
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
        hook_handle = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandle(NULL), NULL);

        overlay.setMicrophoneMute(getMicrophoneMuteState());
        overlay.setCameraMute(getVirtualCameraMuteState());

        overlay.hideOverlay(overlayPositionString, overlayMonitorString);

        _enabled = true;
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
