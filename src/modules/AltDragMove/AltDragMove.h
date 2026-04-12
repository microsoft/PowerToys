#pragma once

#include <windows.h>

/// Low-level mouse hook that intercepts Alt+Left-click-drag to reposition
/// the window under the cursor.  All hook state is kept in a singleton so
/// that the static WndProc / hook callback can reach it.
class AltDragMove
{
public:
    static AltDragMove& instance();

    void Start();
    void Stop();

    bool IsRunning() const { return m_hook != nullptr; }

    // Settings ---------------------------------------------------------------
    enum class Modifier
    {
        Alt,
        Ctrl,
        Shift,
    };

    void SetModifier(Modifier mod) { m_modifier = mod; }
    Modifier GetModifier() const { return m_modifier; }

private:
    AltDragMove() = default;
    ~AltDragMove() = default;
    AltDragMove(const AltDragMove&) = delete;
    AltDragMove& operator=(const AltDragMove&) = delete;

    static LRESULT CALLBACK LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam);

    bool IsModifierPressed() const;

    HHOOK m_hook = nullptr;
    Modifier m_modifier = Modifier::Alt;

    // Drag state
    bool m_dragging = false;
    HWND m_dragWindow = nullptr;
    POINT m_dragStartCursor = {};
    RECT m_dragStartRect = {};
};
