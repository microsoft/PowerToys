#include "pch.h"

#include "VideoConferenceModule.h"

#include <WinUser.h>

#include <gdiplus.h>
#include <shellapi.h>

#include <filesystem>
#include <unordered_set>

#include <common/debug_control.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/utils/elevation.h>
#include <common/utils/process_path.h>

#include <CameraStateUpdateChannels.h>

#include "logging.h"
#include "trace.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

VideoConferenceModule* instance = nullptr;

VideoConferenceSettings VideoConferenceModule::settings;
Toolbar VideoConferenceModule::toolbar;
bool VideoConferenceModule::pushToTalkPressed = false;

HHOOK VideoConferenceModule::hook_handle;

IAudioEndpointVolume* endpointVolume = NULL;

bool VideoConferenceModule::isKeyPressed(unsigned int keyCode)
{
    return (GetKeyState(keyCode) & 0x8000);
}

namespace fs = std::filesystem;

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
    // All controlled mic should same state with _microphoneTrackedInUI
    // Avoid manually change in Control Panel make controlled mic has different state
    bool muted = !getMicrophoneMuteState();
    for (auto& controlledMic : instance->_controlledMicrophones)
    {
        controlledMic->set_muted(muted);
    }
    if (muted)
    {
        Trace::MicrophoneMuted();
    }
    instance->_mic_muted_state_during_disconnect = !instance->_mic_muted_state_during_disconnect;

    toolbar.setMicrophoneMute(muted);
}

bool VideoConferenceModule::getMicrophoneMuteState()
{
    return instance->_microphoneTrackedInUI ? instance->_microphoneTrackedInUI->muted() : instance->_mic_muted_state_during_disconnect;
}

void VideoConferenceModule::reverseVirtualCameraMuteState()
{
    bool muted = false;
    if (!instance->_settingsUpdateChannel.has_value())
    {
        return;
    }

    instance->_settingsUpdateChannel->access([&muted](auto settingsMemory) {
        auto settings = reinterpret_cast<CameraSettingsUpdateChannel*>(settingsMemory._data);
        settings->useOverlayImage = !settings->useOverlayImage;
        muted = settings->useOverlayImage;
    });

    if (muted)
    {
        Trace::CameraMuted();
    }
    toolbar.setCameraMute(muted);
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
        KBDLLHOOKSTRUCT* kbd = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
        switch (wParam)
        {
        case WM_KEYDOWN:

            if (isHotkeyPressed(kbd->vkCode, settings.cameraAndMicrophoneMuteHotkey))
            {
                const bool cameraInUse = getVirtualCameraInUse();
                const bool microphoneIsMuted = getMicrophoneMuteState();
                const bool cameraIsMuted = cameraInUse && getVirtualCameraMuteState();
                if (cameraInUse)
                {
                    // we're likely on a video call, so we must mute the unmuted cam/mic or reverse the mute state
                    // of everything, if cam and mic mute states are the same
                    if (microphoneIsMuted == cameraIsMuted)
                    {
                        reverseMicrophoneMute();
                        reverseVirtualCameraMuteState();
                    }
                    else if (cameraIsMuted)
                    {
                        reverseMicrophoneMute();
                    }
                    else if (microphoneIsMuted)
                    {
                        reverseVirtualCameraMuteState();
                    }
                }
                else
                {
                    // if the camera is not in use, we just mute/unmute the mic
                    reverseMicrophoneMute();
                }
                return 1;
            }
            else if (isHotkeyPressed(kbd->vkCode, settings.microphoneMuteHotkey))
            {
                reverseMicrophoneMute();
                return 1;
            }
            else if (isHotkeyPressed(kbd->vkCode, settings.microphonePushToTalkHotkey))
            {
                if (!pushToTalkPressed)
                {
                    if (settings.pushToReverseEnabled || getMicrophoneMuteState())
                    {
                        reverseMicrophoneMute();
                    }
                    pushToTalkPressed = true;
                }
                return 1;
            }
            else if (isHotkeyPressed(kbd->vkCode, settings.cameraMuteHotkey))
            {
                reverseVirtualCameraMuteState();
                return 1;
            }
            break;
        case WM_KEYUP:
            if (pushToTalkPressed && (kbd->vkCode == settings.microphonePushToTalkHotkey.get_code()))
            {
                reverseMicrophoneMute();
                pushToTalkPressed = false;
                return 1;
            }
        }
    }

    return CallNextHookEx(hook_handle, nCode, wParam, lParam);
}

