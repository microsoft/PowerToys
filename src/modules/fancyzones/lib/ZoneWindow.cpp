#include "pch.h"
#include <common/dpi_aware.h>

#include <ShellScalingApi.h>

struct ZoneWindow : public winrt::implements<ZoneWindow, IZoneWindow>
{
public:
    ZoneWindow(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, PCWSTR deviceId, PCWSTR virtualDesktopId, bool flashZones);

    IFACEMETHODIMP MoveSizeEnter(HWND window, bool dragEnabled) noexcept;
    IFACEMETHODIMP MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled) noexcept;
    IFACEMETHODIMP MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept;
    IFACEMETHODIMP MoveSizeCancel() noexcept;
    IFACEMETHODIMP_(bool) IsDragEnabled() noexcept { return m_dragEnabled; }
    IFACEMETHODIMP_(void) MoveWindowIntoZoneByIndex(HWND window, int index) noexcept;
    IFACEMETHODIMP_(void) MoveWindowIntoZoneByDirection(HWND window, DWORD vkCode) noexcept;
    IFACEMETHODIMP_(void) CycleActiveZoneSet(DWORD vkCode) noexcept;
    IFACEMETHODIMP_(std::wstring) DeviceId() noexcept { return { m_deviceId.get() }; }
    IFACEMETHODIMP_(std::wstring) UniqueId() noexcept { return { m_uniqueId }; }
    IFACEMETHODIMP_(std::wstring) WorkAreaKey() noexcept { return { m_workArea }; }
    IFACEMETHODIMP_(void) SaveWindowProcessToZoneIndex(HWND window) noexcept;
    IFACEMETHODIMP_(IZoneSet*) ActiveZoneSet() noexcept { return m_activeZoneSet.get(); }

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

private:
    struct ColorSetting
    {
        BYTE fillAlpha{};
        COLORREF fill{};
        BYTE borderAlpha{};
        COLORREF border{};
        int thickness{};
    };

    void ShowZoneWindow() noexcept;
    void HideZoneWindow() noexcept;
    void InitializeId(PCWSTR deviceId, PCWSTR virtualDesktopId) noexcept;
    void LoadSettings() noexcept;
    void InitializeZoneSets(MONITORINFO const& mi) noexcept;
    void LoadZoneSetsFromRegistry() noexcept;
    void AddDefaultZoneSet(MONITORINFO const& mi) noexcept;
    void UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept;
    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    void DrawBackdrop(wil::unique_hdc& hdc, RECT const& clientRect) noexcept;
    void DrawZone(wil::unique_hdc& hdc, ColorSetting const& colorSetting, winrt::com_ptr<IZone> zone) noexcept;
    void DrawIndex(wil::unique_hdc& hdc, POINT offset, size_t index, int padding, int size, bool flipX, bool flipY, COLORREF colorFill);
    void DrawActiveZoneSet(wil::unique_hdc& hdc, RECT const& clientRect) noexcept;
    void OnPaint(wil::unique_hdc& hdc) noexcept;
    void OnKeyUp(WPARAM wparam) noexcept;
    winrt::com_ptr<IZone> ZoneFromPoint(POINT pt) noexcept;
    void ChooseDefaultActiveZoneSet() noexcept;
    bool IsOccluded(POINT pt, size_t index) noexcept;
    void CycleActiveZoneSetInternal(DWORD wparam, Trace::ZoneWindow::InputMode mode) noexcept;
    void FlashZones() noexcept;
    UINT GetDpiForMonitor() noexcept;

    winrt::com_ptr<IZoneWindowHost> m_host;
    HMONITOR m_monitor{};
    wchar_t m_uniqueId[256]{};  // Parsed deviceId + resolution + virtualDesktopId
    wchar_t m_workArea[256]{};
    wil::unique_cotaskmem_string m_deviceId{};
    wil::unique_hwnd m_window{};
    HWND m_windowMoveSize{};
    bool m_drawHints{};
    bool m_flashMode{};
    bool m_dragEnabled{};
    winrt::com_ptr<IZoneSet> m_activeZoneSet;
    GUID m_activeZoneSetId{};
    std::vector<winrt::com_ptr<IZoneSet>> m_zoneSets;
    winrt::com_ptr<IZone> m_highlightZone;
    WPARAM m_keyLast{};
    size_t m_keyCycle{};
    static const UINT m_showAnimationDuration = 200; // ms
    static const UINT m_flashDuration = 700; // ms
};

