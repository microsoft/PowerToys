#include "framework.h"
#include "FancyZones_DrawLayoutTest.h"

#include <Uxtheme.h>
#include <objidl.h>
#include <gdiplus.h>
#include <Dwmapi.h>
#include <shellscalingapi.h>

#include <vector>
#include <thread>
#include <string>

using namespace Gdiplus;

#pragma comment (lib,"Gdiplus.lib")
#pragma comment (lib,"UXTheme.lib")
#pragma comment (lib,"Dwmapi.lib")
#pragma comment (lib,"Shcore.lib")

constexpr int   ZONE_COUNT           = 4;
constexpr int   ANIMATION_TIME       = 200; // milliseconds
constexpr int   DISPLAY_REFRESH_TIME = 10;  // milliseconds
constexpr DWORD Q_KEY_CODE           = 0x51;
constexpr DWORD W_KEY_CODE           = 0x57;

HWND  mainWindow;
HHOOK keyboardHook;
bool  showZoneLayout = false;

LRESULT CALLBACK WndProc(HWND, UINT, WPARAM, LPARAM);

std::vector<RECT> zones{};
std::vector<bool> highlighted{};

inline const int RectWidth(const RECT& rect)
{
    return rect.right - rect.left;
}

inline const int RectHeight(const RECT& rect)
{
    return rect.bottom - rect.top;
}

std::vector<RECT> BuildColumnZoneLayout(int zoneCount, const RECT& workArea)
{
    // Builds column layout with specified number of zones (columns).
    int zoneWidth = RectWidth(workArea) / zoneCount;
    int zoneHeight = RectHeight(workArea);
    std::vector<RECT> zones(zoneCount);
    for (int i = 0; i < zoneCount; ++i)
    {
        int left   = workArea.left + i * zoneWidth;
        int top    = workArea.top;
        int right  = left + zoneWidth;
        int bottom = top + zoneHeight;

        zones[i] = { left, top, right, bottom };
    }
    return zones;
}

int GetHighlightedZoneIdx(const std::vector<RECT>& zones, const POINT& cursorPosition)
{
    // Determine which zone should be highlighted based on cursor position.
    for (size_t i = 0; i < zones.size(); ++i)
    {
        if (cursorPosition.x >= zones[i].left && cursorPosition.x < zones[i].right)
        {
            return static_cast<int>(i);
        }
    }
    return -1;
}

void ShowZonesOverlay()
{
    // InvalidateRect will essentially send WM_PAINT to main window.
    UINT flags = SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE;
    SetWindowPos(mainWindow, nullptr, 0, 0, 0, 0, flags);

    std::thread{ [=]() {
        AnimateWindow(mainWindow, ANIMATION_TIME, AW_BLEND);
        InvalidateRect(mainWindow, nullptr, true);
    } }.detach();
}

void HideZonesOverlay()
{
    highlighted = std::vector<bool>(ZONE_COUNT, false);
    ShowWindow(mainWindow, SW_HIDE);
}

void RefreshMainWindow()
{
    while (1)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(DISPLAY_REFRESH_TIME));

        POINT cursorPosition{};
        if (GetCursorPos(&cursorPosition))
        {
            if (showZoneLayout)
            {
                int idx = GetHighlightedZoneIdx(zones, cursorPosition);
                if (idx != -1)
                {
                    if (highlighted[idx]) {
                        // Same zone is active as in previous check, skip invalidating rect.
                    }
                    else
                    {
                        highlighted = std::vector<bool>(ZONE_COUNT, false);
                        highlighted[idx] = true;

                        ShowZonesOverlay();
                    }
                }
            }
        }
    }
}

LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION && wParam == WM_KEYDOWN)
    {
        PKBDLLHOOKSTRUCT info = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
        if (info->vkCode == Q_KEY_CODE)
        {
            PostQuitMessage(0);
            return 1;
        }
        else if (info->vkCode == W_KEY_CODE)
        {
            // Toggle zone layout display.
            showZoneLayout = !showZoneLayout;
            if (showZoneLayout)
            {
                ShowZonesOverlay();
            }
            else
            {
                HideZonesOverlay();
            }
            return 1;
        }
    }
    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}

void StartLowLevelKeyboardHook()
{
    keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandle(nullptr), 0);
}

void StopLowLevelKeyboardHook()
{
    if (keyboardHook)
    {
        UnhookWindowsHookEx(keyboardHook);
        keyboardHook = nullptr;
    }
}


inline void MakeWindowTransparent(HWND window)
{
    int const pos = -GetSystemMetrics(SM_CXVIRTUALSCREEN) - 8;
    if (HRGN hrgn{ CreateRectRgn(pos, 0, (pos + 1), 1) })
    {
        DWM_BLURBEHIND bh = { DWM_BB_ENABLE | DWM_BB_BLURREGION, TRUE, hrgn, FALSE };
        DwmEnableBlurBehindWindow(window, &bh);
    }
}

