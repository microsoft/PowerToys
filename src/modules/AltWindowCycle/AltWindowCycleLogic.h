#pragma once

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>

#include <algorithm>
#include <string>
#include <vector>
#include <wchar.h>

namespace AltWindowCycleLogic
{
    constexpr int DefaultMaxColumns = 6;
    constexpr unsigned int ModifierAlt = 1u << 0;
    constexpr unsigned int ModifierCtrl = 1u << 1;
    constexpr unsigned int ModifierShift = 1u << 2;
    constexpr unsigned int ModifierWin = 1u << 3;
    constexpr unsigned int AllModifiers = ModifierAlt | ModifierCtrl | ModifierShift | ModifierWin;

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

    constexpr unsigned int StableHoldModifiers(unsigned int configuredModifiers)
    {
        const unsigned int sanitized = configuredModifiers & AllModifiers;
        const unsigned int nonShiftModifiers = sanitized & ~ModifierShift;
        return nonShiftModifiers != 0 ? nonShiftModifiers : sanitized;
    }

    constexpr bool AreRequiredModifiersDown(unsigned int requiredModifiers, unsigned int downModifiers)
    {
        return requiredModifiers != 0 && (downModifiers & requiredModifiers) == requiredModifiers;
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
        layout.tileW = ScaledValue(scale, 270);
        layout.headerH = ScaledValue(scale, 48);
        layout.previewH = ScaledValue(scale, 142);
        layout.inner = ScaledValue(scale, 6);
        layout.radius = ScaledValue(scale, 10);
        layout.cardTrimBottom = 0;
        layout.iconSize = ScaledValue(scale, 16);
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
        // Sits directly below the header band, inset by the 1px card stroke on the
        // left/right/bottom so the card border stays visible around the image.
        const int stroke = ScaledValue(layout.scale, 1);
        return {
            tile.left + stroke,
            tile.top + layout.headerH,
            tile.right - stroke,
            tile.bottom - stroke
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

    // =================== Same-process window selection ===================
    //
    // The live module queries Win32 for each window's state; these helpers own the
    // pure decision + ordering logic so the Alt-Tab cycle set can be unit-tested
    // without a live desktop.

    struct WindowEligibility
    {
        bool isVisible = false;
        bool isCloaked = false;
        // True when the window is the representative window for its owner chain, i.e.
        // the GetLastActivePopup walk from its root owner lands back on the window
        // itself. Owned secondary windows (whose owner is visible) are not.
        bool isAltTabRepresentative = false;
        bool isToolWindow = false;
    };

    // Mirrors the classic "IsAltTabWindow" predicate: a window participates in the
    // cycle only when it is visible, not cloaked, the representative window of its
    // owner chain, and not a tool window.
    constexpr bool IsAltTabEligible(const WindowEligibility& window)
    {
        return window.isVisible && !window.isCloaked && window.isAltTabRepresentative && !window.isToolWindow;
    }

    struct CandidateWindow
    {
        unsigned long long id = 0;
        WindowEligibility eligibility;
        // Resolved owning-process image path (UWP windows resolved to the real app,
        // not ApplicationFrameHost). Empty when it could not be determined.
        std::wstring processKey;
    };

    // Case-insensitive process-key match, mirroring the live _wcsicmp grouping. An
    // empty candidate key never matches.
    inline bool ProcessKeyEquals(const std::wstring& candidateKey, const std::wstring& foregroundKey)
    {
        return !candidateKey.empty() && _wcsicmp(candidateKey.c_str(), foregroundKey.c_str()) == 0;
    }

    // Given the foreground app's process key and the windows enumerated in Z-order
    // (top-most first == MRU), return the ids that make up the Alt-Tab cycle set for
    // that app, preserving enumeration order. Ineligible windows (invisible, cloaked,
    // owned, or tool) and windows from other processes are dropped. Returns empty when
    // the foreground key is unknown.
    inline std::vector<unsigned long long> SelectCycleWindows(
        const std::wstring& foregroundProcessKey,
        const std::vector<CandidateWindow>& enumeratedInZOrder)
    {
        std::vector<unsigned long long> result;
        if (foregroundProcessKey.empty())
        {
            return result;
        }

        for (const auto& candidate : enumeratedInZOrder)
        {
            if (!IsAltTabEligible(candidate.eligibility))
            {
                continue;
            }

            if (ProcessKeyEquals(candidate.processKey, foregroundProcessKey))
            {
                result.push_back(candidate.id);
            }
        }

        return result;
    }
}
