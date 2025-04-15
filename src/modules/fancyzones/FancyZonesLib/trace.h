#pragma once

#include <common/Telemetry/TraceBase.h>

struct Settings;
class Layout;
class LayoutAssignedWindows;

class Trace : public telemetry::TraceBase
{
public:
    class FancyZones : public telemetry::TraceBase
    {
    public:
        static void EnableFancyZones(bool enabled) noexcept;
        static void OnKeyDown(DWORD vkCode, bool win, bool control, bool inMoveSize) noexcept;
        static void DataChanged() noexcept;
        static void EditorLaunched(int value) noexcept;
        static void Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept;
        static void QuickLayoutSwitched(bool shortcutUsed) noexcept;
        static void SnapNewWindowIntoZone(Layout* activeLayout, const LayoutAssignedWindows& layoutWindows) noexcept;
        static void KeyboardSnapWindowToZone(Layout* activeLayout, const LayoutAssignedWindows& layoutWindows) noexcept;
    };

    static void SettingsTelemetry(const Settings& settings) noexcept;
    static void VirtualDesktopChanged() noexcept;

    class WorkArea : public telemetry::TraceBase
    {
    public:
        enum class InputMode
        {
            Keyboard,
            Mouse
        };

        static void KeyUp(WPARAM wparam) noexcept;
        static void MoveOrResizeStarted(_In_opt_ Layout* activeLayout, const LayoutAssignedWindows& layoutWindows) noexcept;
        static void MoveOrResizeEnd(_In_opt_ Layout* activeLayout, const LayoutAssignedWindows& layoutWindows) noexcept;
        static void CycleActiveZoneSet(_In_opt_ Layout* activeLayout, const LayoutAssignedWindows& layoutWindows, InputMode mode) noexcept;
    };
};
