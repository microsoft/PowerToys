#include "pch.h"
#include "Colors.h"

#include <winrt/Windows.UI.ViewManagement.h>

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/util.h>

namespace Colors
{
    COLORREF currentAccentColor;
    COLORREF currentBackgroundColor;

    bool GetSystemTheme() noexcept
    {
        winrt::Windows::UI::ViewManagement::UISettings settings;
        auto accentValue = settings.GetColorValue(winrt::Windows::UI::ViewManagement::UIColorType::Accent);
        auto accentColor = RGB(accentValue.R, accentValue.G, accentValue.B);

        auto backgroundValue = settings.GetColorValue(winrt::Windows::UI::ViewManagement::UIColorType::Background);
        auto backgroundColor = RGB(backgroundValue.R, backgroundValue.G, backgroundValue.B);

        if (currentAccentColor != accentColor || currentBackgroundColor != backgroundColor)
        {
            currentAccentColor = accentColor;
            currentBackgroundColor = backgroundColor;
            return true;
        }

        return false;
    }

    ZoneColors GetZoneColors() noexcept
    {
        if (FancyZonesSettings::settings().systemTheme)
        {
            GetSystemTheme();
            auto numberColor = currentBackgroundColor == RGB(0, 0, 0) ? RGB(255, 255, 255) : RGB(0, 0, 0);

            return ZoneColors{
                .primaryColor = currentBackgroundColor,
                .borderColor = currentAccentColor,
                .highlightColor = currentAccentColor,
                .numberColor = numberColor,
                .highlightOpacity = FancyZonesSettings::settings().zoneHighlightOpacity
            };
        }
        else
        {
            return ZoneColors{
                .primaryColor = FancyZonesUtils::HexToRGB(FancyZonesSettings::settings().zoneColor),
                .borderColor = FancyZonesUtils::HexToRGB(FancyZonesSettings::settings().zoneBorderColor),
                .highlightColor = FancyZonesUtils::HexToRGB(FancyZonesSettings::settings().zoneHighlightColor),
                .numberColor = FancyZonesUtils::HexToRGB(FancyZonesSettings::settings().zoneNumberColor),
                .highlightOpacity = FancyZonesSettings::settings().zoneHighlightOpacity
            };
        }
    }
}
