#pragma once
#include "D2DState.h"
#include "ToolState.h"

inline constexpr UINT WM_BOUNDS_SNAP_FRAME_READY = WM_USER + 2;

struct BoundsSnapFrameMessage
{
    uint64_t generation;
    std::shared_ptr<const OwnedBGRATextureView> frame;
};

void DrawBoundsToolTick(const CommonState& commonState,
                        const BoundsToolState& toolState,
                        const HWND overlayWindow,
                        const D2DState& d2dState);

LRESULT CALLBACK BoundsToolWndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;