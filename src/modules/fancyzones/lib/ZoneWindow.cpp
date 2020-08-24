#include "pch.h"

#include <common/common.h>

#include "FancyZonesData.h"
#include "FancyZonesDataTypes.h"
#include "ZoneWindow.h"
#include "ZoneWindowDrawing.h"
#include "trace.h"
#include "util.h"
#include "Settings.h"

#include <ShellScalingApi.h>
#include <mutex>
#include <fileapi.h>

#include <gdiplus.h>

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t ToolWindowClassName[] = L"SuperFancyZones_ZoneWindow";
}

using namespace FancyZonesUtils;

namespace ZoneWindowUtils
{
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

    std::wstring GenerateUniqueIdAllMonitorsArea(PCWSTR virtualDesktopId)
    {
        std::wstring result{ ZonedWindowProperties::MultiMonitorDeviceID };

        RECT combinedResolution = GetAllMonitorsCombinedRect<&MONITORINFO::rcMonitor>();

        result += L'_';
        result += std::to_wstring(combinedResolution.right - combinedResolution.left);
        result += L'_';
        result += std::to_wstring(combinedResolution.bottom - combinedResolution.top);
        result += L'_';
        result += virtualDesktopId;

        return result;
    }
}

struct ZoneWindow : public winrt::implements<ZoneWindow, IZoneWindow>
{
public:
    ZoneWindow(HINSTANCE hinstance);
    ~ZoneWindow();

    bool Init(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, const std::wstring& uniqueId, const std::wstring& parentUniqueId, bool flashZones);

    IFACEMETHODIMP MoveSizeEnter(HWND window) noexcept;
    IFACEMETHODIMP MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled, bool selectManyZones) noexcept;
    IFACEMETHODIMP MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndex(HWND window, size_t index) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndexSet(HWND window, const std::vector<size_t>& indexSet) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(void)
    CycleActiveZoneSet(DWORD vkCode) noexcept;
    IFACEMETHODIMP_(std::wstring)
    UniqueId() noexcept { return { m_uniqueId }; }
    IFACEMETHODIMP_(void)
    SaveWindowProcessToZoneIndex(HWND window) noexcept;
    IFACEMETHODIMP_(IZoneSet*)
    ActiveZoneSet() noexcept { return m_activeZoneSet.get(); }
    IFACEMETHODIMP_(void)
    ShowZoneWindow() noexcept;
    IFACEMETHODIMP_(void)
    HideZoneWindow() noexcept;
    IFACEMETHODIMP_(void)
    UpdateActiveZoneSet() noexcept;
    IFACEMETHODIMP_(void)
    ClearSelectedZones() noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

private:
    void InitializeZoneSets(const std::wstring& parentUniqueId) noexcept;
    void CalculateZoneSet() noexcept;
    void UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept;
    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    void OnPaint(wil::unique_hdc& hdc) noexcept;
    void OnKeyUp(WPARAM wparam) noexcept;
    std::vector<size_t> ZonesFromPoint(POINT pt) noexcept;
    void CycleActiveZoneSetInternal(DWORD wparam, Trace::ZoneWindow::InputMode mode) noexcept;
    void FlashZones() noexcept;

    winrt::com_ptr<IZoneWindowHost> m_host;
    HMONITOR m_monitor{};
    std::wstring m_uniqueId; // Parsed deviceId + resolution + virtualDesktopId
    wil::unique_hwnd m_window{}; // Hidden tool window used to represent current monitor desktop work area.
    HWND m_windowMoveSize{};
    bool m_drawHints{};
    bool m_flashMode{};
    winrt::com_ptr<IZoneSet> m_activeZoneSet;
    std::vector<winrt::com_ptr<IZoneSet>> m_zoneSets;
    std::vector<size_t> m_initialHighlightZone;
    std::vector<size_t> m_highlightZone;
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
    wcex.lpszClassName = NonLocalizable::ToolWindowClassName;
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassExW(&wcex);
}

ZoneWindow::~ZoneWindow()
{
}

