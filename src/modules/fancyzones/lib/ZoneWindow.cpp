#include "pch.h"

#include <common/logger/logger.h>

#include "FancyZonesData.h"
#include "FancyZonesDataTypes.h"
#include "ZoneWindow.h"
#include "ZoneWindowDrawing.h"
#include "trace.h"
#include "util.h"
#include "on_thread_executor.h"
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

struct ZoneWindow : public winrt::implements<ZoneWindow, IZoneWindow>
{
public:
    ZoneWindow(HINSTANCE hinstance);
    ~ZoneWindow();

    bool Init(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, const std::wstring& uniqueId, const std::wstring& parentUniqueId);

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
    IFACEMETHODIMP_(bool)
    ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept;
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
    void OnKeyUp(WPARAM wparam) noexcept;
    std::vector<size_t> ZonesFromPoint(POINT pt) noexcept;
    void CycleActiveZoneSetInternal(DWORD wparam, Trace::ZoneWindow::InputMode mode) noexcept;

    winrt::com_ptr<IZoneWindowHost> m_host;
    HMONITOR m_monitor{};
    std::wstring m_uniqueId; // Parsed deviceId + resolution + virtualDesktopId
    wil::unique_hwnd m_window{}; // Hidden tool window used to represent current monitor desktop work area.
    HWND m_windowMoveSize{};
    winrt::com_ptr<IZoneSet> m_activeZoneSet;
    std::vector<winrt::com_ptr<IZoneSet>> m_zoneSets;
    std::vector<size_t> m_initialHighlightZone;
    std::vector<size_t> m_highlightZone;
    WPARAM m_keyLast{};
    size_t m_keyCycle{};
    static const UINT m_showAnimationDuration = 200; // ms
    static const UINT m_flashDuration = 700; // ms
    std::unique_ptr<ZoneWindowDrawing> m_zoneWindowDrawing;
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

bool ZoneWindow::Init(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, const std::wstring& uniqueId, const std::wstring& parentUniqueId)
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
        workAreaRect = Rect(mi.rcWork);
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
    // According to ShowWindow docs, we must call it with SW_SHOWNORMAL the first time
    ShowWindow(m_window.get(), SW_SHOWNORMAL);
    ShowWindow(m_window.get(), SW_HIDE);

    m_zoneWindowDrawing = std::make_unique<ZoneWindowDrawing>(m_window.get());

    return true;
}

IFACEMETHODIMP ZoneWindow::MoveSizeEnter(HWND window) noexcept
{
    m_windowMoveSize = window;
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
                highlightZone = m_activeZoneSet->GetCombinedZoneRange(m_initialHighlightZone, highlightZone);
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
        m_zoneWindowDrawing->DrawActiveZoneSet(m_activeZoneSet->GetZones(), m_highlightZone, m_host);
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

        if (FancyZonesUtils::HasNoVisibleOwner(window))
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
            if (FancyZonesUtils::HasNoVisibleOwner(window))
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

IFACEMETHODIMP_(bool)
ZoneWindow::ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept
{
    if (m_activeZoneSet)
    {
        if (m_activeZoneSet->ExtendWindowByDirectionAndPosition(window, m_window.get(), vkCode))
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

    UINT flags = SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE;

    HWND windowInsertAfter = m_windowMoveSize;
    if (windowInsertAfter == nullptr)
    {
        windowInsertAfter = HWND_TOPMOST;
    }

    SetWindowPos(window, windowInsertAfter, 0, 0, 0, 0, flags);
    m_zoneWindowDrawing->Show(m_showAnimationDuration);
    m_zoneWindowDrawing->DrawActiveZoneSet(m_activeZoneSet->GetZones(), m_highlightZone, m_host);
}

IFACEMETHODIMP_(void)
ZoneWindow::HideZoneWindow() noexcept
{
    if (m_window)
    {
        m_zoneWindowDrawing->Hide();
        m_keyLast = 0;
        m_windowMoveSize = nullptr;
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
        m_zoneWindowDrawing->DrawActiveZoneSet(m_activeZoneSet->GetZones(), m_highlightZone, m_host);
    }
}

#pragma region private

void ZoneWindow::InitializeZoneSets(const std::wstring& parentUniqueId) noexcept
{
    bool deviceAdded = FancyZonesDataInstance().AddDevice(m_uniqueId);
    // If the device has been added, check if it should inherit the parent's layout
    if (deviceAdded && !parentUniqueId.empty())
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

    if (activeZoneSet.uuid.empty())
    {
        return;
    }

    GUID zoneSetId;
    if (SUCCEEDED_LOG(CLSIDFromString(activeZoneSet.uuid.c_str(), &zoneSetId)))
    {
        int sensitivityRadius = deviceInfoData->sensitivityRadius;

        auto zoneSet = MakeZoneSet(ZoneSetConfig(
            zoneSetId,
            activeZoneSet.type,
            m_monitor,
            sensitivityRadius));

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

    case WM_PAINT:
    {
        m_zoneWindowDrawing->ForceRender();
        break;
    }

    default:
    {
        return DefWindowProc(m_window.get(), message, wparam, lparam);
    }
    }
    return 0;
}

void ZoneWindow::OnKeyUp(WPARAM wparam) noexcept
{
    bool fRedraw = false;
    Trace::ZoneWindow::KeyUp(wparam);

    if ((wparam >= '0') && (wparam <= '9'))
    {
        CycleActiveZoneSetInternal(static_cast<DWORD>(wparam), Trace::ZoneWindow::InputMode::Keyboard);
        m_zoneWindowDrawing->DrawActiveZoneSet(m_activeZoneSet->GetZones(), m_highlightZone, m_host);
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

winrt::com_ptr<IZoneWindow> MakeZoneWindow(IZoneWindowHost* host, HINSTANCE hinstance, HMONITOR monitor, const std::wstring& uniqueId, const std::wstring& parentUniqueId) noexcept
{
    auto self = winrt::make_self<ZoneWindow>(hinstance);
    if (self->Init(host, hinstance, monitor, uniqueId, parentUniqueId))
    {
        return self;
    }

    return nullptr;
}
