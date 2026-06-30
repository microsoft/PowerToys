#pragma once
#include "pch.h"

const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_LEFT_BUTTON_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(166, 255, 255, 0);
const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_RIGHT_BUTTON_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(166, 0, 0, 255);
const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_ALWAYS_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(0, 255, 0, 0);
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_RADIUS = 30;
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_DELAY_MS = 400;
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_DURATION_MS = 400;
constexpr bool MOUSE_HIGHLIGHTER_DEFAULT_AUTO_ACTIVATE = false;
// Ripple-specific defaults (independent of the always-on circle settings above).
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SIZE = 60;
constexpr double MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_INTENSITY = 0.7;
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_DURATION_MS = 480;
constexpr bool MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SHOW_DRAG_TRAIL = true;
constexpr bool MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SHOW_RELEASE_PULSE = true;

struct MouseHighlighterSettings
{
    winrt::Windows::UI::Color leftButtonColor = MOUSE_HIGHLIGHTER_DEFAULT_LEFT_BUTTON_COLOR;
    winrt::Windows::UI::Color rightButtonColor = MOUSE_HIGHLIGHTER_DEFAULT_RIGHT_BUTTON_COLOR;
    winrt::Windows::UI::Color alwaysColor = MOUSE_HIGHLIGHTER_DEFAULT_ALWAYS_COLOR;
    int radius = MOUSE_HIGHLIGHTER_DEFAULT_RADIUS;
    int fadeDelayMs = MOUSE_HIGHLIGHTER_DEFAULT_DELAY_MS;
    int fadeDurationMs = MOUSE_HIGHLIGHTER_DEFAULT_DURATION_MS;
    bool autoActivate = MOUSE_HIGHLIGHTER_DEFAULT_AUTO_ACTIVATE;
    bool spotlightMode = false;
    bool rippleMode = true;
    int rippleSize = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SIZE;
    double rippleIntensity = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_INTENSITY;
    int rippleDurationMs = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_DURATION_MS;
    bool rippleShowDragTrail = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SHOW_DRAG_TRAIL;
    bool rippleShowReleasePulse = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SHOW_RELEASE_PULSE;
};

int MouseHighlighterMain(HINSTANCE hinst, MouseHighlighterSettings settings);
void MouseHighlighterDisable();
bool MouseHighlighterIsEnabled();
void MouseHighlighterSwitch();
void MouseHighlighterApplySettings(MouseHighlighterSettings settings);