bool ZoneWindow::Init(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, const std::wstring& uniqueId, const std::wstring& parentUniqueId, bool flashZones)
{
    m_host.copy_from(host);

    Rect workAreaRect;
    m_monitor = monitor;
    if (monitor)
    {
        MONITORINFO mi{};
        mi.cbSize = sizeof(mi);
        if (!GetMonitorInfoW(monitor, &mi))
        {
            return false;
        }
        const UINT dpi = GetDpiForMonitor(m_monitor);
        workAreaRect = Rect(mi.rcWork, dpi);
    }
    else
    {
        workAreaRect = GetAllMonitorsCombinedRect<&MONITORINFO::rcWork>();
    }

    m_uniqueId = uniqueId;
    InitializeZoneSets(parentUniqueId);

    m_window = wil::unique_hwnd{
        CreateWindowExW(WS_EX_TOOLWINDOW, NonLocalizable::ToolWindowClassName, L"", WS_POPUP, workAreaRect.left(), workAreaRect.top(), workAreaRect.width(), workAreaRect.height(), nullptr, nullptr, hinstance, this)
    };

    if (!m_window)
    {
        return false;
    }

    MakeWindowTransparent(m_window.get());

    // Ignore flashZones
    /*
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
    */

    return true;
}

IFACEMETHODIMP ZoneWindow::MoveSizeEnter(HWND window) noexcept
{
    m_windowMoveSize = window;
    m_drawHints = true;
    m_highlightZone = {};
    m_initialHighlightZone = {};
    ShowZoneWindow();
    return S_OK;
}

IFACEMETHODIMP ZoneWindow::MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled, bool selectManyZones) noexcept
{
    bool redraw = false;
    POINT ptClient = ptScreen;
    MapWindowPoints(nullptr, m_window.get(), &ptClient, 1);

    if (dragEnabled)
    {
        auto highlightZone = ZonesFromPoint(ptClient);

        if (selectManyZones)
        {
            if (m_initialHighlightZone.empty())
            {
                // first time
                m_initialHighlightZone = highlightZone;
            }
            else
            {
                std::vector<size_t> newHighlightZone;
                std::set_union(begin(highlightZone), end(highlightZone), begin(m_initialHighlightZone), end(m_initialHighlightZone), std::back_inserter(newHighlightZone));

                RECT boundingRect;
                bool boundingRectEmpty = true;
                auto zones = m_activeZoneSet->GetZones();

                for (size_t zoneId : newHighlightZone)
                {
                    RECT rect = zones[zoneId]->GetZoneRect();
                    if (boundingRectEmpty)
                    {
                        boundingRect = rect;
                        boundingRectEmpty = false;
                    }
                    else
                    {
                        boundingRect.left = min(boundingRect.left, rect.left);
                        boundingRect.top = min(boundingRect.top, rect.top);
                        boundingRect.right = max(boundingRect.right, rect.right);
                        boundingRect.bottom = max(boundingRect.bottom, rect.bottom);
                    }
                }

                highlightZone.clear();

                if (!boundingRectEmpty)
                {
                    for (size_t zoneId = 0; zoneId < zones.size(); zoneId++)
                    {
                        RECT rect = zones[zoneId]->GetZoneRect();
                        if (boundingRect.left <= rect.left && rect.right <= boundingRect.right &&
                            boundingRect.top <= rect.top && rect.bottom <= boundingRect.bottom)
                        {
                            highlightZone.push_back(zoneId);
                        }
                    }
                }
            }
        }
        else
        {
            m_initialHighlightZone = {};
        }

        redraw = (highlightZone != m_highlightZone);
        m_highlightZone = std::move(highlightZone);
    }
    else if (m_highlightZone.size())
    {
        m_highlightZone = {};
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
        m_activeZoneSet->MoveWindowIntoZoneByIndexSet(window, m_window.get(), m_highlightZone);

        auto windowInfo = FancyZonesUtils::GetFancyZonesWindowInfo(window);
        if (windowInfo.noVisibleOwner)
        {
            SaveWindowProcessToZoneIndex(window);
        }
    }
    Trace::ZoneWindow::MoveSizeEnd(m_activeZoneSet);

    HideZoneWindow();
    m_windowMoveSize = nullptr;
    return S_OK;
}

