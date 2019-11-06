#include "pch.h"
#include <common/dpi_aware.h>

#include <ShellScalingApi.h>

struct ZoneWindow : public winrt::implements<ZoneWindow, IZoneWindow>
{
public:
    ZoneWindow(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, PCWSTR deviceId, PCWSTR virtualDesktopId, bool flashZones);

    IFACEMETHODIMP ShowZoneWindow(bool activate, bool fadeIn) noexcept;
    IFACEMETHODIMP HideZoneWindow() noexcept;
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

    void InitializeId(PCWSTR deviceId, PCWSTR virtualDesktopId) noexcept;
    void LoadSettings() noexcept;
    void InitializeZoneSets() noexcept;
    void LoadZoneSetsFromRegistry() noexcept;
    winrt::com_ptr<IZoneSet> AddZoneSet(ZoneSetLayout layout, int numZones, int paddingOuter, int paddingInner) noexcept;
    void MakeActiveZoneSetCustom() noexcept;
    void UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept;
    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    void OnLButtonDown(LPARAM lparam) noexcept;
    void OnLButtonUp(LPARAM lparam) noexcept;
    void OnRButtonUp(LPARAM lparam) noexcept;
    void OnMouseMove(LPARAM lparam) noexcept;
    void DrawBackdrop(wil::unique_hdc& hdc, RECT const& clientRect) noexcept;
    void DrawGridLines(wil::unique_hdc& hdc, RECT const& clientRect) noexcept;
    void DrawZone(wil::unique_hdc& hdc, ColorSetting const& colorSetting, winrt::com_ptr<IZone> zone) noexcept;
    void DrawIndex(wil::unique_hdc& hdc, POINT offset, size_t index, int padding, int size, bool flipX, bool flipY, COLORREF colorFill);
    void DrawActiveZoneSet(wil::unique_hdc& hdc, RECT const& clientRect) noexcept;
    void DrawZoneBuilder(wil::unique_hdc& hdc, RECT const& clientRect) noexcept;
    void DrawSwitchButtons(wil::unique_hdc& hdc, RECT const& clientRect) noexcept;
    void OnPaint(wil::unique_hdc& hdc) noexcept;
    void UpdateGrid(int stepColumns, int stepRows) noexcept;
    void UpdateGridMargins(int inc) noexcept;
    void EnterEditorMode() noexcept;
    void ExitEditorMode() noexcept;
    void OnKeyUp(WPARAM wparam) noexcept;
    winrt::com_ptr<IZone> ZoneFromPoint(POINT pt) noexcept;
    void ChooseDefaultActiveZoneSet() noexcept;
    bool IsOccluded(POINT pt, size_t index) noexcept;
    void CycleActiveZoneSetInternal(DWORD wparam, Trace::ZoneWindow::InputMode mode) noexcept;
    void FlashZones() noexcept;
    int GetSwitchButtonIndexFromPoint(POINT ptClient) noexcept;
    UINT GetDpiForMonitor() noexcept;

    winrt::com_ptr<IZoneWindowHost> m_host;
    HMONITOR m_monitor{};
    wchar_t m_uniqueId[256]{};  // Parsed deviceId + resolution + virtualDesktopId
    wchar_t m_workArea[256]{};
    wil::unique_cotaskmem_string m_deviceId{};
    wil::unique_hwnd m_window{};
    HWND m_windowMoveSize{};
    bool m_buttonDown{};
    bool m_drawHints{};
    bool m_editorMode{};
    bool m_flashMode{};
    bool m_dragEnabled{};
    POINT m_ptDown{};
    POINT m_ptLast{};
    winrt::com_ptr<IZoneSet> m_activeZoneSet;
    GUID m_activeZoneSetId{};
    std::vector<winrt::com_ptr<IZoneSet>> m_zoneSets;
    winrt::com_ptr<IZone> m_highlightZone;
    WPARAM m_keyLast{};
    size_t m_keyCycle{};
    int m_gridWidth{};
    int m_gridHeight{};
    int m_gridRows{};
    int m_gridColumns{};
    int m_switchButtonWidth = 50;
    int m_switchButtonPadding = 5;
    int m_switchButtonHover = -1;
    SIZE m_gridMargins{};
    RECT m_zoneBuilder{};
    RECT m_switchButtonContainerRect{};
    Trace::ZoneWindow::EditorModeActivity m_editorModeActivity;
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
    InitializeZoneSets();

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
        UpdateGrid(0, 0);
        if (flashZones)
        {
            FlashZones();
        }
    }
}

