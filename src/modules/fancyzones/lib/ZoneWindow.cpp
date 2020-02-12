#include "pch.h"

#include <common/common.h>

#include "ZoneWindow.h"
#include "trace.h"
#include "util.h"
#include "RegistryHelpers.h"

#include <ShellScalingApi.h>
#include <mutex>

namespace ZoneWindowUtils
{
    const std::wstring& GetActiveZoneSetTmpPath()
    {
        static std::wstring activeZoneSetTmpFileName;
        static std::once_flag flag;

        std::call_once(flag, []() {
            wchar_t fileName[L_tmpnam_s];

            if (_wtmpnam_s(fileName, L_tmpnam_s) != 0)
                abort();

            activeZoneSetTmpFileName = std::wstring{ fileName };
        });

        return activeZoneSetTmpFileName;
    }

    const std::wstring& GetAppliedZoneSetTmpPath()
    {
        static std::wstring appliedZoneSetTmpFileName;
        static std::once_flag flag;

        std::call_once(flag, []() {
            wchar_t fileName[L_tmpnam_s];

            if (_wtmpnam_s(fileName, L_tmpnam_s) != 0)
                abort();

            appliedZoneSetTmpFileName = std::wstring{ fileName };
        });

        return appliedZoneSetTmpFileName;
    }

    const std::wstring& GetCustomZoneSetsTmpPath()
    {
        static std::wstring customZoneSetsTmpFileName;
        static std::once_flag flag;

        std::call_once(flag, []() {
            wchar_t fileName[L_tmpnam_s];

            if (_wtmpnam_s(fileName, L_tmpnam_s) != 0)
                abort();

            customZoneSetsTmpFileName = std::wstring{ fileName };
        });

        return customZoneSetsTmpFileName;
    }

    std::wstring GenerateUniqueId(HMONITOR monitor, PCWSTR deviceId, PCWSTR virtualDesktopId)
    {
        wchar_t uniqueId[256]{}; // Parsed deviceId + resolution + virtualDesktopId

        MONITORINFOEXW mi;
        mi.cbSize = sizeof(mi);
        if (virtualDesktopId && GetMonitorInfo(monitor, &mi))
        {
            wchar_t parsedId[256]{};
            ParseDeviceId(deviceId, parsedId, 256);

            Rect const monitorRect(mi.rcMonitor);
            StringCchPrintf(uniqueId, ARRAYSIZE(uniqueId), L"%s_%d_%d_%s", parsedId, monitorRect.width(), monitorRect.height(), virtualDesktopId);
        }
        return std::wstring{ uniqueId };
    }
}

namespace ZoneWindowDrawUtils
{
    struct ColorSetting
    {
        BYTE fillAlpha{};
        COLORREF fill{};
        BYTE borderAlpha{};
        COLORREF border{};
        int thickness{};
    };

    bool IsOccluded(const std::vector<winrt::com_ptr<IZone>>& zones, POINT pt, size_t index) noexcept
    {
        size_t i = 1;

        for (auto iter = zones.begin(); iter != zones.end(); iter++)
        {
            if (winrt::com_ptr<IZone> zone = iter->try_as<IZone>())
            {
                if (i < index)
                {
                    if (PtInRect(&zone->GetZoneRect(), pt))
                    {
                        return true;
                    }
                }
            }
            i++;
        }
        return false;
    }

    void DrawBackdrop(wil::unique_hdc& hdc, RECT const& clientRect) noexcept
    {
        FillRectARGB(hdc, &clientRect, 0, RGB(0, 0, 0), false);
    }

