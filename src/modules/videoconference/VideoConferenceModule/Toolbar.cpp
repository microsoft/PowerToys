#include "pch.h"
#include "Toolbar.h"

#include <windowsx.h>

#include "common/windows_colors.h"

#include "VideoConferenceModule.h"

Toolbar* toolbar = nullptr;

const int REFRESH_RATE = 100;
const int OVERLAY_SHOW_TIME = 500;
const int BORDER_OFFSET = 12;

Toolbar::Toolbar()
{
    toolbar = this;
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

LRESULT Toolbar::WindowProcessMessages(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam)
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
        }

        return DefWindowProcW(hwnd, msg, wparam, lparam);
    }
    case WM_CREATE:
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc;

        hdc = BeginPaint(hwnd, &ps);

        Gdiplus::Graphics graphic(hdc);

        ToolbarImages* themeImages = &toolbar->darkImages;

        if (toolbar->theme == L"light" || (toolbar->theme == L"system" && !WindowsColors::is_dark_mode()))
        {
            themeImages = &toolbar->lightImages;
        }
        else
        {
            themeImages = &toolbar->darkImages;
        }
        Gdiplus::Image* toolbarImage = nullptr;
        if (!toolbar->cameraInUse)
        {
            if (toolbar->microphoneMuted)
            {
                toolbarImage = themeImages->camUnusedMicOff;
            }
            else
            {
                toolbarImage = themeImages->camUnusedMicOn;
            }
        }
        else if (toolbar->microphoneMuted)
        {
            if (toolbar->cameraMuted)
            {
                toolbarImage = themeImages->camOffMicOff;
            }
            else
            {
                toolbarImage = themeImages->camOnMicOff;
            }
        }
        else
        {
            if (toolbar->cameraMuted)
            {
                toolbarImage = themeImages->camOffMicOn;
            }
            else
            {
                toolbarImage = themeImages->camOnMicOn;
            }
        }
        graphic.DrawImage(toolbarImage, 0, 0, toolbarImage->GetWidth(), toolbarImage->GetHeight());

        EndPaint(hwnd, &ps);
        break;
    }
    case WM_TIMER:
    {
        toolbar->cameraInUse = VideoConferenceModule::getVirtualCameraInUse();

        InvalidateRect(hwnd, NULL, NULL);

        using namespace std::chrono;
        const auto nowMillis = duration_cast<milliseconds>(system_clock::now().time_since_epoch()).count();
        const bool showOverlayTimeout = nowMillis - toolbar->lastTimeCamOrMicMuteStateChanged > OVERLAY_SHOW_TIME;

        bool show = false;

        if (toolbar->cameraInUse)
        {
            show = toolbar->HideToolbarWhenUnmuted ? toolbar->microphoneMuted || toolbar->cameraMuted : true;
        }
        else if (toolbar->previouscameraInUse)
        {
            VideoConferenceModule::unmuteAll();
        }
        else
        {
            show = toolbar->microphoneMuted;
        }
        show = show || !showOverlayTimeout;
        if (show)
        {
            ShowWindow(hwnd, SW_SHOW);
        }
        else
        {
            ShowWindow(hwnd, SW_HIDE);
        }

        KillTimer(hwnd, toolbar->nTimerId);
        toolbar->previouscameraInUse = toolbar->cameraInUse;
        break;
    }
    default:
        return DefWindowProcW(hwnd, msg, wparam, lparam);
    }

    toolbar->nTimerId = SetTimer(hwnd, 101, REFRESH_RATE, nullptr);

    return DefWindowProcW(hwnd, msg, wparam, lparam);
}

void Toolbar::show(std::wstring position, std::wstring monitorString)
{
    for (auto& hwnd : hwnds)
    {
        PostMessageW(hwnd, WM_CLOSE, 0, 0);
    }
    hwnds.clear();

    int overlayWidth = darkImages.camOffMicOff->GetWidth();
    int overlayHeight = darkImages.camOffMicOff->GetHeight();

    // Register the window class
    LPCWSTR CLASS_NAME = L"MuteNotificationWindowClass";
    WNDCLASS wc{};
    wc.hInstance = GetModuleHandleW(nullptr);
    wc.lpszClassName = CLASS_NAME;
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)COLOR_WINDOW;
    wc.lpfnWndProc = WindowProcessMessages;
    RegisterClassW(&wc);

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
        hwnd = CreateWindowExW(
            WS_EX_TOOLWINDOW | WS_EX_LAYERED,
            CLASS_NAME,
            CLASS_NAME,
            WS_POPUP,
            positionX,
            positionY,
            overlayWidth,
            overlayHeight,
            nullptr,
            nullptr,
            GetModuleHandleW(nullptr),
            nullptr);

        auto transparrentColorKey = RGB(0, 0, 255);
        HBRUSH brush = CreateSolidBrush(transparrentColorKey);
        SetClassLongPtr(hwnd, GCLP_HBRBACKGROUND, (LONG_PTR)brush);

        SetLayeredWindowAttributes(hwnd, transparrentColorKey, 0, LWA_COLORKEY);

        SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);

        hwnds.push_back(hwnd);
    }
}

void Toolbar::hide()
{
    for (auto& hwnd : hwnds)
    {
        PostMessage(hwnd, WM_CLOSE, 0, 0);
    }
    hwnds.clear();
}

bool Toolbar::getCameraMute()
{
    return cameraMuted;
}

void Toolbar::setCameraMute(bool mute)
{
    if (mute != cameraMuted)
    {
        lastTimeCamOrMicMuteStateChanged = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count();
    }
    cameraMuted = mute;
}

bool Toolbar::getMicrophoneMute()
{
    return microphoneMuted;
}

void Toolbar::setMicrophoneMute(bool mute)
{
    if (mute != microphoneMuted)
    {
        lastTimeCamOrMicMuteStateChanged = std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count();
    }

    microphoneMuted = mute;
}

void Toolbar::setHideToolbarWhenUnmuted(bool hide)
{
    HideToolbarWhenUnmuted = hide;
}

void Toolbar::setTheme(std::wstring theme)
{
    Toolbar::theme = theme;
}