void RegisterClass(HINSTANCE hInstance)
{
  WNDCLASSEXW wcex{};

  wcex.cbSize        = sizeof(WNDCLASSEX);
  wcex.lpfnWndProc   = WndProc;
  wcex.hInstance     = hInstance;
  wcex.lpszClassName = L"DrawRectangle_Test";
  wcex.hCursor       = LoadCursor(nullptr, IDC_ARROW);

  RegisterClassExW(&wcex);
}

bool InitInstance(HINSTANCE hInstance, int nCmdShow)
{
  MONITORINFO mi{};
  mi.cbSize = sizeof(mi);
  if (!GetMonitorInfo(MonitorFromWindow(nullptr, MONITOR_DEFAULTTOPRIMARY), &mi)) {
    return false;
  }

  mainWindow = CreateWindowExW(WS_EX_TOOLWINDOW,
    L"DrawRectangle_Test",
    L"",
    WS_POPUP,
    mi.rcWork.left,
    mi.rcWork.top,
    RectWidth(mi.rcWork),
    RectHeight(mi.rcWork),
    nullptr,
    nullptr,
    hInstance,
    nullptr);

  if (mainWindow)
  {
    MakeWindowTransparent(mainWindow);
    return true;
  }

  return false;
}

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
    _In_opt_ HINSTANCE hPrevInstance,
    _In_ LPWSTR        lpCmdLine,
    _In_ int             nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(lpCmdLine);

    GdiplusStartupInput gdiplusStartupInput;
    ULONG_PTR                     gdiplusToken;
    GdiplusStartup(&gdiplusToken, &gdiplusStartupInput, nullptr);

    SetProcessDpiAwareness(PROCESS_DPI_UNAWARE);
    StartLowLevelKeyboardHook();

    RegisterClass(hInstance);

    if (!InitInstance(hInstance, nCmdShow))
    {
        return 0;
    }

    RECT clientRect{};
    GetClientRect(mainWindow, &clientRect);
    zones = BuildColumnZoneLayout(ZONE_COUNT, clientRect);
    highlighted = std::vector<bool>(ZONE_COUNT, false);

    // Invoke main window re-drawing from separate thread (based on changes in cursor position).
    std::thread refreshThread = std::thread(RefreshMainWindow);
    refreshThread.detach();


    HACCEL hAccelTable = LoadAccelerators(hInstance, MAKEINTRESOURCE(IDC_FANCYZONESDRAWLAYOUTTEST));
    MSG msg{};
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        if (!TranslateAccelerator(msg.hwnd, hAccelTable, &msg))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    StopLowLevelKeyboardHook();
    GdiplusShutdown(gdiplusToken);

    return (int)msg.wParam;
}

struct ColorSetting
{
    BYTE fillAlpha{};
    COLORREF fill{};
    BYTE borderAlpha{};
    COLORREF border{};
    int thickness{};
};

inline void InitRGB(_Out_ RGBQUAD* quad, BYTE alpha, COLORREF color)
{
    ZeroMemory(quad, sizeof(*quad));
    quad->rgbReserved = alpha;
    quad->rgbRed      = GetRValue(color) * alpha / 255;
    quad->rgbGreen    = GetGValue(color) * alpha / 255;
    quad->rgbBlue     = GetBValue(color) * alpha / 255;
}

inline void FillRectARGB(HDC hdc, const RECT& prcFill, BYTE alpha, COLORREF color, bool blendAlpha)
{
    BITMAPINFO bi;
    ZeroMemory(&bi, sizeof(bi));
    bi.bmiHeader.biSize        = sizeof(BITMAPINFOHEADER);
    bi.bmiHeader.biWidth       = 1;
    bi.bmiHeader.biHeight      = 1;
    bi.bmiHeader.biPlanes      = 1;
    bi.bmiHeader.biBitCount    = 32;
    bi.bmiHeader.biCompression = BI_RGB;

    RECT fillRect;
    CopyRect(&fillRect, &prcFill);

    RGBQUAD bitmapBits;
    InitRGB(&bitmapBits, alpha, color);
    StretchDIBits(
        hdc,
        fillRect.left,
        fillRect.top,
        fillRect.right - fillRect.left,
        fillRect.bottom - fillRect.top,
        0,
        0,
        1,
        1,
        &bitmapBits,
        &bi,
        DIB_RGB_COLORS,
        SRCCOPY);
}

void DrawBackdrop(HDC& hdc, const RECT& clientRect)
{
    FillRectARGB(hdc, clientRect, 0, RGB(0, 0, 0), false);
}

