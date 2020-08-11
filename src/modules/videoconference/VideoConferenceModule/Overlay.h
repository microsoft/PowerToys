#pragma once

#include <Windows.h>
#include <gdiplus.h>

#include "common/monitors.h"

struct OverlayImages
{
    Gdiplus::Image* camOnMicOn = nullptr;
    Gdiplus::Image* camOffMicOn = nullptr;
    Gdiplus::Image* camOnMicOff = nullptr;
    Gdiplus::Image* camOffMicOff = nullptr;
    Gdiplus::Image* camUnusedMicOn = nullptr;
    Gdiplus::Image* camUnusedMicOff = nullptr;
};

class Overlay
{
public:
    Overlay();

    static void showOverlay(std::wstring position, std::wstring monitorString);
    static void hideOverlay();

    bool static getCameraMute();
    void static setCameraMute(bool mute);
    bool static getMicrophoneMute();
    void static setMicrophoneMute(bool mute);

    void static setTheme(std::wstring theme);
    void static setHideOverlayWhenUnmuted(bool hide);

private:
    static LRESULT CALLBACK WindowProcessMessages(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam);

    // Window callback can't be non-static so this members can't as well
    static std::vector<HWND> hwnds;

    static OverlayImages darkImages;
    static OverlayImages lightImages;

    static bool valueUpdated;
    static bool cameraMuted;
    static bool cameraInUse;
    static bool microphoneMuted;

    static std::wstring theme;

    static bool hideOverlayWhenUnmuted;

    static unsigned __int64 lastTimeCamOrMicMuteStateChanged;

    static UINT_PTR nTimerId;
};