    void DrawIndex(wil::unique_hdc& hdc, POINT offset, size_t index, int padding, int size, bool flipX, bool flipY, COLORREF colorFill)
    {
        RECT rect = { offset.x, offset.y, offset.x + size, offset.y + size };
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                RECT useRect = rect;
                if (flipX)
                {
                    if (x == 0)
                        useRect.left += (size + padding + size + padding);
                    else if (x == 2)
                        useRect.left -= (size + padding + size + padding);
                    useRect.right = useRect.left + size;
                }

                if (flipY)
                {
                    if (y == 0)
                        useRect.top += (size + padding + size + padding);
                    else if (y == 2)
                        useRect.top -= (size + padding + size + padding);
                    useRect.bottom = useRect.top + size;
                }

                FillRectARGB(hdc, &useRect, 200, RGB(50, 50, 50), true);

                RECT inside = useRect;
                InflateRect(&inside, -2, -2);

                FillRectARGB(hdc, &inside, 100, colorFill, true);

                rect.left += (size + padding);
                rect.right = rect.left + size;

                if (--index == 0)
                {
                    return;
                }
            }
            rect.left = offset.x;
            rect.right = rect.left + size;
            rect.top += (size + padding);
            rect.bottom = rect.top + size;
        }
    }

    void DrawZone(wil::unique_hdc& hdc, ColorSetting const& colorSetting, winrt::com_ptr<IZone> zone, const std::vector<winrt::com_ptr<IZone>>& zones, bool flashMode) noexcept
    {
        RECT zoneRect = zone->GetZoneRect();
        if (colorSetting.borderAlpha > 0)
        {
            FillRectARGB(hdc, &zoneRect, colorSetting.borderAlpha, colorSetting.border, false);
            InflateRect(&zoneRect, colorSetting.thickness, colorSetting.thickness);
        }
        FillRectARGB(hdc, &zoneRect, colorSetting.fillAlpha, colorSetting.fill, false);

        if (flashMode)
        {
            return;
        }
        COLORREF const colorFill = RGB(255, 255, 255);

        size_t const index = zone->Id();
        int const padding = 5;
        int const size = 10;
        POINT offset = { zoneRect.left + padding, zoneRect.top + padding };
        if (!IsOccluded(zones, offset, index))
        {
            DrawIndex(hdc, offset, index, padding, size, false, false, colorFill); // top left
            return;
        }

        offset.x = zoneRect.right - ((padding + size) * 3);
        if (!IsOccluded(zones, offset, index))
        {
            DrawIndex(hdc, offset, index, padding, size, true, false, colorFill); // top right
            return;
        }

        offset.y = zoneRect.bottom - ((padding + size) * 3);
        if (!IsOccluded(zones, offset, index))
        {
            DrawIndex(hdc, offset, index, padding, size, true, true, colorFill); // bottom right
            return;
        }

        offset.x = zoneRect.left + padding;
        DrawIndex(hdc, offset, index, padding, size, false, true, colorFill); // bottom left
    }

    void DrawActiveZoneSet(wil::unique_hdc& hdc, COLORREF highlightColor, int highlightOpacity, const std::vector<winrt::com_ptr<IZone>>& zones, const winrt::com_ptr<IZone>& highlightZone, bool flashMode, bool drawHints) noexcept
    {
        static constexpr std::array<COLORREF, 9> colors{
            RGB(75, 75, 85),
            RGB(150, 150, 160),
            RGB(100, 100, 110),
            RGB(125, 125, 135),
            RGB(225, 225, 235),
            RGB(25, 25, 35),
            RGB(200, 200, 210),
            RGB(50, 50, 60),
            RGB(175, 175, 185),
        };

        //                                 { fillAlpha, fill, borderAlpha, border, thickness }
        ColorSetting const colorHints{ 225, RGB(81, 92, 107), 255, RGB(104, 118, 138), -2 };
        ColorSetting colorViewer{ OpacitySettingToAlpha(highlightOpacity), 0, 255, RGB(40, 50, 60), -2 };
        ColorSetting colorHighlight{ OpacitySettingToAlpha(highlightOpacity), 0, 255, 0, -2 };
        ColorSetting const colorFlash{ 200, RGB(81, 92, 107), 200, RGB(104, 118, 138), -2 };

        const size_t maxColorIndex = min(size(zones) - 1, size(colors) - 1);
        size_t colorIndex = maxColorIndex;
        for (auto iter = zones.begin(); iter != zones.end(); iter++)
        {
            winrt::com_ptr<IZone> zone = iter->try_as<IZone>();
            if (!zone)
            {
                continue;
            }

            if (zone != highlightZone)
            {
                if (flashMode)
                {
                    DrawZone(hdc, colorFlash, zone, zones, flashMode);
                }
                else if (drawHints)
                {
                    DrawZone(hdc, colorHints, zone, zones, flashMode);
                }
                {
                    colorViewer.fill = colors[colorIndex];
                    DrawZone(hdc, colorViewer, zone, zones, flashMode);
                }
            }
            colorIndex = colorIndex != 0 ? colorIndex - 1 : maxColorIndex;
        }

        if (highlightZone)
        {
            colorHighlight.fill = highlightColor;
            colorHighlight.border = RGB(
                max(0, GetRValue(colorHighlight.fill) - 25),
                max(0, GetGValue(colorHighlight.fill) - 25),
                max(0, GetBValue(colorHighlight.fill) - 25));
            DrawZone(hdc, colorHighlight, highlightZone, zones, flashMode);
        }
    }
}

