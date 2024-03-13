#pragma once

#include <common/utils/string_utils.h>

namespace FancyZonesUtils
{
    struct Rect
    {
        Rect() {}

        Rect(RECT rect) :
            m_rect(rect)
        {
        }

        Rect(RECT rect, UINT dpi) :
            m_rect(rect)
        {
            m_rect.right = m_rect.left + MulDiv(m_rect.right - m_rect.left, dpi, 96);
            m_rect.bottom = m_rect.top + MulDiv(m_rect.bottom - m_rect.top, dpi, 96);
        }

        int x() const { return m_rect.left; }
        int y() const { return m_rect.top; }
        int width() const { return m_rect.right - m_rect.left; }
        int height() const { return m_rect.bottom - m_rect.top; }
        int left() const { return m_rect.left; }
        int top() const { return m_rect.top; }
        int right() const { return m_rect.right; }
        int bottom() const { return m_rect.bottom; }
        int aspectRatio() const { return MulDiv(m_rect.bottom - m_rect.top, 100, m_rect.right - m_rect.left); }

    private:
        RECT m_rect{};
    };

    inline bool operator==(const FancyZonesUtils::Rect& left, const FancyZonesUtils::Rect& right)
    {
        return left.left() == right.left() && left.right() == right.right() && left.top() == right.top() && left.bottom() == right.bottom();
    }

    inline bool operator!=(const FancyZonesUtils::Rect& left, const FancyZonesUtils::Rect& right)
    {
        return left.left() != right.left() || left.right() != right.right() || left.top() != right.top() || left.bottom() != right.bottom();
    }

    inline void InitRGB(_Out_ RGBQUAD* quad, BYTE alpha, COLORREF color)
    {
        ZeroMemory(quad, sizeof(*quad));
        quad->rgbReserved = alpha;
        quad->rgbRed = GetRValue(color) * alpha / 255;
        quad->rgbGreen = GetGValue(color) * alpha / 255;
        quad->rgbBlue = GetBValue(color) * alpha / 255;
    }

    inline void FillRectARGB(wil::unique_hdc& hdc, RECT const* prcFill, BYTE alpha, COLORREF color, bool /*blendAlpha*/)
    {
        BITMAPINFO bi;
        ZeroMemory(&bi, sizeof(bi));
        bi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        bi.bmiHeader.biWidth = 1;
        bi.bmiHeader.biHeight = 1;
        bi.bmiHeader.biPlanes = 1;
        bi.bmiHeader.biBitCount = 32;
        bi.bmiHeader.biCompression = BI_RGB;

        RECT fillRect;
        CopyRect(&fillRect, prcFill);

        RGBQUAD bitmapBits;
        InitRGB(&bitmapBits, alpha, color);
        StretchDIBits(
            hdc.get(),
            fillRect.left,
            fillRect.top,
            fillRect.right - fillRect.left,
            fillRect.bottom - fillRect.top,
            0,
            0,
            1,
            1,
            &bitmapBits,
            &bi,
            DIB_RGB_COLORS,
            SRCCOPY);
    }

    inline COLORREF HexToRGB(std::wstring_view hex, const COLORREF fallbackColor = RGB(255, 255, 255))
    {
        hex = left_trim<wchar_t>(trim<wchar_t>(hex), L"#");

        try
        {
            const long long tmp = std::stoll(hex.data(), nullptr, 16);
            const BYTE nR = static_cast<BYTE>((tmp & 0xFF0000) >> 16);
            const BYTE nG = static_cast<BYTE>((tmp & 0xFF00) >> 8);
            const BYTE nB = static_cast<BYTE>((tmp & 0xFF));
            return RGB(nR, nG, nB);
        }
        catch (const std::exception&)
        {
            return fallbackColor;
        }
    }

    constexpr inline BYTE OpacitySettingToAlpha(int opacity)
    {
        return static_cast<BYTE>(opacity * 2.55);
    }