void VideoConferenceModule::onGeneralSettingsChanged()
{
    auto settings = PTSettingsHelper::load_general_settings();
    bool enabled = false;
    try
    {
        if (json::has(settings, L"enabled"))
        {
            for (const auto& mod : settings.GetNamedObject(L"enabled"))
            {
                const auto value = mod.Value();
                if (value.ValueType() != json::JsonValueType::Boolean)
                {
                    continue;
                }
                if (mod.Key() == get_key())
                {
                    enabled = value.GetBoolean();
                    break;
                }
            }
        }
    }
    catch (...)
    {
        LOG("Couldn't get enabled state");
    }
    if (enabled)
    {
        enable();
    }
    else
    {
        disable();
    }
}

void VideoConferenceModule::onModuleSettingsChanged()
{
    try
    {
        PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::load_from_settings_file(get_key());
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
            if (const auto val = values.get_json(L"push_to_talk_microphone_hotkey"))
            {
                settings.microphonePushToTalkHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
            }
            if (const auto val = values.get_bool_value(L"push_to_reverse_enabled"))
            {
                settings.pushToReverseEnabled = *val;
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
            if (const auto val = values.get_string_value(L"toolbar_hide"))
            {
                toolbar.setToolbarHide(val.value());
            }
            if (const auto val = values.get_string_value(L"startup_action"))
            {
                settings.startupAction = val.value();
            }

            const auto selectedMic = values.get_string_value(L"selected_mic");
            if (selectedMic && selectedMic != settings.selectedMicrophone)
            {
                settings.selectedMicrophone = *selectedMic;
                updateControlledMicrophones(settings.selectedMicrophone);
            }

            toolbar.show(settings.toolbarPositionString, settings.toolbarMonitorString);
        }
    }
    catch (...)
    {
        LOG("onModuleSettingsChanged encountered an exception");
    }
}

void VideoConferenceModule::onMicrophoneConfigurationChanged()
{
    if (!_controllingAllMics)
    {
        // Don't care if we don't control all the mics
        return;
    }

    const bool mutedStateForNewMics = getMicrophoneMuteState();
    std::unordered_set<std::wstring_view> currentlyTrackedMicsIds;
    for (const auto& controlledMic : _controlledMicrophones)
    {
        currentlyTrackedMicsIds.emplace(controlledMic->id());
    }

    auto allMics = MicrophoneDevice::getAllActive();
    for (auto& newMic : allMics)
    {
        if (currentlyTrackedMicsIds.contains(newMic->id()))
        {
            continue;
        }

        if (mutedStateForNewMics)
        {
            newMic->set_muted(true);
        }

        _controlledMicrophones.emplace_back(std::move(newMic));
    }
    // Restore invalidated pointer
    _microphoneTrackedInUI = controlledDefaultMic();
    if (_microphoneTrackedInUI)
    {
        _microphoneTrackedInUI->set_mute_changed_callback([](const bool muted) {
            toolbar.setMicrophoneMute(muted);
        });
    }
}

VideoConferenceModule::VideoConferenceModule()
{
    init_settings();
    _settingsUpdateChannel =
        SerializedSharedMemory::create(CameraSettingsUpdateChannel::endpoint(), sizeof(CameraSettingsUpdateChannel), false);
    if (_settingsUpdateChannel)
    {
        _settingsUpdateChannel->access([](auto memory) {

// Suppress warning 26403 -  Reset or explicitly delete an owner<T> pointer 'variable' (r.3)
// the video conference class should be only instantiated once and it is using placement new
// the access to the data can be done through memory._data
#pragma warning(push)
#pragma warning(disable : 26403)
            auto updatesChannel = new (memory._data) CameraSettingsUpdateChannel{};
#pragma warning(pop)
        });
    }
    sendSourceCameraNameUpdate();
    sendOverlayImageUpdate();
}

