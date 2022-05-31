#include "pch.h"
#include "WorkArea.h"

#include <common/logger/call_tracer.h>
#include <common/logger/logger.h>

#include "FancyZonesData/AppliedLayouts.h"
#include "FancyZonesData/AppZoneHistory.h"
#include "FancyZonesDataTypes.h"
#include "SettingsObserver.h"
#include "ZonesOverlay.h"
#include "trace.h"
#include "on_thread_executor.h"
#include "Settings.h"
#include <FancyZonesLib/WindowUtils.h>

#include <ShellScalingApi.h>
#include <mutex>
#include <fileapi.h>

#include <gdiplus.h>

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t ToolWindowClassName[] = L"FancyZones_ZonesOverlay";
}

using namespace FancyZonesUtils;

struct WorkArea;

namespace
{
    // The reason for using this class is the need to call ShowWindow(window, SW_SHOWNORMAL); on each
    // newly created window for it to be displayed properly. The call sometimes has side effects when
    // a fullscreen app is running, and happens when the resolution change event is triggered
    // (e.g. when running some games).
    // This class will serve as a pool of windows for which this call was already done.
    class WindowPool
    {
        std::vector<HWND> m_pool;
        std::mutex m_mutex;

        HWND ExtractWindow()
        {
            std::unique_lock lock(m_mutex);

            if (m_pool.empty())
            {
                return NULL;
            }

            HWND window = m_pool.back();
            m_pool.pop_back();
            return window;
        }

    public:

        HWND NewZonesOverlayWindow(Rect position, HINSTANCE hinstance, WorkArea* owner)
        {
            HWND windowFromPool = ExtractWindow();
            if (windowFromPool == NULL)
            {
                HWND window = CreateWindowExW(WS_EX_TOOLWINDOW, NonLocalizable::ToolWindowClassName, L"", WS_POPUP, position.left(), position.top(), position.width(), position.height(), nullptr, nullptr, hinstance, owner);
                Logger::info("Creating new ZonesOverlay window, hWnd = {}", (void*)window);
                FancyZonesWindowUtils::MakeWindowTransparent(window);

                // According to ShowWindow docs, we must call it with SW_SHOWNORMAL the first time
                ShowWindow(window, SW_SHOWNORMAL);
                ShowWindow(window, SW_HIDE);
                return window;
            }
            else
            {
                Logger::info("Reusing ZonesOverlay window from pool, hWnd = {}", (void*)windowFromPool);
                SetWindowLongPtrW(windowFromPool, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(owner));
                MoveWindow(windowFromPool, position.left(), position.top(), position.width(), position.height(), TRUE);
                return windowFromPool;
            }
        }

        void FreeZonesOverlayWindow(HWND window)
        {
            Logger::info("Freeing ZonesOverlay window into pool, hWnd = {}", (void*)window);
            SetWindowLongPtrW(window, GWLP_USERDATA, 0);
            ShowWindow(window, SW_HIDE);

            std::unique_lock lock(m_mutex);
            m_pool.push_back(window);
        }

        ~WindowPool()
        {
            for (HWND window : m_pool)
            {
                DestroyWindow(window);
            }
        }
    };

    WindowPool windowPool;
}

struct WorkArea : public winrt::implements<WorkArea, IWorkArea>
{
public:
    WorkArea(HINSTANCE hinstance);
    ~WorkArea();

    bool Init(HINSTANCE hinstance, HMONITOR monitor, const FancyZonesDataTypes::DeviceIdData& uniqueId, const FancyZonesDataTypes::DeviceIdData& parentUniqueId);

    IFACEMETHODIMP MoveSizeEnter(HWND window) noexcept;
    IFACEMETHODIMP MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled, bool selectManyZones) noexcept;
    IFACEMETHODIMP MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndex(HWND window, ZoneIndex index) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, bool suppressMove = false) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(bool)
    ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept;
    IFACEMETHODIMP_(FancyZonesDataTypes::DeviceIdData)
    UniqueId() const noexcept { return { m_uniqueId }; }
    IFACEMETHODIMP_(void)
    SaveWindowProcessToZoneIndex(HWND window) noexcept;
    IFACEMETHODIMP_(IZoneSet*)
    ZoneSet() const noexcept { return m_zoneSet.get(); }
    IFACEMETHODIMP_(ZoneIndexSet)
    GetWindowZoneIndexes(HWND window) const noexcept;
    IFACEMETHODIMP_(void)
    ShowZonesOverlay() noexcept;
    IFACEMETHODIMP_(void)
    HideZonesOverlay() noexcept;
    IFACEMETHODIMP_(void)
    UpdateActiveZoneSet() noexcept;
    IFACEMETHODIMP_(void)
    CycleTabs(HWND window, bool reverse) noexcept;
    IFACEMETHODIMP_(void)
    ClearSelectedZones() noexcept;
    IFACEMETHODIMP_(void)
    FlashZones() noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

