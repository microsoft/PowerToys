#include "pch.h"
#include "WorkArea.h"

#include <common/logger/logger.h>

#include "FancyZonesData/AppliedLayouts.h"
#include "FancyZonesData/AppZoneHistory.h"
#include "ZonesOverlay.h"
#include "Settings.h"
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/VirtualDesktop.h>
#include <FancyZonesLib/WindowUtils.h>

// disabling warning 4458 - declaration of 'identifier' hides class member
// to avoid warnings from GDI files - can't add winRT directory to external code
// in the Cpp.Build.props
#pragma warning(push)
#pragma warning(disable : 4458)
#include <gdiplus.h>
#pragma warning(pop)

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t ToolWindowClassName[] = L"FancyZones_ZonesOverlay";
}

using namespace FancyZonesUtils;

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

WorkArea::WorkArea(HINSTANCE hinstance, const FancyZonesDataTypes::WorkAreaId& uniqueId, const FancyZonesUtils::Rect& workAreaRect) :
    m_uniqueId(uniqueId),
    m_workAreaRect(workAreaRect)
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

bool WorkArea::Snap(HWND window, const ZoneIndexSet& zones, bool updatePosition)
{
    if (!m_layout || zones.empty())
    {
        return false;
    }

    for (ZoneIndex zone : zones)
    {
        if (static_cast<size_t>(zone) >= m_layout->Zones().size())
        {
            return false;
        }
    }

    m_layoutWindows.Assign(window, zones);
    AppZoneHistory::instance().SetAppLastZones(window, m_uniqueId, m_layout->Id(), zones);

    if (updatePosition)
    {
        const auto rect = m_layout->GetCombinedZonesRect(zones);
        const auto adjustedRect = FancyZonesWindowUtils::AdjustRectForSizeWindowToRect(window, rect, m_window);
        FancyZonesWindowUtils::SaveWindowSizeAndOrigin(window);
        FancyZonesWindowUtils::SizeWindowToRect(window, adjustedRect);
    }

    return FancyZonesWindowProperties::StampZoneIndexProperty(window, zones);
}

bool WorkArea::Unsnap(HWND window)
{
    if (!m_layout)
    {
        return false;
    }
    
    m_layoutWindows.Dismiss(window);
    AppZoneHistory::instance().RemoveAppLastZone(window, m_uniqueId, m_layout->Id());
    FancyZonesWindowProperties::RemoveZoneIndexProperty(window);

    return true;
}

const GUID WorkArea::GetLayoutId() const noexcept
{
    if (m_layout)
    {
        return m_layout->Id();
    }

    return GUID{};
}