inline VideoConferenceModule::~VideoConferenceModule()
{
    toolbar.hide();
}

const wchar_t* VideoConferenceModule::get_name()
{
    return L"Video Conference";
}

const wchar_t* VideoConferenceModule::get_key()
{
    return L"Video Conference";
}

// Return the configured status for the gpo policy for the module
powertoys_gpo::gpo_rule_configured_t VideoConferenceModule::gpo_policy_enabled_configuration()
{
    return powertoys_gpo::getConfiguredVideoConferenceMuteEnabledValue();
}

bool VideoConferenceModule::get_config(wchar_t* buffer, int* buffer_size)
{
    return true;
}

void VideoConferenceModule::set_config(const wchar_t* config)
{
    try
    {
        PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
        values.save_to_settings_file();
    }
    catch (...)
    {
        LOG("VideoConferenceModule::set_config: exception during saving new settings values");
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
        if (const auto val = powerToysSettings.get_json(L"push_to_talk_microphone_hotkey"))
        {
            settings.microphonePushToTalkHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
        }
        if (const auto val = powerToysSettings.get_bool_value(L"push_to_reverse_enabled"))
        {
            settings.pushToReverseEnabled = *val;
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
        if (const auto val = powerToysSettings.get_string_value(L"toolbar_hide"))
        {
            toolbar.setToolbarHide(val.value());
        }
        if (const auto val = powerToysSettings.get_string_value(L"startup_action"))
        {
            settings.startupAction = val.value();
        }
        if (const auto val = powerToysSettings.get_string_value(L"selected_mic"); val && *val != settings.selectedMicrophone)
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
        toolbar.setTheme(settings_theme);
    }
    catch (...)
    {
    }
}

void VideoConferenceModule::updateControlledMicrophones(const std::wstring_view new_mic)
{
    for (auto& controlledMic : _controlledMicrophones)
    {
        controlledMic->set_muted(false);
    }
    _controlledMicrophones.clear();
    _microphoneTrackedInUI = nullptr;
    auto allMics = MicrophoneDevice::getAllActive();
    if (new_mic == L"[All]")
    {
        _controllingAllMics = true;
        _controlledMicrophones = std::move(allMics);
        _microphoneTrackedInUI = controlledDefaultMic();
    }
    else
    {
        _controllingAllMics = false;
        for (auto& controlledMic : allMics)
        {
            if (controlledMic->name() == new_mic)
            {
                _controlledMicrophones.emplace_back(std::move(controlledMic));
                _microphoneTrackedInUI = _controlledMicrophones[0].get();
                break;
            }
        }
    }

    if (_microphoneTrackedInUI)
    {
        _microphoneTrackedInUI->set_mute_changed_callback([](const bool muted) {
            toolbar.setMicrophoneMute(muted);
        });
        toolbar.setMicrophoneMute(_microphoneTrackedInUI->muted());
    }

    if (settings.startupAction == L"Unmute")
    {
        for (auto& controlledMic : _controlledMicrophones)
        {
            controlledMic->set_muted(false);
        }
    }
    else if (settings.startupAction == L"Mute")
    {
        for (auto& controlledMic : _controlledMicrophones)
        {
            controlledMic->set_muted(true);
        }
    }
}

MicrophoneDevice* VideoConferenceModule::controlledDefaultMic()
{
    if (auto defaultMic = MicrophoneDevice::getDefault())
    {
        for (auto& controlledMic : _controlledMicrophones)
        {
            if (controlledMic->id() == defaultMic->id())
            {
                return controlledMic.get();
            }
        }
    }

    return nullptr;
}

void toggleProxyCamRegistration(const bool enable)
{
    if (!is_process_elevated())
    {
        return;
    }

    auto vcmRoot = fs::path{ get_module_folderpath() };
#if defined(_M_ARM64)
    std::array<fs::path, 2> proxyFilters = { vcmRoot / "PowerToys.VideoConferenceProxyFilter_ARM64.dll", vcmRoot / "PowerToys.VideoConferenceProxyFilter_x86.dll" };
#else
    std::array<fs::path, 2> proxyFilters = { vcmRoot / "PowerToys.VideoConferenceProxyFilter_x64.dll", vcmRoot / "PowerToys.VideoConferenceProxyFilter_x86.dll" };
#endif
    for (const auto filter : proxyFilters)
    {
        std::wstring params{ L"/s " };
        if (!enable)
        {
            params += L"/u ";
        }
        params += '"';
        params += filter;
        params += '"';
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC };
        sei.lpFile = L"regsvr32";
        sei.lpParameters = params.c_str();
        sei.nShow = SW_SHOWNORMAL;
        ShellExecuteExW(&sei);
    }
}

