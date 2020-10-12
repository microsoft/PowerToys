#pragma once

#include <Windows.h>
#include <gdiplus.h>

#include "common/monitors.h"

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

    void show(std::wstring position, std::wstring monitorString);
    void hide();

    bool getCameraMute();
    void setCameraMute(bool mute);
    bool getMicrophoneMute();
    void setMicrophoneMute(bool mute);

    void setTheme(std::wstring theme);
    void setHideToolbarWhenUnmuted(bool hide);

private:
    static LRESULT CALLBACK WindowProcessMessages(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam);

    // Window callback can't be non-static so this members can't as well
    std::vector<HWND> hwnds;

    ToolbarImages darkImages;
    ToolbarImages lightImages;

    bool cameraMuted = false;
    bool cameraInUse = false;
    bool previouscameraInUse = false;
    bool microphoneMuted = false;

    std::wstring theme = L"system";

    bool HideToolbarWhenUnmuted = true;

    uint64_t lastTimeCamOrMicMuteStateChanged;

    UINT_PTR nTimerId;
};