struct ZoneWindow : public winrt::implements<ZoneWindow, IZoneWindow>
{
public:
    ZoneWindow(HINSTANCE hinstance);
    bool Init(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, const std::wstring& uniqueId, bool flashZones);

    IFACEMETHODIMP MoveSizeEnter(HWND window, bool dragEnabled) noexcept;
    IFACEMETHODIMP MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled) noexcept;
    IFACEMETHODIMP MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept;
    IFACEMETHODIMP MoveSizeCancel() noexcept;
    IFACEMETHODIMP_(bool) IsDragEnabled() noexcept { return m_dragEnabled; }
    IFACEMETHODIMP_(void) MoveWindowIntoZoneByIndex(HWND window, int index) noexcept;
    IFACEMETHODIMP_(void) MoveWindowIntoZoneByDirection(HWND window, DWORD vkCode) noexcept;
    IFACEMETHODIMP_(void) CycleActiveZoneSet(DWORD vkCode) noexcept;
    IFACEMETHODIMP_(std::wstring) UniqueId() noexcept { return { m_uniqueId }; }
    IFACEMETHODIMP_(std::wstring) WorkAreaKey() noexcept { return { m_workArea }; }
    IFACEMETHODIMP_(void) SaveWindowProcessToZoneIndex(HWND window) noexcept;
    IFACEMETHODIMP_(IZoneSet*) ActiveZoneSet() noexcept { return m_activeZoneSet.get(); }

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

private:
    void ShowZoneWindow() noexcept;
    void HideZoneWindow() noexcept;
    void LoadSettings() noexcept;
    void InitializeZoneSets(MONITORINFO const& mi) noexcept;
    void CalculateZoneSet() noexcept;
    void UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept;
    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    void OnPaint(wil::unique_hdc& hdc) noexcept;
    void OnKeyUp(WPARAM wparam) noexcept;
    winrt::com_ptr<IZone> ZoneFromPoint(POINT pt) noexcept;
    void CycleActiveZoneSetInternal(DWORD wparam, Trace::ZoneWindow::InputMode mode) noexcept;
    void FlashZones() noexcept;

    winrt::com_ptr<IZoneWindowHost> m_host;
    HMONITOR m_monitor{};
    std::wstring m_uniqueId; // Parsed deviceId + resolution + virtualDesktopId
    wchar_t m_workArea[256]{};
    wil::unique_hwnd m_window{};
    HWND m_windowMoveSize{};
    bool m_drawHints{};
    bool m_flashMode{};
    bool m_dragEnabled{};
    winrt::com_ptr<IZoneSet> m_activeZoneSet;
    std::vector<winrt::com_ptr<IZoneSet>> m_zoneSets;
    winrt::com_ptr<IZone> m_highlightZone;
    WPARAM m_keyLast{};
    size_t m_keyCycle{};
    static const UINT m_showAnimationDuration = 200; // ms
    static const UINT m_flashDuration = 700; // ms
};

ZoneWindow::ZoneWindow(HINSTANCE hinstance)
{
    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = s_WndProc;
    wcex.hInstance = hinstance;
    wcex.lpszClassName = L"SuperFancyZones_ZoneWindow";
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassExW(&wcex);
}

bool ZoneWindow::Init(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, const std::wstring& uniqueId, bool flashZones)
{
    m_host.copy_from(host);

    MONITORINFO mi{};
    mi.cbSize = sizeof(mi);
    if (!GetMonitorInfoW(monitor, &mi))
    {
        return false;
    }

    m_monitor = monitor;
    const UINT dpi = GetDpiForMonitor(m_monitor);
    const Rect monitorRect(mi.rcMonitor);
    const Rect workAreaRect(mi.rcWork, dpi);
    StringCchPrintf(m_workArea, ARRAYSIZE(m_workArea), L"%d_%d", monitorRect.width(), monitorRect.height());

    m_uniqueId = uniqueId;
    LoadSettings();
    InitializeZoneSets(mi);

    m_window = wil::unique_hwnd{
        CreateWindowExW(WS_EX_TOOLWINDOW, L"SuperFancyZones_ZoneWindow", L"", WS_POPUP, workAreaRect.left(), workAreaRect.top(), workAreaRect.width(), workAreaRect.height(), nullptr, nullptr, hinstance, this)
    };

    if (!m_window)
    {
        return false;
    }

    MakeWindowTransparent(m_window.get());
    if (flashZones)
    {
        // Don't flash if the foreground window is in full screen mode
        RECT windowRect;
        if (!(GetWindowRect(GetForegroundWindow(), &windowRect) &&
              windowRect.left == mi.rcMonitor.left &&
              windowRect.top == mi.rcMonitor.top &&
              windowRect.right == mi.rcMonitor.right &&
              windowRect.bottom == mi.rcMonitor.bottom))
        {
            FlashZones();
        }
    }

    return true;
}