private:
    void InitializeZoneSets(const FancyZonesDataTypes::DeviceIdData& parentUniqueId) noexcept;
    void CalculateZoneSet(OverlappingZonesAlgorithm overlappingAlgorithm) noexcept;
    void UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept;
    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    ZoneIndexSet ZonesFromPoint(POINT pt) noexcept;
    void SetAsTopmostWindow() noexcept;

    HMONITOR m_monitor{};
    FancyZonesDataTypes::DeviceIdData m_uniqueId;
    HWND m_window{}; // Hidden tool window used to represent current monitor desktop work area.
    HWND m_windowMoveSize{};
    winrt::com_ptr<IZoneSet> m_zoneSet;
    ZoneIndexSet m_initialHighlightZone;
    ZoneIndexSet m_highlightZone;
    WPARAM m_keyLast{};
    size_t m_keyCycle{};
    std::unique_ptr<ZonesOverlay> m_zonesOverlay;
};

WorkArea::WorkArea(HINSTANCE hinstance)
{
    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = s_WndProc;
    wcex.hInstance = hinstance;
    wcex.lpszClassName = NonLocalizable::ToolWindowClassName;
    wcex.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    RegisterClassExW(&wcex);
}

WorkArea::~WorkArea()
{
    windowPool.FreeZonesOverlayWindow(m_window);
}

bool WorkArea::Init(HINSTANCE hinstance, HMONITOR monitor, const FancyZonesDataTypes::DeviceIdData& uniqueId, const FancyZonesDataTypes::DeviceIdData& parentUniqueId)
{
    Rect workAreaRect;
    m_monitor = monitor;
    if (monitor)
    {
        MONITORINFO mi{};
        mi.cbSize = sizeof(mi);
        if (!GetMonitorInfoW(monitor, &mi))
        {
            Logger::error(L"GetMonitorInfo failed on work area initialization");
            return false;
        }
        workAreaRect = Rect(mi.rcWork);
    }
    else
    {
        workAreaRect = GetAllMonitorsCombinedRect<&MONITORINFO::rcWork>();
    }

    m_uniqueId = uniqueId;
    InitializeZoneSets(parentUniqueId);

    m_window = windowPool.NewZonesOverlayWindow(workAreaRect, hinstance, this);

    if (!m_window)
    {
        Logger::error(L"No work area window");
        return false;
    }

    m_zonesOverlay = std::make_unique<ZonesOverlay>(m_window);

    return true;
}

IFACEMETHODIMP WorkArea::MoveSizeEnter(HWND window) noexcept
{
    m_windowMoveSize = window;
    m_highlightZone = {};
    m_initialHighlightZone = {};
    ShowZonesOverlay();
    Trace::WorkArea::MoveOrResizeStarted(m_zoneSet);
    return S_OK;
}

IFACEMETHODIMP WorkArea::MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled, bool selectManyZones) noexcept
{
    bool redraw = false;
    POINT ptClient = ptScreen;
    MapWindowPoints(nullptr, m_window, &ptClient, 1);

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
                highlightZone = m_zoneSet->GetCombinedZoneRange(m_initialHighlightZone, highlightZone);
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
        m_zonesOverlay->DrawActiveZoneSet(m_zoneSet->GetZones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
    }

    return S_OK;
}

IFACEMETHODIMP WorkArea::MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept
{
    if (m_windowMoveSize != window)
    {
        return E_INVALIDARG;
    }

    if (m_zoneSet)
    {
        POINT ptClient = ptScreen;
        MapWindowPoints(nullptr, m_window, &ptClient, 1);
        m_zoneSet->MoveWindowIntoZoneByIndexSet(window, m_window, m_highlightZone);

        if (!FancyZonesWindowUtils::HasVisibleOwner(window))
        {
            SaveWindowProcessToZoneIndex(window);
        }
    }
    Trace::WorkArea::MoveOrResizeEnd(m_zoneSet);

    HideZonesOverlay();
    m_windowMoveSize = nullptr;
    return S_OK;
}

IFACEMETHODIMP_(void)
WorkArea::MoveWindowIntoZoneByIndex(HWND window, ZoneIndex index) noexcept
{
    MoveWindowIntoZoneByIndexSet(window, { index });
}

