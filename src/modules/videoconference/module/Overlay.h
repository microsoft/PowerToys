#pragma once

#include <Windows.h>
#include <gdiplus.h>

#include "common/monitors.h"

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

private:
    static LRESULT CALLBACK WindowProcessMessages(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam);

    // Window callback can't be non-static so this members can't as well
    static std::vector<HWND> hwnds;

    static Gdiplus::Image* camOnMicOnBitmap;
    static Gdiplus::Image* camOffMicOnBitmap;
    static Gdiplus::Image* camOnMicOffBitmap;
    static Gdiplus::Image* camOffMicOffBitmap;

    static bool valueChanged;
    static bool cameraMuted;
    static bool microphoneMuted;

    static unsigned __int64 lastTimeCamOrMicMuted;

    static UINT_PTR nTimerId;
};
