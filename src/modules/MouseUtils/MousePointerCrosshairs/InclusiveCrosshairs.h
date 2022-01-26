#pragma once
#include "pch.h"

constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_OPACITY = 75;
const winrt::Windows::UI::Color INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(255, 255, 0, 0);
const winrt::Windows::UI::Color INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_BORDER_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(255, 255, 255, 255);
constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_RADIUS = 20;
constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_THICKNESS = 5;
constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_BORDER_SIZE = 1;

struct InclusiveCrosshairsSettings
{
    winrt::Windows::UI::Color crosshairsColor = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_COLOR;
    winrt::Windows::UI::Color crosshairsBorderColor = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_BORDER_COLOR;
    int crosshairsRadius = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_RADIUS;
    int crosshairsThickness = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_THICKNESS;
    int crosshairsOpacity = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_OPACITY;
    int crosshairsBorderSize = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIRS_BORDER_SIZE;
};

int InclusiveCrosshairsMain(HINSTANCE hinst, InclusiveCrosshairsSettings& settings);
void InclusiveCrosshairsDisable();
bool InclusiveCrosshairsIsEnabled();
void InclusiveCrosshairsSwitch();
void InclusiveCrosshairsApplySettings(InclusiveCrosshairsSettings& settings);
