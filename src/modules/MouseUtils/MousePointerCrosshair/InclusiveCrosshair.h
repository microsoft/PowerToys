#pragma once
#include "pch.h"

constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_OPACITY = 75;
const winrt::Windows::UI::Color INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(255, 255, 0, 0);
const winrt::Windows::UI::Color INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_BORDER_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(255, 255, 255, 255);
constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_RADIUS = 20;
constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_THICKNESS = 5;
constexpr int INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_BORDER_SIZE = 1;

struct InclusiveCrosshairSettings
{
    winrt::Windows::UI::Color crosshairColor = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_COLOR;
    winrt::Windows::UI::Color crosshairBorderColor = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_BORDER_COLOR;
    int crosshairRadius = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_RADIUS;
    int crosshairThickness = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_THICKNESS;
    int crosshairOpacity = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_OPACITY;
    int crosshairBorderSize = INCLUSIVE_MOUSE_DEFAULT_CROSSHAIR_BORDER_SIZE;
};

int InclusiveCrosshairMain(HINSTANCE hinst, InclusiveCrosshairSettings& settings);
void InclusiveCrosshairDisable();
bool InclusiveCrosshairIsEnabled();
void InclusiveCrosshairSwitch();
void InclusiveCrosshairApplySettings(InclusiveCrosshairSettings& settings);