IFACEMETHODIMP_(void)
WorkArea::MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, bool suppressMove) noexcept
{
    if (m_zoneSet)
    {
        m_zoneSet->MoveWindowIntoZoneByIndexSet(window, m_window, indexSet, suppressMove);
    }
}

IFACEMETHODIMP_(bool)
WorkArea::MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept
{
    if (m_zoneSet)
    {
        if (m_zoneSet->MoveWindowIntoZoneByDirectionAndIndex(window, m_window, vkCode, cycle))
        {
            if (!FancyZonesWindowUtils::HasVisibleOwner(window))
            {
                SaveWindowProcessToZoneIndex(window);
            }
            return true;
        }
    }
    return false;
}

IFACEMETHODIMP_(bool)
WorkArea::MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept
{
    if (m_zoneSet)
    {
        if (m_zoneSet->MoveWindowIntoZoneByDirectionAndPosition(window, m_window, vkCode, cycle))
        {
            SaveWindowProcessToZoneIndex(window);
            return true;
        }
    }
    return false;
}

IFACEMETHODIMP_(bool)
WorkArea::ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept
{
    if (m_zoneSet)
    {
        if (m_zoneSet->ExtendWindowByDirectionAndPosition(window, m_window, vkCode))
        {
            SaveWindowProcessToZoneIndex(window);
            return true;
        }
    }
    return false;
}

IFACEMETHODIMP_(void)
WorkArea::SaveWindowProcessToZoneIndex(HWND window) noexcept
{
    if (m_zoneSet)
    {
        auto zoneIndexSet = m_zoneSet->GetZoneIndexSetFromWindow(window);
        if (zoneIndexSet.size())
        {
            OLECHAR* guidString;
            if (StringFromCLSID(m_zoneSet->Id(), &guidString) == S_OK)
            {
                AppZoneHistory::instance().SetAppLastZones(window, m_uniqueId, guidString, zoneIndexSet);
            }

            CoTaskMemFree(guidString);
        }
    }
}

IFACEMETHODIMP_(ZoneIndexSet)
WorkArea::GetWindowZoneIndexes(HWND window) const noexcept
{
    if (m_zoneSet)
    {
        wil::unique_cotaskmem_string zoneSetId;
        if (SUCCEEDED(StringFromCLSID(m_zoneSet->Id(), &zoneSetId)))
        {
            return AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_uniqueId, zoneSetId.get());
        }
        else
        {
            Logger::error(L"Failed to convert to string layout GUID on the requested work area");
        }
    }
    else
    {
        Logger::error(L"No layout initialized on the requested work area");
    }

    return {};
}

