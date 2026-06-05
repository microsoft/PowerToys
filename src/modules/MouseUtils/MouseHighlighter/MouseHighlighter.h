#pragma once

#include <algorithm>
#include <windows.h>
#include <winrt/Windows.UI.h>

const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_LEFT_BUTTON_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(166, 255, 255, 0);
const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_RIGHT_BUTTON_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(166, 0, 0, 255);
const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_ALWAYS_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(0, 255, 0, 0);
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_RADIUS = 20;
constexpr int MOUSE_HIGHLIGHTER_MIN_FADE_MS = 1;
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_DELAY_MS = 500;
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_DURATION_MS = 250;
constexpr bool MOUSE_HIGHLIGHTER_DEFAULT_AUTO_ACTIVATE = false;

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
};

struct MouseHighlighterRuntimeSettings
{
    winrt::Windows::UI::Color leftButtonColor = MOUSE_HIGHLIGHTER_DEFAULT_LEFT_BUTTON_COLOR;
    winrt::Windows::UI::Color rightButtonColor = MOUSE_HIGHLIGHTER_DEFAULT_RIGHT_BUTTON_COLOR;
    winrt::Windows::UI::Color alwaysColor = MOUSE_HIGHLIGHTER_DEFAULT_ALWAYS_COLOR;
    float radius = static_cast<float>(MOUSE_HIGHLIGHTER_DEFAULT_RADIUS);
    int fadeDelayMs = MOUSE_HIGHLIGHTER_DEFAULT_DELAY_MS;
    int fadeDurationMs = MOUSE_HIGHLIGHTER_DEFAULT_DURATION_MS;
    bool leftPointerEnabled = true;
    bool rightPointerEnabled = true;
    bool alwaysPointerEnabled = false;
    bool spotlightMode = false;
};

inline int NormalizeMouseHighlighterFadeMilliseconds(int milliseconds) noexcept
{
    return (std::max)(milliseconds, MOUSE_HIGHLIGHTER_MIN_FADE_MS);
}

inline MouseHighlighterRuntimeSettings NormalizeMouseHighlighterSettings(const MouseHighlighterSettings& settings) noexcept
{
    MouseHighlighterRuntimeSettings runtimeSettings;
    runtimeSettings.leftButtonColor = settings.leftButtonColor;
    runtimeSettings.rightButtonColor = settings.rightButtonColor;
    runtimeSettings.alwaysColor = settings.alwaysColor;
    runtimeSettings.radius = static_cast<float>(settings.radius);
    runtimeSettings.fadeDelayMs = NormalizeMouseHighlighterFadeMilliseconds(settings.fadeDelayMs);
    runtimeSettings.fadeDurationMs = NormalizeMouseHighlighterFadeMilliseconds(settings.fadeDurationMs);
    runtimeSettings.leftPointerEnabled = settings.leftButtonColor.A != 0;
    runtimeSettings.rightPointerEnabled = settings.rightButtonColor.A != 0;
    runtimeSettings.alwaysPointerEnabled = settings.alwaysColor.A != 0;
    runtimeSettings.spotlightMode = settings.spotlightMode && runtimeSettings.alwaysPointerEnabled;

    if (runtimeSettings.spotlightMode)
    {
        runtimeSettings.leftPointerEnabled = false;
        runtimeSettings.rightPointerEnabled = false;
    }

    return runtimeSettings;
}

int MouseHighlighterMain(HINSTANCE hinst, MouseHighlighterSettings settings);
void MouseHighlighterDisable();
bool MouseHighlighterIsEnabled();
void MouseHighlighterSwitch();
void MouseHighlighterApplySettings(MouseHighlighterSettings settings);
