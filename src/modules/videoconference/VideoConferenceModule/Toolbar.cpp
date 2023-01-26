#include "pch.h"
#include "Toolbar.h"

#include <windowsx.h>

#include <common/Themes/windows_colors.h>

#include "Logging.h"
#include "VideoConferenceModule.h"

Toolbar* toolbar = nullptr;

const int REFRESH_RATE = 100;
const int OVERLAY_SHOW_TIME = 500;
const int BORDER_OFFSET = 12;
const int TOP_RIGHT_BORDER_OFFSET = 40;

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

void Toolbar::scheduleModuleSettingsUpdate()
{
    moduleSettingsUpdateScheduled = true;
}

void Toolbar::scheduleGeneralSettingsUpdate()
{
    generalSettingsUpdateScheduled = true;
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
        if (toolbar->audioConfChangesNotifier.PullPendingNotifications())
        {
            instance->onMicrophoneConfigurationChanged();
        }
        toolbar->microphoneMuted = instance->getMicrophoneMuteState();

        if (toolbar->generalSettingsUpdateScheduled)
        {
            instance->onGeneralSettingsChanged();
            toolbar->generalSettingsUpdateScheduled = false;
        }
        if (toolbar->moduleSettingsUpdateScheduled)
        {
            instance->onModuleSettingsChanged();
            toolbar->moduleSettingsUpdateScheduled = false;
        }

        toolbar->cameraInUse = VideoConferenceModule::getVirtualCameraInUse();

        InvalidateRect(hwnd, NULL, NULL);

        using namespace std::chrono;
        const auto nowMillis = duration_cast<milliseconds>(system_clock::now().time_since_epoch()).count();
        const bool showOverlayTimeout = nowMillis - toolbar->lastTimeCamOrMicMuteStateChanged > OVERLAY_SHOW_TIME;

        static bool previousShow = false;
        bool show = toolbar->ToolbarHide == L"Never";

        const bool cameraJustStoppedInUse = toolbar->previouscameraInUse && !toolbar->cameraInUse;
        bool shouldUnmuteAll = cameraJustStoppedInUse;

        if (toolbar->ToolbarHide == L"When both camera and microphone are muted")
        {
            // We shouldn't unmute devices, since we'd like to only show the toolbar only
            // when something is unmuted -> the use case is to keep everything muted by default and track it
            shouldUnmuteAll = false;
            show = (!toolbar->cameraMuted && toolbar->cameraInUse) || !toolbar->microphoneMuted;
        }
        else if (toolbar->ToolbarHide == L"When both camera and microphone are unmuted")
            show = (toolbar->cameraMuted && toolbar->cameraInUse) || toolbar->microphoneMuted;

        if (shouldUnmuteAll && !toolbar->moduleSettingsUpdateScheduled)
            VideoConferenceModule::unmuteAll();

        show = show || !showOverlayTimeout;
        ShowWindow(hwnd, show ? SW_SHOW : SW_HIDE);

        if (previousShow != show)
        {
            previousShow = show;
            LOG(show ? "Toolbar visibility changed to shown" : "Toolbar visibility changed to hidden");
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
    wc.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW);
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
        const auto screenSize = monitorInfo.GetScreenSize(false);
        int positionX = 0;
        int positionY = 0;

        if (position == L"Top left corner")
        {
            positionX = screenSize.left() + BORDER_OFFSET;
            positionY = screenSize.top() + BORDER_OFFSET;
        }
        else if (position == L"Top center")
        {
            positionX = screenSize.middle().x - overlayWidth / 2;
            positionY = screenSize.top() + BORDER_OFFSET;
        }
        else if (position == L"Bottom left corner")
        {
            positionX = screenSize.left() + BORDER_OFFSET;
            positionY = screenSize.bottom() - overlayHeight - BORDER_OFFSET;
        }
        else if (position == L"Bottom center")
        {
            positionX = screenSize.middle().x - overlayWidth / 2;
            positionY = screenSize.bottom() - overlayHeight - BORDER_OFFSET;
        }
        else if (position == L"Bottom right corner")
        {
            positionX = screenSize.right() - overlayWidth - BORDER_OFFSET;
            positionY = screenSize.bottom() - overlayHeight - BORDER_OFFSET;
        }
        else //"Top right corner" or non-present
        {
            positionX = screenSize.right() - overlayWidth - BORDER_OFFSET;
            positionY = screenSize.top() + TOP_RIGHT_BORDER_OFFSET;
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

        auto transparentColorKey = RGB(0, 0, 255);
        HBRUSH brush = CreateSolidBrush(transparentColorKey);
        SetClassLongPtr(hwnd, GCLP_HBRBACKGROUND, reinterpret_cast<LONG_PTR>(brush));

        SetLayeredWindowAttributes(hwnd, transparentColorKey, 0, LWA_COLORKEY);

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

void Toolbar::setToolbarHide(std::wstring hide)
{
    ToolbarHide = hide;
}

void Toolbar::setTheme(std::wstring theme)
{
    Toolbar::theme = theme;
}
