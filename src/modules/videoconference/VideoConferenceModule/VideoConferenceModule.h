#pragma once

#include <mmdeviceapi.h>
#include <endpointvolume.h>

#include <interface/powertoy_module_interface.h>

#include "common/settings_objects.h"

#include "Toolbar.h"
#include "CVolumeNotification.h"

#include <SerializedSharedMemory.h>

extern class VideoConferenceModule* instance;

struct VideoConferenceSettings
{
    CVolumeNotification* volumeNotification = nullptr;

    PowerToysSettings::HotkeyObject cameraAndMicrophoneMuteHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, 78);
    PowerToysSettings::HotkeyObject microphoneMuteHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, 65);
    PowerToysSettings::HotkeyObject cameraMuteHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, 79);

    std::wstring toolbarPositionString;
    std::wstring toolbarMonitorString;

    std::wstring selectedCamera;
    std::wstring imageOverlayPath;
};

class VideoConferenceModule : public PowertoyModuleIface
{
public:
    VideoConferenceModule();
    ~VideoConferenceModule();
    virtual const wchar_t* get_name() override;

    virtual bool get_config(wchar_t* buffer, int* buffer_size) override;

    virtual void set_config(const wchar_t* config) override;

    virtual void enable() override;
    virtual void disable() override;
    virtual bool is_enabled() override;
    virtual void destroy() override;

    void sendSourceCameraNameUpdate();
    void sendOverlayImageUpdate();

    static void reverseMicrophoneMute();
    static bool getMicrophoneMuteState();
    static void reverseVirtualCameraMuteState();
    static bool getVirtualCameraMuteState();
    static bool getVirtualCameraInUse();

private:
    void init_settings();

    //  all callback methods and used by callback have to be static
    static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);
    static bool isKeyPressed(unsigned int keyCode);
    static bool isHotkeyPressed(DWORD code, PowerToysSettings::HotkeyObject& hotkey);

    static HHOOK hook_handle;
    bool _enabled = false;

    std::optional<SerializedSharedMemory> _imageOverlayChannel;
    std::optional<SerializedSharedMemory> _settingsUpdateChannel;

    static VideoConferenceSettings settings;
    static Toolbar toolbar;
};
