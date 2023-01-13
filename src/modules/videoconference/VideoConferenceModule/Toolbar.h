#pragma once

#include <Windows.h>
#include <gdiplus.h>
#include <atomic>

#include <common/Display/monitors.h>

#include "AudioDeviceNotificationClient.h"

struct ToolbarImages
{
    Gdiplus::Image* camOnMicOn = nullptr;
    Gdiplus::Image* camOffMicOn = nullptr;
    Gdiplus::Image* camOnMicOff = nullptr;
    Gdiplus::Image* camOffMicOff = nullptr;
    Gdiplus::Image* camUnusedMicOn = nullptr;
    Gdiplus::Image* camUnusedMicOff = nullptr;
};

class Toolbar
{
public:
    Toolbar();

    void scheduleModuleSettingsUpdate();
    void scheduleGeneralSettingsUpdate();

    void show(std::wstring position, std::wstring monitorString);
    void hide();

    bool getCameraMute();
    void setCameraMute(bool mute);
    bool getMicrophoneMute();
    void setMicrophoneMute(bool mute);

    void setTheme(std::wstring theme);
    void setToolbarHide(std::wstring hide);

private:
    static LRESULT CALLBACK WindowProcessMessages(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam);

    // Window callback can't be non-static so this members can't as well
    std::vector<HWND> hwnds;

    ToolbarImages darkImages;
    ToolbarImages lightImages;
    AudioDeviceNotificationClient audioConfChangesNotifier;

    bool cameraMuted = false;
    bool cameraInUse = false;
    bool previouscameraInUse = false;
    bool microphoneMuted = false;

    std::wstring theme = L"system";

    std::wstring ToolbarHide = L"When both camera and microphone are unmuted";

    uint64_t lastTimeCamOrMicMuteStateChanged{};

    std::atomic_bool moduleSettingsUpdateScheduled = false;
    std::atomic_bool generalSettingsUpdateScheduled = false;
    UINT_PTR nTimerId{};
};