IFACEMETHODIMP_(void)
ZoneWindow::MoveWindowIntoZoneByIndex(HWND window, size_t index) noexcept
{
    MoveWindowIntoZoneByIndexSet(window, { index });
}

IFACEMETHODIMP_(void)
ZoneWindow::MoveWindowIntoZoneByIndexSet(HWND window, const std::vector<size_t>& indexSet) noexcept
{
    if (m_activeZoneSet)
    {
        m_activeZoneSet->MoveWindowIntoZoneByIndexSet(window, m_window.get(), indexSet);
    }
}

IFACEMETHODIMP_(bool)
ZoneWindow::MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept
{
    if (m_activeZoneSet)
    {
        if (m_activeZoneSet->MoveWindowIntoZoneByDirectionAndIndex(window, m_window.get(), vkCode, cycle))
        {
            auto windowInfo = FancyZonesUtils::GetFancyZonesWindowInfo(window);
            if (windowInfo.noVisibleOwner)
            {
                SaveWindowProcessToZoneIndex(window);
            }
            return true;
        }
    }
    return false;
}

IFACEMETHODIMP_(bool)
ZoneWindow::MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept
{
    if (m_activeZoneSet)
    {
        if (m_activeZoneSet->MoveWindowIntoZoneByDirectionAndPosition(window, m_window.get(), vkCode, cycle))
        {
            SaveWindowProcessToZoneIndex(window);
            return true;
        }
    }
    return false;
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
        auto zoneIndexSet = m_activeZoneSet->GetZoneIndexSetFromWindow(window);
        if (zoneIndexSet.size())
        {
            OLECHAR* guidString;
            if (StringFromCLSID(m_activeZoneSet->Id(), &guidString) == S_OK)
            {
                FancyZonesDataInstance().SetAppLastZones(window, m_uniqueId, guidString, zoneIndexSet);
            }

            CoTaskMemFree(guidString);
        }
    }
}

IFACEMETHODIMP_(void)
ZoneWindow::ShowZoneWindow() noexcept
{
    auto window = m_window.get();
    if (!window)
    {
        return;
    }

    m_flashMode = false;

    UINT flags = SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE;

    HWND windowInsertAfter = m_windowMoveSize;
    if (windowInsertAfter == nullptr)
    {
        windowInsertAfter = HWND_TOPMOST;
    }

    SetWindowPos(window, windowInsertAfter, 0, 0, 0, 0, flags);

    std::thread{ [this, strong_this{ get_strong() }]() {
        auto window = m_window.get();
        AnimateWindow(window, m_showAnimationDuration, AW_BLEND);
        InvalidateRect(window, nullptr, true);
        if (!m_host->InMoveSize())
        {
            HideZoneWindow();
        }
    } }.detach();
}

IFACEMETHODIMP_(void)
ZoneWindow::HideZoneWindow() noexcept
{
    if (m_window)
    {
        ShowWindow(m_window.get(), SW_HIDE);
        m_keyLast = 0;
        m_windowMoveSize = nullptr;
        m_drawHints = false;
        m_highlightZone = {};
    }
}

IFACEMETHODIMP_(void)
ZoneWindow::UpdateActiveZoneSet() noexcept
{
    CalculateZoneSet();
}

IFACEMETHODIMP_(void)
ZoneWindow::ClearSelectedZones() noexcept
{
    if (m_highlightZone.size())
    {
        m_highlightZone.clear();
        InvalidateRect(m_window.get(), nullptr, true);
    }
}

#pragma region private

