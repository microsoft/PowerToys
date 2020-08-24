#include "pch.h"
#include "Overlay.h"

#include <windowsx.h>

#include "common/windows_colors.h"

#include "VideoConferenceModule.h"

OverlayImages Overlay::darkImages;
OverlayImages Overlay::lightImages;

bool Overlay::valueUpdated = false;
bool Overlay::cameraMuted = false;
bool Overlay::cameraInUse = false;
bool Overlay::microphoneMuted = false;

std::wstring Overlay::theme = L"system";

bool Overlay::hideOverlayWhenUnmuted = true;

std::vector<HWND> Overlay::hwnds;

UINT_PTR Overlay::nTimerId;

unsigned __int64 Overlay::lastTimeCamOrMicMuteStateChanged;

const int REFRESH_RATE = 100;
const int OVERLAY_SHOW_TIME = 500;
const int BORDER_OFFSET = 12;

Overlay::Overlay()
{
    darkImages.camOnMicOn = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/On-On Dark.png");
    darkImages.camOffMicOn = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/On-Off Dark.png");
    darkImages.camOnMicOff = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/Off-On Dark.png");
    darkImages.camOffMicOff = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/Off-Off Dark.png");
    darkImages.camUnusedMicOn = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/On-NotInUse Dark.png");
    darkImages.camUnusedMicOff = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/Off-NotInUse Dark.png");

    lightImages.camOnMicOn = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/On-On Light.png");
    lightImages.camOffMicOn = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/On-Off Light.png");
    lightImages.camOnMicOff = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/Off-On Light.png");
    lightImages.camOffMicOff = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/Off-Off Light.png");
    lightImages.camUnusedMicOn = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/On-NotInUse Light.png");
    lightImages.camUnusedMicOff = Gdiplus::Image::FromFile(L"modules/VideoConference/Icons/Off-NotInUse Light.png");
}

LRESULT Overlay::WindowProcessMessages(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam)
{
    switch (msg)
    {
    case WM_DESTROY:
        return 0;
    case WM_LBUTTONDOWN:
    {
        int x = GET_X_LPARAM(lparam);
        int y = GET_Y_LPARAM(lparam);

        if (x < 322 / 2)
        {
            VideoConferenceModule::reverseMicrophoneMute();
        }
        else
        {
            VideoConferenceModule::reverseVirtualCameraMuteState();
            setCameraMute(VideoConferenceModule::getVirtualCameraMuteState());
        }

        return DefWindowProc(hwnd, msg, wparam, lparam);
    }
    case WM_CREATE:
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc;

        hdc = BeginPaint(hwnd, &ps);

        Gdiplus::Graphics graphic(hdc);

        OverlayImages* themeImages = &darkImages;

        if (theme == L"light" || (theme == L"system" && !WindowsColors::is_dark_mode()))
        {
            themeImages = &lightImages;
        }
        else
        {
            themeImages = &darkImages;
        }

        if (!cameraInUse)
        {
            if (microphoneMuted)
            {
                graphic.DrawImage(themeImages->camUnusedMicOff, 0, 0, themeImages->camUnusedMicOff->GetWidth(), themeImages->camUnusedMicOff->GetHeight());
            }
            else
            {
                graphic.DrawImage(themeImages->camUnusedMicOn, 0, 0, themeImages->camUnusedMicOn->GetWidth(), themeImages->camUnusedMicOn->GetHeight());
            }
        }
        else if (microphoneMuted )
        {
            if (cameraMuted)
            {
                graphic.DrawImage(themeImages->camOffMicOff, 0, 0, themeImages->camOffMicOff->GetWidth(), themeImages->camOffMicOff->GetHeight());
            }
            else
            {
                graphic.DrawImage(themeImages->camOnMicOff, 0, 0, themeImages->camOnMicOff->GetWidth(), themeImages->camOnMicOff->GetHeight());
            }
        }
        else
        {
            if (cameraMuted)
            {
                graphic.DrawImage(themeImages->camOffMicOn, 0, 0, themeImages->camOffMicOn->GetWidth(), themeImages->camOffMicOn->GetHeight());
            }
            else
            {
                graphic.DrawImage(themeImages->camOnMicOn, 0, 0, themeImages->camOnMicOn->GetWidth(), themeImages->camOnMicOn->GetHeight());
            }
        }

        EndPaint(hwnd, &ps);
        break;
    }
    case WM_TIMER:
    {
        cameraInUse = VideoConferenceModule::getVirtualCameraInUse();

        InvalidateRect(hwnd, NULL, NULL);
        using namespace std::chrono;

        if (cameraInUse || microphoneMuted || !hideOverlayWhenUnmuted)
        {
            ShowWindow(hwnd, SW_SHOW);
        }
        else
        {
            if (duration_cast<milliseconds>(system_clock::now().time_since_epoch()).count() - lastTimeCamOrMicMuteStateChanged > OVERLAY_SHOW_TIME || !valueUpdated)
            {
                ShowWindow(hwnd, SW_HIDE);
            }
            else
            {
                ShowWindow(hwnd, SW_SHOW);
            }
        }

        KillTimer(hwnd, nTimerId);

        break;
    }
    default:
        return DefWindowProc(hwnd, msg, wparam, lparam);
    }

    nTimerId = SetTimer(hwnd, 101, REFRESH_RATE, NULL);

    return DefWindowProc(hwnd, msg, wparam, lparam);
}

