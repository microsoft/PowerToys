#pragma once
#include "pch.h"

#include <unordered_map>

class CursorMagnifierOverlay
{
public:
    CursorMagnifierOverlay() = default;
    ~CursorMagnifierOverlay();

    bool Initialize(HINSTANCE instance);
    void Terminate();
    void SetVisible(bool visible);
    void SetScale(float scale);
    void SetAnimationDurationMs(int durationMs);

private:
    static constexpr wchar_t kWindowClassName[] = L"FindMyMouseCursorMagnifier";
    static constexpr UINT_PTR kTimerId = 1;
    static constexpr UINT kFrameIntervalMs = 16;

    static LRESULT CALLBACK WndProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept;

    void OnTimer();
    void Render();
    void EnsureResources(int width, int height);
    void CleanupResources();
    bool HideSystemCursors();
    void RestoreSystemCursors();
    HCURSOR CreateTransparentCursor() const;
    void ReleaseOriginalCursors();
    void UpdateCursorMetrics(HCURSOR cursor);
    void ResetCursorMetrics();
    void DestroyWindowInternal();
    void BeginScaleAnimation();
    float GetAnimatedScale();

    HWND m_hwnd = nullptr;
    HINSTANCE m_instance = nullptr;
    bool m_visible = false;
    float m_targetScale = 2.0f;
    float m_startScale = 1.0f;
    float m_currentScale = 1.0f;
    ULONGLONG m_animationStartTick = 0;
    DWORD m_animationDurationMs = 500;

    bool m_systemCursorsHidden = false;
    std::unordered_map<HCURSOR, UINT> m_hiddenCursorIds;
    std::unordered_map<UINT, HCURSOR> m_originalCursors;

    HCURSOR m_cachedCursor = nullptr;
    SIZE m_cursorSize{ 0, 0 };
    POINT m_hotspot{ 0, 0 };

    HDC m_memDc = nullptr;
    HBITMAP m_dib = nullptr;
    void* m_bits = nullptr;
    SIZE m_dibSize{ 0, 0 };
};