IFACEMETHODIMP ZoneWindow::ShowZoneWindow(bool activate, bool fadeIn) noexcept
{
    if (!m_window)
    {
        return E_FAIL;
    }

    m_flashMode = false;

    UINT flags = SWP_NOSIZE | SWP_NOMOVE;
    if (!activate)
    {
        WI_SetFlag(flags, SWP_NOACTIVATE);
    }

    if (!fadeIn)
    {
        WI_SetFlag(flags, SWP_SHOWWINDOW);
    }

    HWND windowInsertAfter = m_windowMoveSize;
    if (windowInsertAfter == nullptr)
    {
        windowInsertAfter = HWND_TOPMOST;
    }

    SetWindowPos(m_window.get(), windowInsertAfter, 0, 0, 0, 0, flags);

    if (fadeIn)
    {
        AnimateWindow(m_window.get(), m_showAnimationDuration, AW_BLEND);
        InvalidateRect(m_window.get(), nullptr, true);
    }
    return S_OK;
}

IFACEMETHODIMP ZoneWindow::HideZoneWindow() noexcept
{
    if (!m_window)
    {
        return E_FAIL;
    }

    if (m_editorMode)
    {
        ExitEditorMode();
    }

    ShowWindow(m_window.get(), SW_HIDE);
    m_keyLast = 0;
    m_windowMoveSize = nullptr;
    m_drawHints = false;
    m_highlightZone = nullptr;
    m_editorMode = false;
    return S_OK;
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
    ShowZoneWindow(false /*activate*/, true /*fadeIn*/);
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

#pragma region private
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

    RegistryHelpers::GetValue<SIZE>(m_uniqueId, L"GridMargins", &m_gridMargins, sizeof(m_gridMargins));
}

