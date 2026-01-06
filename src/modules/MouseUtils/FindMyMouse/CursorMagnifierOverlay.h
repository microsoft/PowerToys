#pragma once
#include "pch.h"

class CursorMagnifierOverlay
{
public:
    CursorMagnifierOverlay() = default;
    ~CursorMagnifierOverlay();

    bool Initialize(HINSTANCE instance);
    void Terminate();
    void SetVisible(bool visible);
    void SetScale(float scale);

private:
    static constexpr wchar_t kWindowClassName[] = L"FindMyMouseCursorMagnifier";
    static constexpr UINT_PTR kTimerId = 1;
    static constexpr UINT kFrameIntervalMs = 16;

    static LRESULT CALLBACK WndProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept;

    void OnTimer();
    void Render();
    void EnsureResources(int width, int height);
    void CleanupResources();
    void DestroyWindowInternal();

    HWND m_hwnd = nullptr;
    HINSTANCE m_instance = nullptr;
    bool m_visible = false;
    float m_scale = 2.0f;

    HDC m_memDc = nullptr;
    HBITMAP m_dib = nullptr;
    void* m_bits = nullptr;
    SIZE m_dibSize{ 0, 0 };
};
