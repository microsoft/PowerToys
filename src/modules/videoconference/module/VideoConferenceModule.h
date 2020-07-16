#pragma once

#include <mmdeviceapi.h>
#include <endpointvolume.h>

#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>

#include "common/settings_objects.h"

#include "Overlay.h"
#include "CVolumeNotification.h"

#include <SerializedSharedMemory.h>

extern class VideoConferenceModule* instance;

class VideoConferenceModule : public PowertoyModuleIface
{
public:
    VideoConferenceModule();
    ~VideoConferenceModule();
    virtual const wchar_t* get_name() override;
    virtual const wchar_t** get_events() override;
    virtual bool get_config(wchar_t* buffer, int* buffer_size) override;

    virtual void set_config(const wchar_t* config) override;

    virtual void enable() override;
    virtual void disable() override;
    virtual bool is_enabled() override;

    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override;

    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}
    virtual void signal_system_menu_action(const wchar_t* name) override {}

    virtual void destroy() override;

    void sendSourceCameraNameUpdate();
    void sendOverlayImageUpdate();

private:
    void init_settings();

    //  all callback methods and used by callback have to be static
    static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);
    static bool isKeyPressed(unsigned int keyCode);
    static bool isHotkeyPressed(DWORD code, PowerToysSettings::HotkeyObject& hotkey);
    static void reverseMicrophoneMute();
    static bool getMicrophoneMuteState();
    static void reverseVirtualCameraMuteState();
    static bool getVirtualCameraMuteState();

    static HHOOK hook_handle;
    bool _enabled = false;

    std::optional<SerializedSharedMemory> _imageOverlayChannel;
    std::optional<SerializedSharedMemory> _settingsUpdateChannel;

    static Overlay overlay;

    static CVolumeNotification* volumeNotification;

    static PowerToysSettings::HotkeyObject cameraAndMicrophoneMuteHotkey;
    static PowerToysSettings::HotkeyObject microphoneMuteHotkey;
    static PowerToysSettings::HotkeyObject cameraMuteHotkey;

    static std::wstring overlayPositionString;
    static std::wstring overlayMonitorString;

    static std::wstring selectedCamera;
    static std::wstring imageOverlayPath;

    static std::mutex keyboardInputMutex;
};