IFACEMETHODIMP_(void)
WorkArea::ShowZonesOverlay() noexcept
{
    if (m_window)
    {
        SetAsTopmostWindow();
        m_zonesOverlay->DrawActiveZoneSet(m_zoneSet->GetZones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
        m_zonesOverlay->Show();
    }
}

IFACEMETHODIMP_(void)
WorkArea::HideZonesOverlay() noexcept
{
    if (m_window)
    {
        m_zonesOverlay->Hide();
        m_keyLast = 0;
        m_windowMoveSize = nullptr;
        m_highlightZone = {};
    }
}

IFACEMETHODIMP_(void)
WorkArea::UpdateActiveZoneSet() noexcept
{
    bool isLayoutAlreadyApplied = AppliedLayouts::instance().IsLayoutApplied(m_uniqueId);
    if (!isLayoutAlreadyApplied)
    {
        AppliedLayouts::instance().ApplyDefaultLayout(m_uniqueId);
    }

    CalculateZoneSet(FancyZonesSettings::settings().overlappingZonesAlgorithm);
    if (m_window && m_zoneSet)
    {
        m_highlightZone.clear();
        m_zonesOverlay->DrawActiveZoneSet(m_zoneSet->GetZones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
    }
}

IFACEMETHODIMP_(void)
WorkArea::CycleTabs(HWND window, bool reverse) noexcept
{
    if (m_zoneSet)
    {
        m_zoneSet->CycleTabs(window, reverse);
    }
}

IFACEMETHODIMP_(void)
WorkArea::ClearSelectedZones() noexcept
{
    if (m_highlightZone.size())
    {
        m_highlightZone.clear();
        m_zonesOverlay->DrawActiveZoneSet(m_zoneSet->GetZones(), m_highlightZone, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
    }
}

IFACEMETHODIMP_(void)
WorkArea::FlashZones() noexcept
{
    if (m_window)
    {
        SetAsTopmostWindow();
        m_zonesOverlay->DrawActiveZoneSet(m_zoneSet->GetZones(), {}, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
        m_zonesOverlay->Flash();
    }
}

#pragma region private

void WorkArea::InitializeZoneSets(const FancyZonesDataTypes::DeviceIdData& parentUniqueId) noexcept
{
    wil::unique_cotaskmem_string virtualDesktopId;
    if (SUCCEEDED(StringFromCLSID(m_uniqueId.virtualDesktopId, &virtualDesktopId)))
    {
        Logger::debug(L"Initialize layout on the virtual desktop {}", virtualDesktopId.get());
    }
    
    bool isLayoutAlreadyApplied = AppliedLayouts::instance().IsLayoutApplied(m_uniqueId);
    if (!isLayoutAlreadyApplied)
    {
        if (parentUniqueId.virtualDesktopId != GUID_NULL)
        {
            AppliedLayouts::instance().CloneLayout(parentUniqueId, m_uniqueId);
        }
        else
        {
            AppliedLayouts::instance().ApplyDefaultLayout(m_uniqueId);
        }
    }
    
    CalculateZoneSet(FancyZonesSettings::settings().overlappingZonesAlgorithm);
}

void WorkArea::CalculateZoneSet(OverlappingZonesAlgorithm overlappingAlgorithm) noexcept
{
    const auto appliedLayout = AppliedLayouts::instance().GetDeviceLayout(m_uniqueId);
    if (!appliedLayout.has_value())
    {
        Logger::error(L"Layout wasn't applied. Can't init zone set");
        return;
    }

    auto zoneSet = MakeZoneSet(ZoneSetConfig(
        appliedLayout->uuid,
        appliedLayout->type,
        m_monitor,
        appliedLayout->sensitivityRadius,
        overlappingAlgorithm));

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
            Logger::error(L"CalculateZoneSet: GetMonitorInfo failed");
            return;
        }
    }
    else
    {
        workArea = GetAllMonitorsCombinedRect<&MONITORINFO::rcWork>();
    }

    bool showSpacing = appliedLayout->showSpacing;
    int spacing = showSpacing ? appliedLayout->spacing : 0;
    int zoneCount = appliedLayout->zoneCount;

    zoneSet->CalculateZones(workArea, zoneCount, spacing);
    UpdateActiveZoneSet(zoneSet.get());
}

void WorkArea::UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept
{
    m_zoneSet.copy_from(zoneSet);
}

LRESULT WorkArea::WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    switch (message)
    {
    case WM_NCDESTROY:
    {
        ::DefWindowProc(m_window, message, wparam, lparam);
        SetWindowLongPtr(m_window, GWLP_USERDATA, 0);
    }
    break;

    case WM_ERASEBKGND:
        return 1;

    default:
    {
        return DefWindowProc(m_window, message, wparam, lparam);
    }
    }
    return 0;
}

ZoneIndexSet WorkArea::ZonesFromPoint(POINT pt) noexcept
{
    if (m_zoneSet)
    {
        return m_zoneSet->ZonesFromPoint(pt);
    }
    return {};
}

void WorkArea::SetAsTopmostWindow() noexcept
{
    if (!m_window)
    {
        return;
    }

    UINT flags = SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE;

    HWND windowInsertAfter = m_windowMoveSize;
    if (windowInsertAfter == nullptr)
    {
        windowInsertAfter = HWND_TOPMOST;
    }

    SetWindowPos(m_window, windowInsertAfter, 0, 0, 0, 0, flags);
}

#pragma endregion

LRESULT CALLBACK WorkArea::s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    auto thisRef = reinterpret_cast<WorkArea*>(GetWindowLongPtr(window, GWLP_USERDATA));
    if ((thisRef == nullptr) && (message == WM_CREATE))
    {
        auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
        thisRef = reinterpret_cast<WorkArea*>(createStruct->lpCreateParams);
        SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
    }

    return (thisRef != nullptr) ? thisRef->WndProc(message, wparam, lparam) :
                                  DefWindowProc(window, message, wparam, lparam);
}

winrt::com_ptr<IWorkArea> MakeWorkArea(HINSTANCE hinstance, HMONITOR monitor, const FancyZonesDataTypes::DeviceIdData& uniqueId, const FancyZonesDataTypes::DeviceIdData& parentUniqueId) noexcept
{
    auto self = winrt::make_self<WorkArea>(hinstance);
    if (self->Init(hinstance, monitor, uniqueId, parentUniqueId))
    {
        return self;
    }

    return nullptr;
}