void Overlay::showOverlay(std::wstring position, std::wstring monitorString)
{
    valueUpdated = false;
    for (auto& hwnd : hwnds)
    {
        PostMessage(hwnd, WM_CLOSE, 0, 0);
    }
    hwnds.clear();

    int overlayWidth = darkImages.camOffMicOff->GetWidth();
    int overlayHeight = darkImages.camOffMicOff->GetHeight();

    // Register the window class
    LPCWSTR CLASS_NAME = L"MuteNotificationWindowClass";
    WNDCLASS wc{};
    wc.hInstance = GetModuleHandle(NULL);
    wc.lpszClassName = CLASS_NAME;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)COLOR_WINDOW;
    wc.lpfnWndProc = WindowProcessMessages;
    RegisterClass(&wc);

    // Create the window
    DWORD dwExtStyle = 0;
    DWORD dwStyle = WS_POPUPWINDOW;

    std::vector<MonitorInfo> monitorInfos;

    if (monitorString == L"All monitors")
    {
        monitorInfos = MonitorInfo::GetMonitors(false);
    }
    else //"Main monitor" or non-present
    {
        monitorInfos.push_back(MonitorInfo::GetPrimaryMonitor());
    }

    for (auto& monitorInfo : monitorInfos)
    {
        int positionX = 0;
        int positionY = 0;

        if (position == L"Top left corner")
        {
            positionX = monitorInfo.left() + BORDER_OFFSET;
            positionY = monitorInfo.top() + BORDER_OFFSET;
        }
        else if (position == L"Top center")
        {
            positionX = monitorInfo.middle().x - overlayWidth / 2;
            positionY = monitorInfo.top() + BORDER_OFFSET;
        }
        else if (position == L"Bottom left corner")
        {
            positionX = monitorInfo.left() + BORDER_OFFSET;
            positionY = monitorInfo.bottom() - overlayHeight - BORDER_OFFSET;
        }
        else if (position == L"Bottom center")
        {
            positionX = monitorInfo.middle().x - overlayWidth / 2;
            positionY = monitorInfo.bottom() - overlayHeight - BORDER_OFFSET;
        }
        else if (position == L"Bottom right corner")
        {
            positionX = monitorInfo.right() - overlayWidth - BORDER_OFFSET;
            positionY = monitorInfo.bottom() - overlayHeight - BORDER_OFFSET;
        }
        else //"Top right corner" or non-present
        {
            positionX = monitorInfo.right() - overlayWidth - BORDER_OFFSET;
            positionY = monitorInfo.top() + BORDER_OFFSET;
        }

        HWND hwnd;
        hwnd = CreateWindowEx(
            WS_EX_TOOLWINDOW | WS_EX_LAYERED,
            CLASS_NAME,
            CLASS_NAME,
            WS_POPUP,
            positionX,
            positionY,
            overlayWidth,
            overlayHeight,
            NULL,
            NULL,
            GetModuleHandle(NULL),
            NULL);

        auto transparrentColorKey = RGB(0, 0, 255);
        HBRUSH brush = CreateSolidBrush(transparrentColorKey);
        SetClassLongPtr(hwnd, GCLP_HBRBACKGROUND, (LONG_PTR)brush);

        SetLayeredWindowAttributes(hwnd, transparrentColorKey, 0, LWA_COLORKEY);

        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

        hwnds.push_back(hwnd);
    }
}

void Overlay::hideOverlay()
{
    for (auto& hwnd : hwnds)
    {
        PostMessage(hwnd, WM_CLOSE, 0, 0);
    }
    hwnds.clear();
}

bool Overlay::getCameraMute()
{
    return cameraMuted;
}

void Overlay::setCameraMute(bool mute)
{
    valueUpdated = true;
    lastTimeCamOrMicMuteStateChanged = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count();
    cameraMuted = mute;
}

bool Overlay::getMicrophoneMute()
{
    return microphoneMuted;
}

void Overlay::setMicrophoneMute(bool mute)
{
    if (mute != microphoneMuted)
    {
        valueUpdated = true;
        lastTimeCamOrMicMuteStateChanged = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count();
    }

    microphoneMuted = mute;
}

void Overlay::setHideOverlayWhenUnmuted(bool hide)
{
    hideOverlayWhenUnmuted = hide;
}

void Overlay::setTheme(std::wstring theme)
{
    Overlay::theme = theme;
}
