#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

#include <algorithm>

namespace AltWindowCycleLogic
{
    constexpr int DefaultMaxColumns = 6;

    struct OverlayLayout
    {
        double scale = 1.0;
        int pad = 0;
        int gap = 0;
        int tileW = 0;
        int tileH = 0;
        int headerH = 0;
        int previewH = 0;
        int inner = 0;
        int radius = 0;
        int cardTrimBottom = 0;
        int iconSize = 0;
        int cols = 1;
        int rows = 0;
        int panelX = 0;
        int panelY = 0;
        int panelW = 0;
        int panelH = 0;
    };

    enum class FirstHotkeyAction
    {
        Ignore,
        ShowOverlay,
    };

    struct FirstHotkeyResult
    {
        int selected = 0;
        FirstHotkeyAction action = FirstHotkeyAction::Ignore;
    };

    constexpr int ScaledValue(double scale, int value)
    {
        return static_cast<int>(value * scale + 0.5);
    }

    inline int WrapIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        return ((index % count) + count) % count;
    }

    inline FirstHotkeyResult BeginCycle(int currentIndex, int windowCount, bool forward)
    {
        if (windowCount < 2)
        {
            return {};
        }

        const int normalizedCurrentIndex = currentIndex < 0 ? 0 : currentIndex;
        return {
            WrapIndex(forward ? normalizedCurrentIndex + 1 : normalizedCurrentIndex - 1, windowCount),
            FirstHotkeyAction::ShowOverlay
        };
    }

    inline OverlayLayout ComputeOverlayLayout(const RECT& work, int windowCount, double scale, int maxColumns = DefaultMaxColumns)
    {
        OverlayLayout layout;
        layout.scale = scale;
        layout.pad = ScaledValue(scale, 32);
        layout.gap = ScaledValue(scale, 26);
        layout.tileW = ScaledValue(scale, 300);
        layout.headerH = ScaledValue(scale, 44);
        layout.previewH = ScaledValue(scale, 158);
        layout.inner = ScaledValue(scale, 8);
        layout.radius = ScaledValue(scale, 8);
        layout.cardTrimBottom = 0;
        layout.iconSize = ScaledValue(scale, 24);
        layout.tileH = layout.headerH + layout.inner + layout.previewH + layout.inner;

        const int workW = work.right - work.left;
        const int workH = work.bottom - work.top;
        const int safeWindowCount = (std::max)(0, windowCount);
        const int safeMaxColumns = (std::max)(1, maxColumns);
        const int columnsFromWork = (std::max)(1, (workW - 2 * layout.pad + layout.gap) / (layout.tileW + layout.gap));

        layout.cols = (std::min)(safeWindowCount, (std::min)(safeMaxColumns, columnsFromWork));
        if (layout.cols < 1)
        {
            layout.cols = 1;
        }

        layout.rows = (safeWindowCount + layout.cols - 1) / layout.cols;
        layout.panelW = 2 * layout.pad + layout.cols * layout.tileW + (layout.cols - 1) * layout.gap;
        layout.panelH = 2 * layout.pad + layout.rows * layout.tileH + (layout.rows - 1) * layout.gap;
        layout.panelX = work.left + (workW - layout.panelW) / 2;
        layout.panelY = work.top + (workH - layout.panelH) / 2;

        return layout;
    }

    inline RECT TileRect(const OverlayLayout& layout, int index)
    {
        const int col = index % layout.cols;
        const int row = index / layout.cols;
        const int left = layout.pad + col * (layout.tileW + layout.gap);
        const int top = layout.pad + row * (layout.tileH + layout.gap);
        return { left, top, left + layout.tileW, top + layout.tileH - layout.cardTrimBottom };
    }

    inline RECT PreviewRect(const OverlayLayout& layout, const RECT& tile)
    {
        return {
            tile.left + layout.inner,
            tile.top + layout.headerH + layout.inner,
            tile.right - layout.inner,
            tile.bottom - layout.inner
        };
    }

    inline RECT HeaderRect(const OverlayLayout& layout, const RECT& tile)
    {
        const int margin = ScaledValue(layout.scale, 12);
        return { tile.left + margin, tile.top, tile.right - margin, tile.top + layout.headerH };
    }

    inline RECT CoverSource(const RECT& dest, const RECT& avail)
    {
        const int aw = avail.right - avail.left;
        const int ah = avail.bottom - avail.top;
        const int dw = dest.right - dest.left;
        const int dh = dest.bottom - dest.top;
        if (aw <= 0 || ah <= 0 || dw <= 0 || dh <= 0)
        {
            return avail;
        }

        const double destA = static_cast<double>(dw) / dh;
        const double srcA = static_cast<double>(aw) / ah;
        if (srcA > destA)
        {
            const int cw = (std::max)(1, static_cast<int>(ah * destA + 0.5));
            const int x = avail.left + (aw - cw) / 2;
            return { x, avail.top, x + cw, avail.bottom };
        }

        const int ch = (std::max)(1, static_cast<int>(aw / destA + 0.5));
        const int y = avail.top + (ah - ch) / 2;
        return { avail.left, y, avail.right, y + ch };
    }
}