void ZoneWindow::InitializeZoneSets(const std::wstring& parentUniqueId) noexcept
{
    // If there is not defined zone layout for this work area, created default entry.
    FancyZonesDataInstance().AddDevice(m_uniqueId);
    if (!parentUniqueId.empty())
    {
        FancyZonesDataInstance().CloneDeviceInfo(parentUniqueId, m_uniqueId);
    }
    CalculateZoneSet();
}

void ZoneWindow::CalculateZoneSet() noexcept
{
    const auto& fancyZonesData = FancyZonesDataInstance();
    const auto deviceInfoData = fancyZonesData.FindDeviceInfo(m_uniqueId);

    if (!deviceInfoData.has_value())
    {
        return;
    }

    const auto& activeZoneSet = deviceInfoData->activeZoneSet;

    if (activeZoneSet.uuid.empty() || activeZoneSet.type == FancyZonesDataTypes::ZoneSetLayoutType::Blank)
    {
        return;
    }

    GUID zoneSetId;
    if (SUCCEEDED_LOG(CLSIDFromString(activeZoneSet.uuid.c_str(), &zoneSetId)))
    {
        auto zoneSet = MakeZoneSet(ZoneSetConfig(
            zoneSetId,
            activeZoneSet.type,
            m_monitor));
        
        RECT workArea;
        if (m_monitor)
        {
            MONITORINFO monitorInfo{};
            monitorInfo.cbSize = sizeof(monitorInfo);
            if (GetMonitorInfoW(m_monitor, &monitorInfo))
            {
                workArea = monitorInfo.rcWork;
            }
            else
            {
                return;
            }
        }
        else
        {
            workArea = GetAllMonitorsCombinedRect<&MONITORINFO::rcWork>();
        }

        bool showSpacing = deviceInfoData->showSpacing;
        int spacing = showSpacing ? deviceInfoData->spacing : 0;
        int zoneCount = deviceInfoData->zoneCount;
        zoneSet->CalculateZones(workArea, zoneCount, spacing);
        UpdateActiveZoneSet(zoneSet.get());        
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
            FancyZonesDataTypes::ZoneSetData data{
                .uuid = zoneSetId.get(),
                .type = m_activeZoneSet->LayoutType()
            };
            FancyZonesDataInstance().SetActiveZoneSet(m_uniqueId, data);
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

void ZoneWindow::OnPaint(wil::unique_hdc& hdc) noexcept
{
    RECT clientRect;
    GetClientRect(m_window.get(), &clientRect);

    wil::unique_hdc hdcMem;
    HPAINTBUFFER bufferedPaint = BeginBufferedPaint(hdc.get(), &clientRect, BPBF_TOPDOWNDIB, nullptr, &hdcMem);
    if (bufferedPaint)
    {
        ZoneWindowDrawing::DrawBackdrop(hdcMem, clientRect);

        if (m_activeZoneSet && m_host)
        {
            ZoneWindowDrawing::DrawActiveZoneSet(hdcMem,
                                                   m_host->GetZoneColor(),
                                                   m_host->GetZoneBorderColor(),
                                                   m_host->GetZoneHighlightColor(),
                                                   m_host->GetZoneHighlightOpacity(),
                                                   m_activeZoneSet->GetZones(),
                                                   m_highlightZone,
                                                   m_flashMode,
                                                   m_drawHints);
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

std::vector<size_t> ZoneWindow::ZonesFromPoint(POINT pt) noexcept
{
    if (m_activeZoneSet)
    {
        return m_activeZoneSet->ZonesFromPoint(pt);
    }
    return {};
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
    m_highlightZone = {};
}

void ZoneWindow::FlashZones() noexcept
{
    // "Turning FLASHING_ZONE option off"
    if (true)
    {
        return;
    }

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

winrt::com_ptr<IZoneWindow> MakeZoneWindow(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, const std::wstring& uniqueId, const std::wstring& parentUniqueId, bool flashZones) noexcept
{
    auto self = winrt::make_self<ZoneWindow>(hinstance);
    if (self->Init(host, hinstance, monitor, uniqueId, parentUniqueId, flashZones))
    {
        return self;
    }

    return nullptr;
}
