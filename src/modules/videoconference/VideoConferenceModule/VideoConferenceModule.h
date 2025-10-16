#pragma once

#include <common/SettingsAPI/FileWatcher.h>

#include <mmdeviceapi.h>
#include <endpointvolume.h>

#include <interface/powertoy_module_interface.h>

#include <common/SettingsAPI/settings_objects.h>
#include <MicrophoneDevice.h>

#include "Toolbar.h"

#include <SerializedSharedMemory.h>

extern class VideoConferenceModule* instance;

struct VideoConferenceSettings
{
    PowerToysSettings::HotkeyObject cameraAndMicrophoneMuteHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, 81);
    PowerToysSettings::HotkeyObject microphoneMuteHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, 65);
    PowerToysSettings::HotkeyObject microphonePushToTalkHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, 73);
    PowerToysSettings::HotkeyObject cameraMuteHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, 79);

    std::wstring toolbarPositionString;
    std::wstring toolbarMonitorString;

    std::wstring selectedCamera;
    std::wstring imageOverlayPath;
    std::wstring selectedMicrophone;

    std::wstring startupAction;

    bool pushToReverseEnabled = false;
};

class VideoConferenceModule : public PowertoyModuleIface
{
public:
    VideoConferenceModule();
    ~VideoConferenceModule();
    virtual const wchar_t* get_name() override;

    virtual powertoys_gpo::gpo_rule_configured_t gpo_policy_enabled_configuration() override;

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override;

    virtual void set_config(const wchar_t* config) override;

    virtual void enable() override;
    virtual void disable() override;
    virtual bool is_enabled() override;
    virtual void destroy() override;
    virtual bool is_enabled_by_default() const override;

    virtual const wchar_t * get_key() override;

    void sendSourceCameraNameUpdate();
    void sendOverlayImageUpdate();

    static void unmuteAll();
    static void muteAll();
    static void reverseMicrophoneMute();
    static bool getMicrophoneMuteState();
    static void reverseVirtualCameraMuteState();
    static bool getVirtualCameraMuteState();
    static bool getVirtualCameraInUse();

    void onGeneralSettingsChanged();
    void onModuleSettingsChanged();
    void onMicrophoneConfigurationChanged();

private:

    void init_settings();
    void updateControlledMicrophones(const std::wstring_view new_mic);
    MicrophoneDevice* controlledDefaultMic();

    //  all callback methods and used by callback have to be static
    static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);
    static bool isKeyPressed(unsigned int keyCode);
    static bool isHotkeyPressed(DWORD code, PowerToysSettings::HotkeyObject& hotkey);

    static HHOOK hook_handle;
    bool _enabled = false;

    bool _mic_muted_state_during_disconnect = false;
    bool _controllingAllMics = false;
    std::vector<std::unique_ptr<MicrophoneDevice>> _controlledMicrophones;
    MicrophoneDevice* _microphoneTrackedInUI = nullptr;

    std::optional<SerializedSharedMemory> _imageOverlayChannel;
    std::optional<SerializedSharedMemory> _settingsUpdateChannel;

    std::unique_ptr<FileWatcher> _generalSettingsWatcher;
    std::unique_ptr<FileWatcher> _moduleSettingsWatcher;

    static VideoConferenceSettings settings;
    static Toolbar toolbar;
    static bool pushToTalkPressed;
};
