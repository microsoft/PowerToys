#include "pch.h"

#include "VideoConferenceModule.h"

#include <WinUser.h>

#include <gdiplus.h>

#include <common/common.h>
#include <common/debug_control.h>
#include <common/settings_helpers.h>

#include <CameraStateUpdateChannels.h>

#include "logging.h"
#include "trace.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

VideoConferenceModule* instance = nullptr;

VideoConferenceSettings VideoConferenceModule::settings;
Toolbar VideoConferenceModule::toolbar;

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
    for (auto& controlledMic : instance->_controlledMicrophones)
    {
        controlledMic.toggle_muted();
    }
}

bool VideoConferenceModule::getMicrophoneMuteState()
{
    return instance->_microphoneTrackedInUI ? instance->_microphoneTrackedInUI->muted() : false;
}

void VideoConferenceModule::reverseVirtualCameraMuteState()
{
    bool camera_muted = false;
    if (!instance->_settingsUpdateChannel.has_value())
    {
        return;
    }

    instance->_settingsUpdateChannel->access([&camera_muted](auto settingsMemory) {
        auto settings = reinterpret_cast<CameraSettingsUpdateChannel*>(settingsMemory._data);
        settings->useOverlayImage = !settings->useOverlayImage;
        camera_muted = settings->useOverlayImage;
    });

    if (camera_muted)
    {
        Trace::CameraMuted();
    }
}

bool VideoConferenceModule::getVirtualCameraMuteState()
{
    bool disabled = false;
    if (!instance->_settingsUpdateChannel.has_value())
    {
        return disabled;
    }
    instance->_settingsUpdateChannel->access([&disabled](auto settingsMemory) {
        auto settings = reinterpret_cast<CameraSettingsUpdateChannel*>(settingsMemory._data);
        disabled = settings->useOverlayImage;
    });
    return disabled;
}

bool VideoConferenceModule::getVirtualCameraInUse()
{
    if (!instance->_settingsUpdateChannel.has_value())
    {
        return false;
    }
    bool inUse = false;
    instance->_settingsUpdateChannel->access([&inUse](auto settingsMemory) {
        auto settings = reinterpret_cast<CameraSettingsUpdateChannel*>(settingsMemory._data);
        inUse = settings->cameraInUse;
    });
    return inUse;
}

LRESULT CALLBACK VideoConferenceModule::LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        switch (wParam)
        {
        case WM_KEYDOWN:
            KBDLLHOOKSTRUCT* kbd = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);

            if (isHotkeyPressed(kbd->vkCode, settings.cameraAndMicrophoneMuteHotkey))
            {
                reverseMicrophoneMute();
                if (toolbar.getCameraMute() != toolbar.getMicrophoneMute())
                {
                    reverseVirtualCameraMuteState();
                    toolbar.setCameraMute(getVirtualCameraMuteState());
                }
            }
            else if (isHotkeyPressed(kbd->vkCode, settings.microphoneMuteHotkey))
            {
                reverseMicrophoneMute();
            }
            else if (isHotkeyPressed(kbd->vkCode, settings.cameraMuteHotkey))
            {
                reverseVirtualCameraMuteState();
                toolbar.setCameraMute(getVirtualCameraMuteState());
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
            auto updatesChannel = new (memory._data) CameraSettingsUpdateChannel{};
        });
    }
    sendSourceCameraNameUpdate();
    sendOverlayImageUpdate();
    toolbar.show(settings.toolbarPositionString, settings.toolbarMonitorString);
}

inline VideoConferenceModule::~VideoConferenceModule()
{
    if (toolbar.getCameraMute())
    {
        reverseVirtualCameraMuteState();
        toolbar.setCameraMute(getVirtualCameraMuteState());
    }

    if (toolbar.getMicrophoneMute())
    {
        reverseMicrophoneMute();
    }

    toolbar.hide();
}