IFACEMETHODIMP ZoneWindow::MoveSizeEnter(HWND window, bool dragEnabled) noexcept
{
    if (m_windowMoveSize)
    {
        return E_INVALIDARG;
    }

    m_dragEnabled = dragEnabled;
    m_windowMoveSize = window;
    m_drawHints = true;
    m_highlightZone = nullptr;
    ShowZoneWindow();
    return S_OK;
}

IFACEMETHODIMP ZoneWindow::MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled) noexcept
{
    bool redraw = false;
    POINT ptClient = ptScreen;
    MapWindowPoints(nullptr, m_window.get(), &ptClient, 1);

    m_dragEnabled = dragEnabled;

    if (dragEnabled)
    {
        auto highlightZone = ZoneFromPoint(ptClient);
        redraw = (highlightZone != m_highlightZone);
        m_highlightZone = std::move(highlightZone);
    }
    else if (m_highlightZone)
    {
        m_highlightZone = nullptr;
        redraw = true;
    }

    if (redraw)
    {
        InvalidateRect(m_window.get(), nullptr, true);
    }
    return S_OK;
}

IFACEMETHODIMP ZoneWindow::MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept
{
    if (m_windowMoveSize != window)
    {
        return E_INVALIDARG;
    }

    if (m_activeZoneSet)
    {
        POINT ptClient = ptScreen;
        MapWindowPoints(nullptr, m_window.get(), &ptClient, 1);
        m_activeZoneSet->MoveWindowIntoZoneByPoint(window, m_window.get(), ptClient);

        SaveWindowProcessToZoneIndex(window);
    }
    Trace::ZoneWindow::MoveSizeEnd(m_activeZoneSet);

    HideZoneWindow();
    m_windowMoveSize = nullptr;
    return S_OK;
}

IFACEMETHODIMP ZoneWindow::MoveSizeCancel() noexcept
{
    HideZoneWindow();
    return S_OK;
}

IFACEMETHODIMP_(void)
ZoneWindow::MoveWindowIntoZoneByIndex(HWND window, int index) noexcept
{
    if (m_activeZoneSet)
    {
        m_activeZoneSet->MoveWindowIntoZoneByIndex(window, m_window.get(), index);
    }
}

IFACEMETHODIMP_(void)
ZoneWindow::MoveWindowIntoZoneByDirection(HWND window, DWORD vkCode) noexcept
{
    if (m_activeZoneSet)
    {
        m_activeZoneSet->MoveWindowIntoZoneByDirection(window, m_window.get(), vkCode);
        SaveWindowProcessToZoneIndex(window);
    }
}

IFACEMETHODIMP_(void)
ZoneWindow::CycleActiveZoneSet(DWORD wparam) noexcept
{
    CycleActiveZoneSetInternal(wparam, Trace::ZoneWindow::InputMode::Keyboard);

    if (m_windowMoveSize)
    {
        InvalidateRect(m_window.get(), nullptr, true);
    }
    else
    {
        FlashZones();
    }
}

IFACEMETHODIMP_(void)
ZoneWindow::SaveWindowProcessToZoneIndex(HWND window) noexcept
{
    if (m_activeZoneSet)
    {
        DWORD zoneIndex = static_cast<DWORD>(m_activeZoneSet->GetZoneIndexFromWindow(window));
        if (zoneIndex != -1)
        {
            OLECHAR* guidString;
            if (StringFromCLSID(m_activeZoneSet->Id(), &guidString) == S_OK)
            {
                JSONHelpers::FancyZonesDataInstance().SetAppLastZone(window, m_uniqueId, guidString, zoneIndex);
            }

            CoTaskMemFree(guidString);
        }
    }
}