void WorkArea::ShowZones(const ZoneIndexSet& highlight, HWND draggedWindow/* = nullptr*/)
{
    if (m_layout && m_zonesOverlay)
    {
        SetWorkAreaWindowAsTopmost(draggedWindow);
        m_zonesOverlay->DrawActiveZoneSet(m_layout->Zones(), highlight, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
        m_zonesOverlay->Show();
    }
}

void WorkArea::HideZones()
{
    if (m_zonesOverlay)
    {
        m_zonesOverlay->Hide();
    }
}

void WorkArea::FlashZones()
{
    if (m_layout && m_zonesOverlay)
    {
        SetWorkAreaWindowAsTopmost(nullptr);
        m_zonesOverlay->DrawActiveZoneSet(m_layout->Zones(), {}, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
        m_zonesOverlay->Flash();
    }
}

void WorkArea::InitLayout()
{
    InitLayout({});

    if (m_window && m_layout)
    {
        m_zonesOverlay->DrawActiveZoneSet(m_layout->Zones(), {}, Colors::GetZoneColors(), FancyZonesSettings::settings().showZoneNumber);
    }
}

void WorkArea::UpdateWindowPositions()
{
    const auto& snappedWindows = m_layoutWindows.SnappedWindows();
    for (const auto& [window, zones] : snappedWindows)
    {
        Snap(window, zones, true);
    }
}

void WorkArea::CycleWindows(HWND window, bool reverse)
{
    m_layoutWindows.CycleWindows(window, reverse);
}

#pragma region private

bool WorkArea::InitWindow(HINSTANCE hinstance)
{
    m_window = windowPool.NewZonesOverlayWindow(m_workAreaRect, hinstance, this);
    if (!m_window)
    {
        Logger::error(L"No work area window");
        return false;
    }

    m_zonesOverlay = std::make_unique<ZonesOverlay>(m_window);
    return true;
}

void WorkArea::InitLayout(const FancyZonesDataTypes::WorkAreaId& parentUniqueId)
{
    Logger::info(L"Initialize layout on {}, work area rect = {}x{}", m_uniqueId.toString(), m_workAreaRect.width(), m_workAreaRect.height());

    const bool isLayoutAlreadyApplied = AppliedLayouts::instance().IsLayoutApplied(m_uniqueId);
    if (!isLayoutAlreadyApplied)
    {
        if (!AppliedLayouts::instance().CloneLayout(parentUniqueId, m_uniqueId))
        {
            AppliedLayouts::instance().ApplyDefaultLayout(m_uniqueId);
        }

        AppliedLayouts::instance().SaveData();
    }

    CalculateZoneSet();
}

void WorkArea::InitSnappedWindows()
{
    static bool updatePositionOnceOnStartFlag = true;
    Logger::info(L"Init work area {} windows, update positions = {}", m_uniqueId.toString(), updatePositionOnceOnStartFlag);

    for (const auto& window : VirtualDesktop::instance().GetWindowsFromCurrentDesktop())
    {
        auto indexes = FancyZonesWindowProperties::RetrieveZoneIndexProperty(window);
        if (indexes.size() == 0)
        {
            continue;
        }

        if (!m_uniqueId.monitorId.monitor) // one work area across monitors
        {
            Snap(window, indexes, updatePositionOnceOnStartFlag);
        }
        else
        {
            const auto monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
            if (monitor && m_uniqueId.monitorId.monitor == monitor)
            {
                // prioritize snapping on the current monitor if the window was snapped to several work areas
                Snap(window, indexes, updatePositionOnceOnStartFlag);
            }
            else
            {
                // if the window is not snapped on the current monitor, then check the others
                auto savedIndexes = AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_uniqueId, GetLayoutId());
                if (savedIndexes == indexes)
                {
                    Snap(window, indexes, updatePositionOnceOnStartFlag);
                }
            }
        }
    }

    updatePositionOnceOnStartFlag = false;
}

void WorkArea::CalculateZoneSet()
{
    const auto appliedLayout = AppliedLayouts::instance().GetDeviceLayout(m_uniqueId);
    if (!appliedLayout.has_value())
    {
        Logger::error(L"Layout wasn't applied. Can't init layout on work area {}x{}", m_workAreaRect.width(), m_workAreaRect.height());
        return;
    }

    m_layout = std::make_unique<Layout>(appliedLayout.value());
    m_layout->Init(m_workAreaRect, m_uniqueId.monitorId.monitor);
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

void WorkArea::SetWorkAreaWindowAsTopmost(HWND draggedWindow) noexcept
{
    if (!m_window)
    {
        return;
    }

    HWND windowInsertAfter = draggedWindow ? draggedWindow : HWND_TOPMOST;

    constexpr UINT flags = SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE;
    SetWindowPos(m_window, windowInsertAfter, 0, 0, 0, 0, flags);
}

#pragma endregion

LRESULT CALLBACK WorkArea::s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    auto thisRef = reinterpret_cast<WorkArea*>(GetWindowLongPtr(window, GWLP_USERDATA));
    if ((thisRef == nullptr) && (message == WM_CREATE))
    {
        auto createStruct = reinterpret_cast<LPCREATESTRUCT>(lparam);
        thisRef = static_cast<WorkArea*>(createStruct->lpCreateParams);
        SetWindowLongPtr(window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(thisRef));
    }

    return (thisRef != nullptr) ? thisRef->WndProc(message, wparam, lparam) :
                                  DefWindowProc(window, message, wparam, lparam);
}