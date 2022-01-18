#pragma once
#include "pch.h"

constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_OPACITY = 75;
const winrt::Windows::UI::Color INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_OPACITY*255/100, 255, 0, 0);
constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_RADIUS = 20;
constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_THICKNESS = 10;

struct InclusiveCrosshairSettings
{
    winrt::Windows::UI::Color crosshairColor = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_COLOR;
    int crosshairRadius = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_RADIUS;
    int crosshairThickness = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_THICKNESS;
};

int InclusiveCrosshairMain(HINSTANCE hinst, InclusiveCrosshairSettings settings);
void InclusiveCrosshairDisable();
bool InclusiveCrosshairIsEnabled();
void InclusiveCrosshairSwitch();
void InclusiveCrosshairApplySettings(InclusiveCrosshairSettings settings);