#pragma region private
void ZoneWindow::ShowZoneWindow() noexcept
{
    if (m_window)
    {
        m_flashMode = false;

        UINT flags = SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE;

        HWND windowInsertAfter = m_windowMoveSize;
        if (windowInsertAfter == nullptr)
        {
            windowInsertAfter = HWND_TOPMOST;
        }

        SetWindowPos(m_window.get(), windowInsertAfter, 0, 0, 0, 0, flags);

        AnimateWindow(m_window.get(), m_showAnimationDuration, AW_BLEND);
        InvalidateRect(m_window.get(), nullptr, true);
    }
}

void ZoneWindow::HideZoneWindow() noexcept
{
    if (m_window)
    {
        ShowWindow(m_window.get(), SW_HIDE);
        m_keyLast = 0;
        m_windowMoveSize = nullptr;
        m_drawHints = false;
        m_highlightZone = nullptr;
    }
}

void ZoneWindow::LoadSettings() noexcept
{
    JSONHelpers::FancyZonesDataInstance().AddDevice(m_uniqueId);
}

void ZoneWindow::InitializeZoneSets(MONITORINFO const& mi) noexcept
{
    auto parent = m_host->GetParentZoneWindow(m_monitor);
    if (parent)
    {
        // Update device info with device info from parent virtual desktop (if empty).
        JSONHelpers::FancyZonesDataInstance().CloneDeviceInfo(parent->UniqueId(), m_uniqueId);
    }
    CalculateZoneSet();
}

void ZoneWindow::CalculateZoneSet() noexcept
{
    const auto& fancyZonesData = JSONHelpers::FancyZonesDataInstance();
    const auto deviceInfoData = fancyZonesData.FindDeviceInfo(m_uniqueId);
    const auto& activeDeviceId = fancyZonesData.GetActiveDeviceId();

    if (!activeDeviceId.empty() && activeDeviceId != m_uniqueId)
    {
        return;
    }

    if (!deviceInfoData.has_value())
    {
        return;
    }

    const auto& activeZoneSet = deviceInfoData->activeZoneSet;

    if (activeZoneSet.uuid.empty() || activeZoneSet.type == JSONHelpers::ZoneSetLayoutType::Blank)
    {
        return;
    }

    GUID zoneSetId;
    if (SUCCEEDED_LOG(CLSIDFromString(activeZoneSet.uuid.c_str(), &zoneSetId)))
    {
        auto zoneSet = MakeZoneSet(ZoneSetConfig(
            zoneSetId,
            activeZoneSet.type,
            m_monitor,
            m_workArea));
        MONITORINFO monitorInfo{};
        monitorInfo.cbSize = sizeof(monitorInfo);
        if (GetMonitorInfoW(m_monitor, &monitorInfo))
        {
            bool showSpacing = deviceInfoData->showSpacing;
            int spacing = showSpacing ? deviceInfoData->spacing : 0;
            int zoneCount = deviceInfoData->zoneCount;
            zoneSet->CalculateZones(monitorInfo, zoneCount, spacing);
            UpdateActiveZoneSet(zoneSet.get());
        }
    }
}

void ZoneWindow::UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept
{
    m_activeZoneSet.copy_from(zoneSet);

    if (m_activeZoneSet)
    {
        wil::unique_cotaskmem_string zoneSetId;
        if (SUCCEEDED_LOG(StringFromCLSID(m_activeZoneSet->Id(), &zoneSetId)))
        {
            JSONHelpers::ZoneSetData data{
                .uuid = zoneSetId.get(),
                .type = m_activeZoneSet->LayoutType()
            };
            JSONHelpers::FancyZonesDataInstance().SetActiveZoneSet(m_uniqueId, data);
        }
    }
}

LRESULT ZoneWindow::WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_NCDESTROY: {
        ::DefWindowProc(m_window.get(), message, wparam, lparam);
        SetWindowLongPtr(m_window.get(), GWLP_USERDATA, 0);
    }
    break;

    case WM_ERASEBKGND:
        return 1;

    case WM_PRINTCLIENT:
    case WM_PAINT: {
        PAINTSTRUCT ps;
        wil::unique_hdc hdc{ reinterpret_cast<HDC>(wparam) };
        if (!hdc)
        {
            hdc.reset(BeginPaint(m_window.get(), &ps));
        }

        OnPaint(hdc);

        if (wparam == 0)
        {
            EndPaint(m_window.get(), &ps);
        }

        hdc.release();
    }
    break;

    default:
    {
        return DefWindowProc(m_window.get(), message, wparam, lparam);
    }
    }
    return 0;
}