void VideoConferenceModule::enable()
{
    if (!_enabled)
    {
        _generalSettingsWatcher = std::make_unique<FileWatcher>(
            PTSettingsHelper::get_powertoys_general_save_file_location(), [this] {
                toolbar.scheduleGeneralSettingsUpdate();
            });
        _moduleSettingsWatcher = std::make_unique<FileWatcher>(
            PTSettingsHelper::get_module_save_file_location(get_key()), [this] {
                toolbar.scheduleModuleSettingsUpdate();
            });

        toggleProxyCamRegistration(true);
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
    Trace::EnableVideoConference(true);
}

void VideoConferenceModule::unmuteAll()
{
    if (getVirtualCameraMuteState())
    {
        reverseVirtualCameraMuteState();
    }

    if (getMicrophoneMuteState())
    {
        reverseMicrophoneMute();
    }
}

void VideoConferenceModule::muteAll()
{
    if (!getVirtualCameraMuteState())
    {
        reverseVirtualCameraMuteState();
    }

    if (!getMicrophoneMuteState())
    {
        reverseMicrophoneMute();
    }
}

void VideoConferenceModule::disable()
{
    if (_enabled)
    {
        _generalSettingsWatcher.reset();
        _moduleSettingsWatcher.reset();
        toggleProxyCamRegistration(false);
        if (hook_handle)
        {
            bool success = UnhookWindowsHookEx(hook_handle);
            if (success)
            {
                hook_handle = nullptr;
            }
        }

        if (getVirtualCameraMuteState())
        {
            reverseVirtualCameraMuteState();
        }

        toolbar.hide();

        _enabled = false;
    }
    Trace::EnableVideoConference(false);
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

bool VideoConferenceModule::is_enabled_by_default() const
{
    return false;
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
        if (settings.startupAction == L"Unmute")
        {
            updatesChannel->useOverlayImage = false;
        }
        else if (settings.startupAction == L"Mute")
        {
            updatesChannel->useOverlayImage = true;
        }
    });
}

void VideoConferenceModule::sendOverlayImageUpdate()
{
    if (!_settingsUpdateChannel.has_value())
    {
        return;
    }
    _imageOverlayChannel.reset();

    wchar_t powertoysDirectory[MAX_PATH + 1];

    DWORD length = GetModuleFileNameW(nullptr, powertoysDirectory, MAX_PATH);
    PathRemoveFileSpecW(powertoysDirectory);

    std::wstring blankImagePath(powertoysDirectory);
    blankImagePath += L"\\Assets\\VCM\\black.bmp";

    _imageOverlayChannel = SerializedSharedMemory::create_readonly(CameraOverlayImageChannel::endpoint(),
                                                                   settings.imageOverlayPath != L"" ? settings.imageOverlayPath : blankImagePath);

    const auto imageSize = static_cast<uint32_t>(_imageOverlayChannel->size());
    _settingsUpdateChannel->access([imageSize](auto memory) {
        auto updatesChannel = reinterpret_cast<CameraSettingsUpdateChannel*>(memory._data);
        updatesChannel->overlayImageSize.emplace(imageSize);
        updatesChannel->newOverlayImagePosted = true;
    });
}
