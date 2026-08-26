#pragma once

#include <common/hooks/WinHookEvent.h>

#include <algorithm>
#include <functional>
#include <initializer_list>
#include <unordered_set>

struct WinHookEvent;

interface __declspec(uuid("{50D3F0F5-736E-4186-BDF4-3D6BEE150C3A}")) IFancyZones : public IUnknown
{
    /**
     * Start and initialize FancyZones.
     */
    IFACEMETHOD_(void, Run)
    () = 0;
    /**
     * Stop FancyZones and do the clean up.
     */
    IFACEMETHOD_(void, Destroy)
    () = 0;
};

/**
 * Core FancyZones functionality.
 */
interface __declspec(uuid("{2CB37E8F-87E6-4AEC-B4B2-E0FDC873343F}")) IFancyZonesCallback : public IUnknown
{
    /**
     * Inform FancyZones that user has switched between virtual desktops.
     */
    IFACEMETHOD_(void, VirtualDesktopChanged)
    () = 0;
    /**
     * Callback from WinEventHook to FancyZones
     *
     * @param   data  Handle of window being moved or resized.
     */
    IFACEMETHOD_(void, HandleWinHookEvent)
    (const WinHookEvent* data) = 0;
    /**
     * Process keyboard event.
     *
     * @param   info Information about low level keyboard event.
     * @returns Boolean indicating if this event should be passed on further to other applications
     *          in event chain, or should it be suppressed.
     */
    IFACEMETHOD_(bool, OnKeyDown)
    (PKBDLLHOOKSTRUCT info) = 0;
    /**
     * Process keyboard key-up event.
     *
     * @param   info Information about low level keyboard event.
     * @returns Boolean indicating if this event should be suppressed.
     */
    IFACEMETHOD_(bool, OnKeyUp)
    (PKBDLLHOOKSTRUCT info) = 0;
};

namespace MonitorRotation
{
    LONG ScaleCoordinate(LONG value, LONG sourceStart, LONG sourceSize, LONG targetStart, LONG targetSize) noexcept;
    RECT MapRectBetweenMonitorWorkAreas(const RECT& windowRect, const RECT& sourceWorkArea, const RECT& targetWorkArea) noexcept;
    size_t GetRotatedMonitorIndex(size_t sourceIndex, size_t monitorCount, bool reverse) noexcept;

    class KeyState
    {
    public:
        void Update(DWORD vkCode, bool isDown)
        {
            if (isDown)
            {
                m_pressedKeys.insert(vkCode);
            }
            else
            {
                m_pressedKeys.erase(vkCode);
            }
        }

        bool IsDown(DWORD vkCode) const noexcept
        {
            return m_pressedKeys.contains(vkCode);
        }

        bool IsAnyDown(std::initializer_list<DWORD> keys) const noexcept
        {
            return std::ranges::any_of(keys, [this](DWORD key) { return IsDown(key); });
        }

        bool Consume(DWORD vkCode)
        {
            return m_consumedKeys.insert(vkCode).second;
        }

        bool ReleaseWasConsumed(DWORD vkCode)
        {
            return m_consumedKeys.erase(vkCode) != 0;
        }

        void Reset() noexcept
        {
            m_pressedKeys.clear();
            m_consumedKeys.clear();
        }

    private:
        std::unordered_set<DWORD> m_pressedKeys;
        std::unordered_set<DWORD> m_consumedKeys;
    };
}

winrt::com_ptr<IFancyZones> MakeFancyZones(HINSTANCE hinstance, std::function<void()> disableCallback) noexcept;