void ZoneWindow::OnPaint(wil::unique_hdc& hdc) noexcept
{
    RECT clientRect;
    GetClientRect(m_window.get(), &clientRect);

    wil::unique_hdc hdcMem;
    HPAINTBUFFER bufferedPaint = BeginBufferedPaint(hdc.get(), &clientRect, BPBF_TOPDOWNDIB, nullptr, &hdcMem);
    if (bufferedPaint)
    {
        ZoneWindowDrawUtils::DrawBackdrop(hdcMem, clientRect);
        if (m_activeZoneSet && m_host)
        {
            ZoneWindowDrawUtils::DrawActiveZoneSet(hdcMem, m_host->GetZoneHighlightColor(), m_host->GetZoneHighlightOpacity(), m_activeZoneSet->GetZones(), m_highlightZone, m_flashMode, m_drawHints);
        }

        EndBufferedPaint(bufferedPaint, TRUE);
    }
}

void ZoneWindow::OnKeyUp(WPARAM wparam) noexcept
{
    bool fRedraw = false;
    Trace::ZoneWindow::KeyUp(wparam);

    if ((wparam >= '0') && (wparam <= '9'))
    {
        CycleActiveZoneSetInternal(static_cast<DWORD>(wparam), Trace::ZoneWindow::InputMode::Keyboard);
        InvalidateRect(m_window.get(), nullptr, true);
    }
}

winrt::com_ptr<IZone> ZoneWindow::ZoneFromPoint(POINT pt) noexcept
{
    if (m_activeZoneSet)
    {
        return m_activeZoneSet->ZoneFromPoint(pt);
    }
    return nullptr;
}

void ZoneWindow::CycleActiveZoneSetInternal(DWORD wparam, Trace::ZoneWindow::InputMode mode) noexcept
{
    Trace::ZoneWindow::CycleActiveZoneSet(m_activeZoneSet, mode);
    if (m_keyLast != wparam)
    {
        m_keyCycle = 0;
    }

    m_keyLast = wparam;

    bool loopAround = true;
    size_t const val = static_cast<size_t>(wparam - L'0');
    size_t i = 0;
    for (auto zoneSet : m_zoneSets)
    {
        if (zoneSet->GetZones().size() == val)
        {
            if (i < m_keyCycle)
            {
                i++;
            }
            else
            {
                UpdateActiveZoneSet(zoneSet.get());
                loopAround = false;
                break;
            }
        }
    }

    if ((m_keyCycle > 0) && loopAround)
    {
        // Cycling through a non-empty group and hit the end
        m_keyCycle = 0;
        OnKeyUp(wparam);
    }
    else
    {
        m_keyCycle++;
    }

    if (m_host)
    {
        m_host->MoveWindowsOnActiveZoneSetChange();
    }
    m_highlightZone = nullptr;
}

void ZoneWindow::FlashZones() noexcept
{
    m_flashMode = true;

    ShowWindow(m_window.get(), SW_SHOWNA);
    std::thread([window = m_window.get()]() {
        AnimateWindow(window, m_flashDuration, AW_HIDE | AW_BLEND);
    }).detach();
}
#pragma endregion

LRESULT CALLBACK ZoneWindow::s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    auto thisRef = reinterpret_cast<ZoneWindow*>(GetWindowLongPtr(window, GWLP_USERDATA));
    if ((thisRef == nullptr) && (message == WM_CREATE))
    {
        auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
        thisRef = reinterpret_cast<ZoneWindow*>(createStruct->lpCreateParams);
        SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
    }

    return (thisRef != nullptr) ? thisRef->WndProc(message, wparam, lparam) :
                                  DefWindowProc(window, message, wparam, lparam);
}

winrt::com_ptr<IZoneWindow> MakeZoneWindow(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, const std::wstring& uniqueId, bool flashZones) noexcept
{
    auto self = winrt::make_self<ZoneWindow>(hinstance);
    if (self->Init(host, hinstance, monitor, uniqueId, flashZones))
    {
        return self;
    }

    return nullptr;
}