ZoneWindow::ZoneWindow(
    IZoneWindowHost* host,
    HINSTANCE hinstance,
    HMONITOR monitor,
    PCWSTR deviceId,
    PCWSTR virtualDesktopId,
    bool flashZones)
        : m_monitor(monitor)
{
    m_host.copy_from(host);

    MONITORINFO mi{};
    mi.cbSize = sizeof(mi);
    if (!GetMonitorInfoW(m_monitor, &mi))
    {
        return;
    }
    const UINT dpi = GetDpiForMonitor();
    const Rect monitorRect(mi.rcMonitor);
    const Rect workAreaRect(mi.rcWork, dpi);

    StringCchPrintf(m_workArea, ARRAYSIZE(m_workArea), L"%d_%d", monitorRect.width(), monitorRect.height());

    InitializeId(deviceId, virtualDesktopId);
    LoadSettings();
    InitializeZoneSets(mi);

    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = s_WndProc;
    wcex.hInstance = hinstance;
    wcex.lpszClassName = L"SuperFancyZones_ZoneWindow";
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassExW(&wcex);

    m_window = wil::unique_hwnd {
        CreateWindowExW(WS_EX_TOOLWINDOW, L"SuperFancyZones_ZoneWindow", L"", WS_POPUP,
                workAreaRect.left(), workAreaRect.top(), workAreaRect.width(), workAreaRect.height(),
                nullptr, nullptr, hinstance, this)
    };

    if (m_window)
    {
        MakeWindowTransparent(m_window.get());
        if (flashZones)
        {
            FlashZones();
        }
    }
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
        m_activeZoneSet->MoveSizeEnd(window, m_window.get(), ptClient);

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

IFACEMETHODIMP_(void) ZoneWindow::MoveWindowIntoZoneByIndex(HWND window, int index) noexcept
{
    if (m_activeZoneSet)
    {
        m_activeZoneSet->MoveWindowIntoZoneByIndex(window, m_window.get(), index);
    }
}

IFACEMETHODIMP_(void) ZoneWindow::MoveWindowIntoZoneByDirection(HWND window, DWORD vkCode) noexcept
{
    if (m_activeZoneSet)
    {
        m_activeZoneSet->MoveWindowIntoZoneByDirection(window, m_window.get(), vkCode);
        SaveWindowProcessToZoneIndex(window);
    }
}

IFACEMETHODIMP_(void) ZoneWindow::CycleActiveZoneSet(DWORD wparam) noexcept
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

IFACEMETHODIMP_(void) ZoneWindow::SaveWindowProcessToZoneIndex(HWND window) noexcept
{
    auto processPath = get_process_path(window);
    if (!processPath.empty())
    {
        DWORD zoneIndex = static_cast<DWORD>(m_activeZoneSet->GetZoneIndexFromWindow(window));
        if (zoneIndex != -1)
        {
            RegistryHelpers::SaveAppLastZone(window, processPath.data(), zoneIndex);
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

void ZoneWindow::InitializeId(PCWSTR deviceId, PCWSTR virtualDesktopId) noexcept
{
    SHStrDup(deviceId, &m_deviceId);

    MONITORINFOEXW mi;
    mi.cbSize = sizeof(mi);
    if (GetMonitorInfo(m_monitor, &mi))
    {
        wchar_t parsedId[256]{};
        ParseDeviceId(m_deviceId.get(), parsedId, 256);

        Rect const monitorRect(mi.rcMonitor);
        StringCchPrintf(m_uniqueId, ARRAYSIZE(m_uniqueId), L"%s_%d_%d_%s",
            parsedId, monitorRect.width(), monitorRect.height(), virtualDesktopId);
    }
}

void ZoneWindow::LoadSettings() noexcept
{
    wchar_t activeZoneSetId[256];
    RegistryHelpers::GetString(m_uniqueId, L"ActiveZoneSetId", activeZoneSetId, sizeof(activeZoneSetId));
    CLSIDFromString(activeZoneSetId, &m_activeZoneSetId);
}

void ZoneWindow::InitializeZoneSets(MONITORINFO const& mi) noexcept
{
    LoadZoneSetsFromRegistry();

    if (m_zoneSets.empty())
    {
        // Add a "maximize" zone as the only default layout.
        AddDefaultZoneSet(mi);
    }

    if (!m_activeZoneSet)
    {
        if (GUID id{ m_host->GetCurrentMonitorZoneSetId(m_monitor) }; id != GUID_NULL) {
            for (const auto& zoneSet : m_zoneSets) {
                if (id == zoneSet->Id()) {
                    UpdateActiveZoneSet(zoneSet.get());
                    break;
                }
            }
        }
        else {
            ChooseDefaultActiveZoneSet();
        }
    }
}

void ZoneWindow::LoadZoneSetsFromRegistry() noexcept
{
    wil::unique_hkey key{RegistryHelpers::OpenKey(m_workArea)};
    if (!key)
    {
        return;
    }

    ZoneSetPersistedData data{};
    DWORD dataSize = sizeof(data);
    wchar_t value[256]{};
    DWORD valueLength = ARRAYSIZE(value);
    DWORD i = 0;
    while (RegEnumValueW(key.get(), i++, value, &valueLength, nullptr, nullptr, reinterpret_cast<BYTE*>(&data), &dataSize) == ERROR_SUCCESS)
    {
        if (data.Version == VERSION_PERSISTEDDATA)
        {
            GUID zoneSetId;
            if (SUCCEEDED_LOG(CLSIDFromString(value, &zoneSetId)))
            {
                auto zoneSet = MakeZoneSet(ZoneSetConfig(
                    zoneSetId,
                    data.LayoutId,
                    m_monitor,
                    m_workArea));

                if (zoneSet)
                {
                    for (UINT j = 0; j < data.ZoneCount; j++)
                    {
                        zoneSet->AddZone(MakeZone(data.Zones[j]));
                    }

                    if (zoneSetId == m_activeZoneSetId)
                    {
                        UpdateActiveZoneSet(zoneSet.get());
                    }

                    m_zoneSets.emplace_back(std::move(zoneSet));
                }
            }
        }
        else
        {
            // Migrate from older settings format
        }

        valueLength = ARRAYSIZE(value);
        dataSize = sizeof(data);
    }
}

void ZoneWindow::AddDefaultZoneSet(MONITORINFO const& mi) noexcept
{
    GUID zoneSetId;
    if (SUCCEEDED_LOG(CoCreateGuid(&zoneSetId)))
    {
        if (auto zoneSet = MakeZoneSet(ZoneSetConfig(zoneSetId, 0, m_monitor, m_workArea)))
        {
            zoneSet->AddZone(MakeZone(mi.rcWork));

            m_zoneSets.emplace_back(zoneSet);
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
            RegistryHelpers::SetString(m_uniqueId, L"ActiveZoneSetId", zoneSetId.get());
        }
    }
}

LRESULT ZoneWindow::WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
        case WM_NCDESTROY:
        {
            ::DefWindowProc(m_window.get(), message, wparam, lparam);
            SetWindowLongPtr(m_window.get(), GWLP_USERDATA, 0);
        }
        break;

        case WM_ERASEBKGND:
            return 1;

        case WM_PRINTCLIENT:
        case WM_PAINT:
        {
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

void ZoneWindow::DrawBackdrop(wil::unique_hdc& hdc, RECT const& clientRect) noexcept
{
    FillRectARGB(hdc, &clientRect, 0, RGB(0, 0, 0), false);
}

void ZoneWindow::DrawZone(wil::unique_hdc& hdc, ColorSetting const& colorSetting, winrt::com_ptr<IZone> zone) noexcept
{
    RECT zoneRect = zone->GetZoneRect();
    if (colorSetting.borderAlpha > 0)
    {
        FillRectARGB(hdc, &zoneRect, colorSetting.borderAlpha, colorSetting.border, false);
        InflateRect(&zoneRect, colorSetting.thickness, colorSetting.thickness);
    }
    FillRectARGB(hdc, &zoneRect, colorSetting.fillAlpha, colorSetting.fill, false);

    if (m_flashMode)
    {
        return;
    }
    COLORREF const colorFill = RGB(255, 255, 255);

    size_t const index = zone->Id();
    int const padding = 5;
    int const size = 10;
    POINT offset = { zoneRect.left + padding, zoneRect.top + padding };
    if (!IsOccluded(offset, index))
    {
        DrawIndex(hdc, offset, index, padding, size, false, false, colorFill); // top left
        return;
    }

    offset.x = zoneRect.right - ((padding + size) * 3);
    if (!IsOccluded(offset, index))
    {
        DrawIndex(hdc, offset, index, padding, size, true, false, colorFill); // top right
        return;
    }

    offset.y = zoneRect.bottom - ((padding + size) * 3);
    if (!IsOccluded(offset, index))
    {
        DrawIndex(hdc, offset, index, padding, size, true, true, colorFill); // bottom right
        return;
    }

    offset.x = zoneRect.left + padding;
    DrawIndex(hdc, offset, index, padding, size, false, true, colorFill); // bottom left
}

void ZoneWindow::DrawIndex(wil::unique_hdc& hdc, POINT offset, size_t index, int padding, int size, bool flipX, bool flipY, COLORREF colorFill)
{
    RECT rect = { offset.x, offset.y, offset.x + size, offset.y + size };
    for (int y = 0; y < 3; y++)
    {
        for (int x = 0; x < 3; x++)
        {
            RECT useRect = rect;
            if (flipX)
            {
                if (x == 0) useRect.left += (size + padding + size + padding);
                else if (x == 2) useRect.left -= (size + padding + size + padding);
                useRect.right = useRect.left + size;
            }

            if (flipY)
            {
                if (y == 0) useRect.top += (size + padding + size + padding);
                else if (y == 2) useRect.top -= (size + padding + size + padding);
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

void ZoneWindow::DrawActiveZoneSet(wil::unique_hdc& hdc, RECT const& clientRect) noexcept
{
    if (m_activeZoneSet)
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
        ColorSetting const colorHints      { 225, RGB(81, 92, 107),   255, RGB(104, 118, 138), -2 };
        ColorSetting       colorViewer     { 225, 0,                  255, RGB(40, 50, 60),    -2 };
        ColorSetting       colorHighlight  { 225, 0,                  255, 0,                  -2 };
        ColorSetting const colorFlash      { 200, RGB(81, 92, 107),   200, RGB(104, 118, 138), -2 };

        auto zones = m_activeZoneSet->GetZones();
        const size_t maxColorIndex = min(size(zones) - 1, size(colors) - 1);
        size_t colorIndex = maxColorIndex;
        for (auto iter = zones.rbegin(); iter != zones.rend(); iter++)
        {
            winrt::com_ptr<IZone> zone = iter->try_as<IZone>();
            if (!zone)
            {
                continue;
            }

            if (zone != m_highlightZone)
            {
                if (m_flashMode)
                {
                    DrawZone(hdc, colorFlash, zone);
                }
                else if (m_drawHints)
                {
                    DrawZone(hdc, colorHints, zone);
                }
                {
                    colorViewer.fill = colors[colorIndex];
                    DrawZone(hdc, colorViewer, zone);
                }
            }
            colorIndex = colorIndex != 0 ? colorIndex - 1 : maxColorIndex;
        }

        if (m_highlightZone)
        {
            colorHighlight.fill = m_host->GetZoneHighlightColor();
            colorHighlight.border = RGB(
                max(0, GetRValue(colorHighlight.fill) - 25),
                max(0, GetGValue(colorHighlight.fill) - 25),
                max(0, GetBValue(colorHighlight.fill) - 25)
            );
            DrawZone(hdc, colorHighlight, m_highlightZone);
        }
    }
}

void ZoneWindow::OnPaint(wil::unique_hdc& hdc) noexcept
{
    RECT clientRect;
    GetClientRect(m_window.get(), &clientRect);

    wil::unique_hdc hdcMem;
    HPAINTBUFFER bufferedPaint = BeginBufferedPaint(hdc.get(), &clientRect, BPBF_TOPDOWNDIB, nullptr, &hdcMem);
    if (bufferedPaint)
    {
        DrawBackdrop(hdcMem, clientRect);
        DrawActiveZoneSet(hdcMem, clientRect);
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

void ZoneWindow::ChooseDefaultActiveZoneSet() noexcept
{
    if (!m_activeZoneSet)
    {
        auto zoneSet = m_zoneSets.at(0);
        UpdateActiveZoneSet(zoneSet.get());
    }
}

bool ZoneWindow::IsOccluded(POINT pt, size_t index) noexcept
{
    auto zones = m_activeZoneSet->GetZones();
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

    m_host->MoveWindowsOnActiveZoneSetChange();
    m_highlightZone = nullptr;
}

void ZoneWindow::FlashZones() noexcept
{
    m_flashMode = true;

    ShowWindow(m_window.get(), SW_SHOWNA);
    std::thread([window = m_window.get()]()
        {
            AnimateWindow(window, m_flashDuration, AW_HIDE | AW_BLEND);
        }).detach();
}

typedef BOOL(WINAPI *GetDpiForMonitorInternalFunc)(HMONITOR, UINT, UINT*, UINT*);
UINT ZoneWindow::GetDpiForMonitor() noexcept
{
    UINT dpi{};
    if (wil::unique_hmodule user32{ LoadLibrary(L"user32.dll") })
    {
        if (auto func = reinterpret_cast<GetDpiForMonitorInternalFunc>(GetProcAddress(user32.get(), "GetDpiForMonitorInternal")))
        {
            func(m_monitor, 0, &dpi, &dpi);
        }
    }

    if (dpi == 0)
    {
        if (wil::unique_hdc hdc{ GetDC(nullptr) })
        {
            dpi = GetDeviceCaps(hdc.get(), LOGPIXELSX);
        }
    }

    return (dpi == 0) ? DPIAware::DEFAULT_DPI : dpi;
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

winrt::com_ptr<IZoneWindow> MakeZoneWindow(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor,
    PCWSTR deviceId, PCWSTR virtualDesktopId, bool flashZones) noexcept
{
    return winrt::make_self<ZoneWindow>(host, hinstance, monitor, deviceId, virtualDesktopId, flashZones);
}