const wchar_t* VideoConferenceModule::get_name()
{
    return L"Video Conference";
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
                settings.cameraAndMicrophoneMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
            }
            if (const auto val = values.get_json(L"mute_microphone_hotkey"))
            {
                settings.microphoneMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
            }
            if (const auto val = values.get_json(L"mute_camera_hotkey"))
            {
                settings.cameraMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
            }
            if (const auto val = values.get_string_value(L"toolbar_position"))
            {
                settings.toolbarPositionString = val.value();
            }
            if (const auto val = values.get_string_value(L"toolbar_monitor"))
            {
                settings.toolbarMonitorString = val.value();
            }
            if (const auto val = values.get_string_value(L"selected_camera"); val && val != settings.selectedCamera)
            {
                settings.selectedCamera = val.value();
                sendSourceCameraNameUpdate();
            }
            if (const auto val = values.get_string_value(L"camera_overlay_image_path"); val && val != settings.imageOverlayPath)
            {
                settings.imageOverlayPath = val.value();
                sendOverlayImageUpdate();
            }
            if (const auto val = values.get_string_value(L"theme"))
            {
                Toolbar::setTheme(val.value());
            }
            if (const auto val = values.get_bool_value(L"hide_toolbar_when_unmuted"))
            {
                Toolbar::setHideToolbarWhenUnmuted(val.value());
            }
            if (const auto val = values.get_string_value(L"selected_mic"))
            {
                settings.selectedMicrophone = *val;
                updateControlledMicrophones(settings.selectedMicrophone);
            }

            toolbar.show(settings.toolbarPositionString, settings.toolbarMonitorString);
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
        PowerToysSettings::PowerToyValues powerToysSettings = PowerToysSettings::PowerToyValues::load_from_settings_file(L"Video Conference");

        if (const auto val = powerToysSettings.get_json(L"mute_camera_and_microphone_hotkey"))
        {
            settings.cameraAndMicrophoneMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
        }
        if (const auto val = powerToysSettings.get_json(L"mute_microphone_hotkey"))
        {
            settings.microphoneMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
        }
        if (const auto val = powerToysSettings.get_json(L"mute_camera_hotkey"))
        {
            settings.cameraMuteHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
        }
        if (const auto val = powerToysSettings.get_string_value(L"toolbar_position"))
        {
            settings.toolbarPositionString = val.value();
        }
        if (const auto val = powerToysSettings.get_string_value(L"toolbar_monitor"))
        {
            settings.toolbarMonitorString = val.value();
        }
        if (const auto val = powerToysSettings.get_string_value(L"selected_camera"))
        {
            settings.selectedCamera = val.value();
        }
        if (const auto val = powerToysSettings.get_string_value(L"camera_overlay_image_path"))
        {
            settings.imageOverlayPath = val.value();
        }
        if (const auto val = powerToysSettings.get_bool_value(L"hide_toolbar_when_unmuted"))
        {
            Toolbar::setHideToolbarWhenUnmuted(val.value());
        }
        if (const auto val = powerToysSettings.get_string_value(L"selected_mic"); *val != settings.selectedMicrophone)
        {
            settings.selectedMicrophone = *val;
            updateControlledMicrophones(settings.selectedMicrophone);
        }
    }
    catch (std::exception&)
    {
        // Error while loading from the settings file. Just let default values stay as they are.
    }

    try
    {
        auto loaded = PTSettingsHelper::load_general_settings();
        std::wstring settings_theme{ static_cast<std::wstring_view>(loaded.GetNamedString(L"theme", L"system")) };
        if (settings_theme != L"dark" && settings_theme != L"light")
        {
            settings_theme = L"system";
        }
        Toolbar::setTheme(settings_theme);
    }
    catch (...)
    {
    }
}

void VideoConferenceModule::updateControlledMicrophones(const std::wstring_view new_mic)
{
    for (auto& controlledMic : _controlledMicrophones)
    {
        controlledMic.set_muted(false);
    }
    _controlledMicrophones.clear();
    _microphoneTrackedInUI = nullptr;
    auto allMics = MicrophoneDevice::getAllActive();
    if (new_mic == L"[All]")
    {
        _controlledMicrophones = std::move(allMics);
        if (auto defaultMic = MicrophoneDevice::getDefault())
        {
            for (auto& controlledMic : _controlledMicrophones)
            {
                if (controlledMic.id() == defaultMic->id())
                {
                    _microphoneTrackedInUI = &controlledMic;
                    break;
                }
            }
        }
    }
    else
    {
        for (auto& controlledMic : allMics)
        {
            if (controlledMic.name() == new_mic)
            {
                _controlledMicrophones.emplace_back(std::move(controlledMic));
                _microphoneTrackedInUI = &_controlledMicrophones[0];
                break;
            }
        }
    }
    if (_microphoneTrackedInUI)
    {
        _microphoneTrackedInUI->set_mute_changed_callback([](const bool muted) {
            Toolbar::setMicrophoneMute(muted);
        });
        Toolbar::setMicrophoneMute(_microphoneTrackedInUI->muted());
    }
}

void VideoConferenceModule::enable()
{
    if (!_enabled)
    {
        toolbar.setMicrophoneMute(getMicrophoneMuteState());
        toolbar.setCameraMute(getVirtualCameraMuteState());

        toolbar.show(settings.toolbarPositionString, settings.toolbarMonitorString);

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

        if (toolbar.getCameraMute()) // TODO: unmute
        {
            reverseVirtualCameraMuteState();
            toolbar.setCameraMute(getVirtualCameraMuteState());
        }

        if (toolbar.getMicrophoneMute())
        {
            reverseMicrophoneMute();
        }

        toolbar.hide();

        _enabled = false;
    }
}

bool VideoConferenceModule::is_enabled()
{
    return _enabled;
}

void VideoConferenceModule::destroy()
{
    delete this;
    instance = nullptr;
}

void VideoConferenceModule::sendSourceCameraNameUpdate()
{
    if (!_settingsUpdateChannel.has_value() || settings.selectedCamera.empty())
    {
        return;
    }
    _settingsUpdateChannel->access([](auto memory) {
        auto updatesChannel = reinterpret_cast<CameraSettingsUpdateChannel*>(memory._data);
        updatesChannel->sourceCameraName.emplace();
        std::copy(begin(settings.selectedCamera), end(settings.selectedCamera), begin(*updatesChannel->sourceCameraName));
    });
}

void VideoConferenceModule::sendOverlayImageUpdate()
{
    if (!_settingsUpdateChannel.has_value())
    {
        return;
    }
    _imageOverlayChannel.reset();

    TCHAR* powertoysDirectory = new TCHAR[255];

    DWORD length = GetModuleFileName(NULL, powertoysDirectory, 255);
    PathRemoveFileSpec(powertoysDirectory);

    std::wstring blankImagePath(powertoysDirectory);
    blankImagePath += L"\\modules\\VideoConference\\black.bmp";

    _imageOverlayChannel = SerializedSharedMemory::create_readonly(CameraOverlayImageChannel::endpoint(),
                                                                   settings.imageOverlayPath != L"" ? settings.imageOverlayPath : blankImagePath);

    const size_t imageSize = _imageOverlayChannel->size();
    _settingsUpdateChannel->access([imageSize](auto memory) {
        auto updatesChannel = reinterpret_cast<CameraSettingsUpdateChannel*>(memory._data);
        updatesChannel->overlayImageSize.emplace(imageSize);
        updatesChannel->newOverlayImagePosted = true;
    });
}