    template<RECT MONITORINFO::*member>
    std::vector<std::pair<HMONITOR, RECT>> GetAllMonitorRects()
    {
        using result_t = std::vector<std::pair<HMONITOR, RECT>>;
        result_t result;

        auto enumMonitors = [](HMONITOR monitor, HDC /*hdc*/, LPRECT /*pRect*/, LPARAM param) -> BOOL {
            MONITORINFOEX mi;
            mi.cbSize = sizeof(mi);
            result_t& result = *reinterpret_cast<result_t*>(param);
            if (GetMonitorInfo(monitor, &mi))
            {
                result.push_back({ monitor, mi.*member });
            }

            return TRUE;
        };

        EnumDisplayMonitors(NULL, NULL, enumMonitors, reinterpret_cast<LPARAM>(&result));
        return result;
    }

    template<RECT MONITORINFO::*member>
    std::vector<std::pair<HMONITOR, MONITORINFOEX>> GetAllMonitorInfo()
    {
        using result_t = std::vector<std::pair<HMONITOR, MONITORINFOEX>>;
        result_t result;

        auto enumMonitors = [](HMONITOR monitor, HDC /*hdc*/, LPRECT /*pRect*/, LPARAM param) -> BOOL {
            MONITORINFOEX mi;
            mi.cbSize = sizeof(mi);
            result_t& result = *reinterpret_cast<result_t*>(param);
            if (GetMonitorInfo(monitor, &mi))
            {
                result.push_back({ monitor, mi });
            }

            return TRUE;
        };

        EnumDisplayMonitors(NULL, NULL, enumMonitors, reinterpret_cast<LPARAM>(&result));
        return result;
    }

    template<RECT MONITORINFO::*member>
    RECT GetMonitorsCombinedRect(const std::vector<std::pair<HMONITOR, RECT>>& monitorRects)
    {
        bool empty = true;
        RECT result{ 0, 0, 0, 0 };

        for (auto& [monitor, rect] : monitorRects)
        {
            if (empty)
            {
                empty = false;
                result = rect;
            }
            else
            {
                result.left = min(result.left, rect.left);
                result.top = min(result.top, rect.top);
                result.right = max(result.right, rect.right);
                result.bottom = max(result.bottom, rect.bottom);
            }
        }

        return result;
    }

    template<RECT MONITORINFO::*member>
    RECT GetAllMonitorsCombinedRect()
    {
        auto allMonitors = GetAllMonitorRects<member>();
        return GetMonitorsCombinedRect<member>(allMonitors);
    }

    constexpr RECT PrepareRectForCycling(RECT windowRect, RECT workAreaRect, DWORD vkCode) noexcept
    {
        LONG deltaX = 0, deltaY = 0;
        switch (vkCode)
        {
        case VK_UP:
            deltaY = workAreaRect.bottom - workAreaRect.top;
            break;
        case VK_DOWN:
            deltaY = workAreaRect.top - workAreaRect.bottom;
            break;
        case VK_LEFT:
            deltaX = workAreaRect.right - workAreaRect.left;
            break;
        case VK_RIGHT:
            deltaX = workAreaRect.left - workAreaRect.right;
        }

        windowRect.left += deltaX;
        windowRect.right += deltaX;
        windowRect.top += deltaY;
        windowRect.bottom += deltaY;

        return windowRect;
    }

    UINT GetDpiForMonitor(HMONITOR monitor) noexcept;
    void OrderMonitors(std::vector<std::pair<HMONITOR, RECT>>& monitorInfo);
    std::vector<HMONITOR> GetMonitorsOrdered();

    bool IsValidGuid(const std::wstring& str);
    std::optional<GUID> GuidFromString(const std::wstring& str) noexcept;
    std::optional<std::wstring> GuidToString(const GUID& guid) noexcept;

    size_t ChooseNextZoneByPosition(DWORD vkCode, RECT windowRect, const std::vector<RECT>& zoneRects) noexcept;

    void SwallowKey(const WORD key) noexcept;
}