void ZoneWindow::InitializeZoneSets() noexcept
{
    LoadZoneSetsFromRegistry();
    if (m_zoneSets.empty())
    {
        // Add a "maximize" zone as the only default layout.
        AddZoneSet(ZoneSetLayout::Grid, 1, 0, 0);
    }

    if (!m_activeZoneSet)
    {
        ChooseDefaultActiveZoneSet();
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
                    m_workArea,
                    data.Layout,
                    0,
                    static_cast<int>(data.PaddingInner),
                    static_cast<int>(data.PaddingOuter)));

                if (zoneSet)
                {
                    for (UINT j = 0; j < data.ZoneCount; j++)
                    {
                        zoneSet->AddZone(MakeZone(data.Zones[j]), false);
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

winrt::com_ptr<IZoneSet> ZoneWindow::AddZoneSet(ZoneSetLayout layout, int numZones, int paddingOuter, int paddingInner) noexcept
{
    GUID zoneSetId;
    if (SUCCEEDED_LOG(CoCreateGuid(&zoneSetId)))
    {
        if (auto zoneSet = MakeZoneSet(ZoneSetConfig(zoneSetId, 0, m_monitor, m_workArea, layout, numZones, paddingOuter, paddingInner)))
        {
            m_zoneSets.emplace_back(zoneSet);
            return zoneSet;
        }
    }
    return nullptr;
}

void ZoneWindow::MakeActiveZoneSetCustom() noexcept
{
    if (m_activeZoneSet)
    {
        if (auto customZoneSet = m_activeZoneSet->MakeCustomClone())
        {
            UpdateActiveZoneSet(customZoneSet.get());
            m_zoneSets.emplace_back(customZoneSet);
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

        case WM_LBUTTONDOWN:
            OnLButtonDown(lparam);
            break;

        case WM_LBUTTONUP:
            OnLButtonUp(lparam);
            break;

        case WM_RBUTTONUP:
            OnRButtonUp(lparam);
            break;

        case WM_MOUSEMOVE:
            OnMouseMove(lparam);
            break;

        case WM_KEYUP:
            OnKeyUp(wparam);
            break;

        default:
        {
            return DefWindowProc(m_window.get(), message, wparam, lparam);
        }
    }
    return 0;
}

void ZoneWindow::OnLButtonDown(LPARAM lparam) noexcept
{
    m_buttonDown = true;
    m_ptDown = { GET_X_LPARAM(lparam), GET_Y_LPARAM(lparam) };
}

void ZoneWindow::OnLButtonUp(LPARAM lparam) noexcept
{
    POINT const ptClient = { GET_X_LPARAM(lparam), GET_Y_LPARAM(lparam) };
    if (m_buttonDown && m_activeZoneSet)
    {
        if (m_editorMode)
        {
            bool const ctrl = GetAsyncKeyState(VK_CONTROL) & 0x8000;
            if (ctrl)
            {
                auto zone = ZoneFromPoint(ptClient);
                if (zone)
                {
                    m_activeZoneSet->RemoveZone(zone);

                    int const padding = m_activeZoneSet->GetInnerPadding();
                    RECT const zoneRect = zone->GetZoneRect();
                    int const zoneRectWidthHalf = ((zoneRect.right - zoneRect.left) / 2) - padding;
                    RECT rectLeft = zoneRect;
                    rectLeft.right = rectLeft.left + zoneRectWidthHalf;
                    m_activeZoneSet->AddZone(MakeZone(rectLeft), false);

                    RECT rectRight = zoneRect;
                    rectRight.left = rectLeft.right + padding;
                    m_activeZoneSet->AddZone(MakeZone(rectRight), false);

                    m_activeZoneSet->Save();
                }
            }
            else if (m_activeZoneSet && !IsRectEmpty(&m_zoneBuilder))
            {
                m_activeZoneSet->AddZone(MakeZone(m_zoneBuilder), true);
            }
        }
        else if (!m_flashMode && !m_editorMode && !m_drawHints)
        {
            if (PtInRect(&m_switchButtonContainerRect, ptClient))
            {
                auto switchButtonIndex = GetSwitchButtonIndexFromPoint(ptClient);
                if (switchButtonIndex != -1)
                {
                    CycleActiveZoneSetInternal('0' + switchButtonIndex, Trace::ZoneWindow::InputMode::Mouse);
                }
            }
            else
            {
                if (auto zone = ZoneFromPoint(ptClient))
                {
                    m_activeZoneSet->MoveZoneToFront(zone);
                    m_activeZoneSet->Save();
                }
            }
        }
    }

    m_zoneBuilder = {};
    m_buttonDown = false;
    InvalidateRect(m_window.get(), nullptr, true);
}

void ZoneWindow::OnRButtonUp(LPARAM lparam) noexcept
{
    POINT const ptClient = { GET_X_LPARAM(lparam), GET_Y_LPARAM(lparam) };
    if (m_activeZoneSet)
    {
        if (m_editorMode)
        {
            if (bool const ctrl = GetAsyncKeyState(VK_CONTROL) & 0x8000)
            {
                if (auto zone = ZoneFromPoint(ptClient))
                {
                    m_activeZoneSet->RemoveZone(zone);

                    int const padding = m_activeZoneSet->GetInnerPadding();
                    RECT const zoneRect = zone->GetZoneRect();
                    int const zoneRectHeightHalf = ((zoneRect.bottom - zoneRect.top) / 2) - padding;
                    RECT rectTop = zoneRect;
                    rectTop.bottom = rectTop.top + zoneRectHeightHalf;
                    m_activeZoneSet->AddZone(MakeZone(rectTop), false);

                    RECT rectBottom = zoneRect;
                    rectBottom.top = rectTop.bottom + padding;
                    m_activeZoneSet->AddZone(MakeZone(rectBottom), false);

                    m_activeZoneSet->Save();
                }
            }
            else if (auto zone = ZoneFromPoint(ptClient))
            {
                m_activeZoneSet->RemoveZone(zone);
                m_activeZoneSet->Save();
            }
        }
        else if (auto zone = ZoneFromPoint(ptClient))
        {
            m_activeZoneSet->MoveZoneToBack(zone);
            m_activeZoneSet->Save();
        }
    }
    InvalidateRect(m_window.get(), nullptr, true);
}

void ZoneWindow::OnMouseMove(LPARAM lparam) noexcept
{
    int const oldHover = m_switchButtonHover;
    m_switchButtonHover = -1;

    POINT const ptClient = { GET_X_LPARAM(lparam), GET_Y_LPARAM(lparam) };
    if (m_buttonDown && m_editorMode && ((ptClient.x != m_ptLast.x) || (ptClient.y != m_ptLast.y)))
    {
        RECT start;
        int const startColumn = max(0, min(m_gridColumns, ((m_ptDown.x - m_gridMargins.cx) / m_gridWidth)));
        int const startRow = max(0, min(m_gridRows, ((m_ptDown.y  - m_gridMargins.cy) / m_gridHeight)));
        start.left = startColumn * m_gridWidth;
        start.top = startRow * m_gridHeight;
        start.right = start.left + m_gridWidth;
        start.bottom = start.top + m_gridHeight;
        OffsetRect(&start, m_gridMargins.cx, m_gridMargins.cy);

        RECT current;
        int const currentColumn = max(0, min(m_gridColumns, ((ptClient.x - m_gridMargins.cx) / m_gridWidth)));
        int const currentRow = max(0, min(m_gridRows, ((ptClient.y - m_gridMargins.cy) / m_gridHeight)));
        current.left = currentColumn * m_gridWidth;
        current.top = currentRow * m_gridHeight;
        current.right = current.left + m_gridWidth;
        current.bottom = current.top + m_gridHeight;
        OffsetRect(&current, m_gridMargins.cx, m_gridMargins.cy);

        RECT invalidateRect = m_zoneBuilder;
        POINT const last = {
             m_gridMargins.cx + ((m_ptLast.x - m_gridMargins.cx) / m_gridWidth),
             m_gridMargins.cy + ((m_ptLast.y - m_gridMargins.cy) / m_gridHeight)
        };

        m_ptLast = ptClient;
        UnionRect(&m_zoneBuilder, &start, &current);
        UnionRect(&invalidateRect, &invalidateRect, &m_zoneBuilder);

        if ((current.left != last.x) || (current.top != last.y))
        {
            InvalidateRect(m_window.get(), &invalidateRect, true);
        }
    }
    else if (!m_flashMode && !m_editorMode && !m_drawHints && PtInRect(&m_switchButtonContainerRect, ptClient))
    {
        m_switchButtonHover = GetSwitchButtonIndexFromPoint(ptClient);
    }

    if (oldHover != m_switchButtonHover)
    {
        InvalidateRect(m_window.get(), &m_switchButtonContainerRect, true);
    }
}

void ZoneWindow::DrawBackdrop(wil::unique_hdc& hdc, RECT const& clientRect) noexcept
{
    if (m_windowMoveSize || m_flashMode)
    {
        FillRectARGB(hdc, &clientRect, 0, RGB(0, 0, 0), false);
    }
    else
    {
        FillRectARGB(hdc, &clientRect, 225, RGB(0, 0, 0), false);
    }
}

void ZoneWindow::DrawGridLines(wil::unique_hdc& hdc, RECT const& clientRect) noexcept
{
    if (m_editorMode)
    {
        COLORREF const color = RGB(225, 225, 225);

        wil::unique_hpen pen{ CreatePen(PS_SOLID, 1, color) };
        wil::unique_select_object oldPen{ SelectObject(hdc.get(), pen.get()) };
        for (int i = 0; i <= m_gridRows; i++)
        {
            int const y = m_gridMargins.cy + (i * m_gridHeight);
            MoveToEx(hdc.get(), m_gridMargins.cx, y, nullptr);
            LineTo(hdc.get(), clientRect.right - m_gridMargins.cx, y);
        }

        for (int i = 0; i <= m_gridColumns; i++)
        {
            int const x = m_gridMargins.cx + (i * m_gridWidth);
            MoveToEx(hdc.get(), x, m_gridMargins.cy, nullptr);
            LineTo(hdc.get(), x, clientRect.bottom - m_gridMargins.cy);
        }
    }
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
        ColorSetting const colorEditorMode { 240, RGB(100, 100, 100), 255, RGB(50, 50, 50),    -5 };
        ColorSetting       colorViewer     { 225, 0,                  255, RGB(40, 50, 60),    -2 };
        ColorSetting       colorHighlight  { 225, 0,                  255, 0,                  -2 };
        ColorSetting const colorFlash      { 200, RGB(81, 92, 107),   200, RGB(104, 118, 138), -2 };

        auto zones = m_activeZoneSet->GetZones();
        size_t colorIndex = zones.size() - 1;
        for (auto iter = zones.rbegin(); iter != zones.rend(); iter++)
        {
            if (winrt::com_ptr<IZone> zone = iter->try_as<IZone>())
            {
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
                    else if (m_editorMode)
                    {
                        DrawZone(hdc, colorEditorMode, zone);
                    }
                    else
                    {
                        colorViewer.fill = colors[colorIndex];
                        DrawZone(hdc, colorViewer, zone);
                    }
                }
                colorIndex--;
            }
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

void ZoneWindow::DrawZoneBuilder(wil::unique_hdc& hdc, RECT const& clientRect) noexcept
{
    if (m_editorMode && m_buttonDown)
    {
        COLORREF const colorDrag = RGB(255, 255, 255);
        FillRectARGB(hdc, &m_zoneBuilder, 255, colorDrag, false);
    }
}

void ZoneWindow::DrawSwitchButtons(wil::unique_hdc& hdc, RECT const& clientRect) noexcept
{
    if (!m_editorMode && !m_drawHints && !m_flashMode)
    {
        Rect const rect(clientRect);

        int const numButtons = 9;
        int const containerRectWidth = (m_switchButtonWidth * numButtons) + (m_switchButtonPadding * numButtons) + m_switchButtonPadding;
        int const containerRectHeight = 42;

        m_switchButtonContainerRect = { (rect.width() / 2) - (containerRectWidth / 2), 0, (rect.width() / 2) + (containerRectWidth / 2), containerRectHeight };

        COLORREF const switchButtonContainerColor = RGB(50, 50, 50);
        BYTE const switchButtonContainerAlpha = 150;
        FillRectARGB(hdc, &m_switchButtonContainerRect, switchButtonContainerAlpha, switchButtonContainerColor, true);

        COLORREF const fillColor = RGB(128, 128, 128);
        COLORREF const hoverColor = RGB(255, 255, 255);
        COLORREF const activeColor = RGB(0, 128, 0);
        COLORREF const activeHoverColor = RGB(0, 200, 0);

        size_t activeZoneCount = 0;
        if (m_activeZoneSet)
        {
            activeZoneCount = m_activeZoneSet->GetZones().size();
        }

        int x = m_switchButtonContainerRect.left + m_switchButtonPadding;
        for (UINT i = 1; i < 10; i++)
        {
            POINT const offset = { x, 5 };
            int const padding = 1;
            int const size = 10;

            bool const active = activeZoneCount == i;
            bool const hover = i == m_switchButtonHover;

            COLORREF color = fillColor;
            if (active && hover)
            {
                color = activeHoverColor;
            }
            else if (active)
            {
                color = activeColor;
            }
            else if (hover)
            {
                color = hoverColor;
            }

            DrawIndex(hdc, offset, i, padding, size, false /*flipX*/, false /*flipY*/, color);
            x += m_switchButtonWidth + m_switchButtonPadding;
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
        DrawGridLines(hdcMem, clientRect);
        DrawActiveZoneSet(hdcMem, clientRect);
        DrawZoneBuilder(hdcMem, clientRect);
        DrawSwitchButtons(hdcMem, clientRect);
        EndBufferedPaint(bufferedPaint, TRUE);
    }
}

void ZoneWindow::UpdateGrid(int stepColumns, int stepRows) noexcept
{
    bool const shift = GetAsyncKeyState(VK_SHIFT) & 0x8000;
    bool const control = GetAsyncKeyState(VK_CONTROL) & 0x8000;

    RECT clientRect;
    ::GetClientRect(m_window.get(), &clientRect);
    InflateRect(&clientRect, -m_gridMargins.cx, -m_gridMargins.cy);

    Rect gridRect(clientRect);
    if (control || (!stepColumns && !stepRows))
    {
        // Reset
        m_gridColumns = (gridRect.width() / 50) + 1;
        m_gridRows = (gridRect.height() / 50) + 1;
    }
    else
    {
        stepColumns = stepColumns * (shift ? 5 : 1);
        stepRows = stepRows * (shift ? 5 : 1);

        m_gridColumns = max(1, m_gridColumns + stepColumns);
        m_gridRows = max(1, m_gridRows + stepRows);
    }

    m_gridWidth = gridRect.width() / m_gridColumns;
    m_gridHeight = gridRect.height() / m_gridRows;
}

void ZoneWindow::UpdateGridMargins(int inc) noexcept
{
    bool const shift = GetAsyncKeyState(VK_SHIFT) & 0x8000;
    bool const control = GetAsyncKeyState(VK_CONTROL) & 0x8000;
    if (control)
    {
        m_gridMargins.cx = 0;
        m_gridMargins.cy = 0;
    }
    else
    {
        inc = inc * (shift ? 5 : 1);
        m_gridMargins.cx = max(0, m_gridMargins.cx + inc);
        m_gridMargins.cy = max(0, m_gridMargins.cy + inc);
    }
    UpdateGrid(0, 0);

    RegistryHelpers::SetValue<SIZE>(m_uniqueId, L"GridMargins", m_gridMargins, sizeof(m_gridMargins));
}

void ZoneWindow::EnterEditorMode() noexcept
{
    m_editorModeActivity.Start();
    MakeActiveZoneSetCustom();
    m_editorMode = true;
}

void ZoneWindow::ExitEditorMode() noexcept
{
    m_editorMode = false;
    if (m_activeZoneSet)
    {
        m_activeZoneSet->Save();
    }
    m_editorModeActivity.Stop(m_activeZoneSet);
}

void ZoneWindow::OnKeyUp(WPARAM wparam) noexcept
{
    bool fRedraw = false;
    Trace::ZoneWindow::KeyUp(wparam, m_editorMode);

    if ((wparam >= '0') && (wparam<= '9'))
    {
        CycleActiveZoneSetInternal(static_cast<DWORD>(wparam), Trace::ZoneWindow::InputMode::Keyboard);
    }
    else
    {
        switch (wparam)
        {
            case VK_DELETE:
            case 'd':
            case 'D':
            {
                // Delete active zone set
                for (auto iter = m_zoneSets.begin(); iter != m_zoneSets.end(); iter++)
                {
                    if (iter->get() == m_activeZoneSet.get())
                    {
                        RegistryHelpers::DeleteZoneSet(m_workArea, m_activeZoneSet->Id());
                        m_zoneSets.erase(iter);
                        m_activeZoneSet = nullptr;
                        break;
                    }
                }
            }
            break;

            case 'r':
            case 'R':
            {
                // Reset zone sets for current work area
                m_zoneSets.clear();
                m_activeZoneSet = nullptr;
                RegistryHelpers::DeleteAllZoneSets(m_workArea);
                InitializeZoneSets();
            }
            break;

            case 'e':
            case 'E':
            {
                // Toggle editor mode
                m_editorMode ? ExitEditorMode() : EnterEditorMode();
            }
            break;

            case 'c':
            case 'C':
            {
                // Create a custom zone
                if (auto zoneSet = AddZoneSet(ZoneSetLayout::Custom, 0, 0, 0))
                {
                    UpdateActiveZoneSet(zoneSet.get());
                }
            }
            break;

            case VK_LEFT: UpdateGrid(-1, 0); break;
            case VK_RIGHT: UpdateGrid(1, 0); break;

            case VK_UP: UpdateGrid(0, 1); break;
            case VK_DOWN: UpdateGrid(0, -1); break;

            case VK_PRIOR: UpdateGridMargins(10); break;
            case VK_NEXT: UpdateGridMargins(-10); break;

            case VK_ESCAPE: m_host->ToggleZoneViewers(); break;
        }
    }
    InvalidateRect(m_window.get(), nullptr, true);
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
    MONITORINFO mi{};
    mi.cbSize = sizeof(mi);
    if (GetMonitorInfoW(m_monitor, &mi))
    {
        Rect const monitorRect(mi.rcMonitor);

        if ((monitorRect.width() == 3840) && (monitorRect.height() == 2160))
        {
            // For 4k screens, pick a layout with 5 zones in focus mode as the default.
            winrt::com_ptr<IZoneSet> zoneSetBest;
            for (auto zoneSet : m_zoneSets)
            {
                auto zones = zoneSet->GetZones();
                if (zones.size() == 5)
                {
                    if (!zoneSetBest)
                    {
                        zoneSetBest = zoneSet;
                    }
                    else if (zoneSet->GetLayout() == ZoneSetLayout::Focus)
                    {
                        zoneSetBest = zoneSet;
                        break;
                    }
                }
            }

            if (zoneSetBest)
            {
                UpdateActiveZoneSet(zoneSetBest.get());
            }
        }
        else if (monitorRect.aspectRatio() < 40)
        {
            // Ultrawide, prefer 3 columns
            winrt::com_ptr<IZoneSet> zoneSetBest;
            for (auto zoneSet : m_zoneSets)
            {
                auto zones = zoneSet->GetZones();
                if (zones.size() == 3)
                {
                    if (!zoneSetBest)
                    {
                        zoneSetBest = zoneSet;
                    }
                    else if (zoneSet->GetLayout() == ZoneSetLayout::Row)
                    {
                        zoneSetBest = zoneSet;
                        break;
                    }
                }
            }

            if (zoneSetBest)
            {
                UpdateActiveZoneSet(zoneSetBest.get());
            }
        }
    }

    if (!m_activeZoneSet)
    {
        // Couldn't find a ZoneSet to use so just use the first one.
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
    if (!m_editorMode)
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

int ZoneWindow::GetSwitchButtonIndexFromPoint(POINT ptClient) noexcept
{
    auto const switchButtonIndex = ((ptClient.x - m_switchButtonContainerRect.left) / (m_switchButtonWidth + m_switchButtonPadding)) + 1;
    return ((switchButtonIndex > 0) && (switchButtonIndex < 10)) ? switchButtonIndex : -1;
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
