#pragma once

#include <Windows.h>

// disabling warning 4458 - declaration of 'identifier' hides class member
// to avoid warnings from GDI files - can't add winRT directory to external code
// in the Cpp.Build.props
#pragma warning(push)
#pragma warning(disable : 4458)
#include "gdiplus.h"
#pragma warning(pop)

#include <string>
#include <vector>

namespace FancyZonesWindowUtils
{
    bool IsSplashScreen(HWND window);
    bool IsWindowMaximized(HWND window) noexcept;
    bool HasVisibleOwner(HWND window) noexcept;
    bool IsStandardWindow(HWND window);
    bool IsPopupWindow(HWND window) noexcept;
    bool HasThickFrameAndMinimizeMaximizeButtons(HWND window) noexcept;
    bool IsCandidateForZoning(HWND window);
    bool IsProcessOfWindowElevated(HWND window); // If HWND is already dead, we assume it wasn't elevated
    bool IsExcludedByUser(const std::wstring& processPath) noexcept;
    bool IsExcludedByDefault(const std::wstring& processPath) noexcept;

    void SwitchToWindow(HWND window) noexcept;
    void SizeWindowToRect(HWND window, RECT rect) noexcept; // Parameter rect must be in screen coordinates (e.g. obtained from GetWindowRect)
    void SaveWindowSizeAndOrigin(HWND window) noexcept;
    void RestoreWindowSize(HWND window) noexcept;
    void RestoreWindowOrigin(HWND window) noexcept;
    void MakeWindowTransparent(HWND window);
    RECT AdjustRectForSizeWindowToRect(HWND window, RECT rect, HWND windowOfRect) noexcept; // Parameter rect is in windowOfRect coordinates

    void DisableRoundCorners(HWND window) noexcept;
    void ResetRoundCornersPreference(HWND window) noexcept;

    bool IsCursorTypeIndicatingSizeEvent();
}