void DrawIndex(HDC hdc, const RECT& rect, size_t index)
{
    Gdiplus::Graphics g(hdc);

    Gdiplus::FontFamily fontFamily(L"Segoe ui");
    Gdiplus::Font font(&fontFamily, 80, Gdiplus::FontStyleRegular, Gdiplus::UnitPixel);
    Gdiplus::SolidBrush solidBrush(Gdiplus::Color(255, 0, 0, 0));

    std::wstring text = std::to_wstring(index);

    g.SetTextRenderingHint(Gdiplus::TextRenderingHintAntiAlias);
    Gdiplus::StringFormat stringFormat = new Gdiplus::StringFormat();
    stringFormat.SetAlignment(Gdiplus::StringAlignmentCenter);
    stringFormat.SetLineAlignment(Gdiplus::StringAlignmentCenter);

    Gdiplus::RectF gdiRect(
        static_cast<Gdiplus::REAL>(rect.left),
        static_cast<Gdiplus::REAL>(rect.top),
        static_cast<Gdiplus::REAL>(RectWidth(rect)),
        static_cast<Gdiplus::REAL>(RectHeight(rect)));

    g.DrawString(text.c_str(), -1, &font, gdiRect, &stringFormat, &solidBrush);
}

void DrawZone(HDC hdc, const ColorSetting& colorSetting, const RECT& rect, size_t index)
{
    Gdiplus::Graphics g(hdc);
    Gdiplus::Color fillColor(colorSetting.fillAlpha, GetRValue(colorSetting.fill), GetGValue(colorSetting.fill), GetBValue(colorSetting.fill));
    Gdiplus::Color borderColor(colorSetting.borderAlpha, GetRValue(colorSetting.border), GetGValue(colorSetting.border), GetBValue(colorSetting.border));

    Gdiplus::Rect rectangle(rect.left, rect.top, RectWidth(rect), RectHeight(rect));

    Gdiplus::Pen pen(borderColor, static_cast<Gdiplus::REAL>(colorSetting.thickness));
    g.FillRectangle(new Gdiplus::SolidBrush(fillColor), rectangle);
    g.DrawRectangle(&pen, rectangle);

    DrawIndex(hdc, rect, index);
}

constexpr inline BYTE OpacitySettingToAlpha(int opacity)
{
    return static_cast<BYTE>(opacity * 2.55);
}

COLORREF ParseColor(const std::wstring& zoneColor)
{
    // Skip the leading # and convert to long
    const auto color = zoneColor;
    const auto tmp   = std::stol(color.substr(1), nullptr, 16);
    const auto nR    = (tmp & 0xFF0000) >> 16;
    const auto nG    = (tmp & 0xFF00) >> 8;
    const auto nB    = (tmp & 0xFF);
    return RGB(nR, nG, nB);
}

static int highlightedIdx = -1;

void OnPaint(HDC hdc)
{
    int zoneOpacity = 50;
    std::wstring zoneColor = L"#0078D7";
    std::wstring zoneBorderColor = L"#FFFFFF";
    std::wstring zoneHighlightColor = L"#F5FCFF";

    ColorSetting color{ OpacitySettingToAlpha(zoneOpacity),
                                            ParseColor(zoneColor),
                                            255,
                                            ParseColor(zoneBorderColor),
                                            -2 };

    ColorSetting highlight{ OpacitySettingToAlpha(zoneOpacity),
                                                    ParseColor(zoneHighlightColor),
                                                    255,
                                                    ParseColor(zoneBorderColor),
                                                    -2 };

    HMONITOR monitor = MonitorFromWindow(nullptr, MONITOR_DEFAULTTOPRIMARY);
    MONITORINFOEX mi;
    mi.cbSize = sizeof(mi);
    GetMonitorInfo(monitor, &mi);

    HDC hdcMem{ nullptr };
    HPAINTBUFFER bufferedPaint = BeginBufferedPaint(hdc, &mi.rcWork, BPBF_TOPDOWNDIB, nullptr, &hdcMem);
    if (bufferedPaint)
    {
        DrawBackdrop(hdcMem, mi.rcWork);
        for (size_t i = 0; i < zones.size(); ++i)
        {
            if (highlighted[i])
            {
                DrawZone(hdcMem, color, zones[i], i);
            }
            else
            {
                DrawZone(hdcMem, highlight, zones[i], i);
            }
        }
        EndBufferedPaint(bufferedPaint, TRUE);
    }
}

LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_NCDESTROY:
    {
        DefWindowProc(mainWindow, message, wParam, lParam);
        SetWindowLongPtr(mainWindow, GWLP_USERDATA, 0);
    }
    break;

    case WM_ERASEBKGND:
        return 1;

    case WM_PRINTCLIENT:
    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint(hWnd, &ps);
        OnPaint(hdc);
        EndPaint(hWnd, &ps);
    }
    break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}
