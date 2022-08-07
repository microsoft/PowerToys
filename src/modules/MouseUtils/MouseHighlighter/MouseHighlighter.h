#pragma once
#include "pch.h"

constexpr int MOUSE_HIGHLIGHTER_DEFAULT_OPACITY = 160;
const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_LEFT_BUTTON_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(MOUSE_HIGHLIGHTER_DEFAULT_OPACITY, 255, 255, 0);
const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_RIGHT_BUTTON_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(MOUSE_HIGHLIGHTER_DEFAULT_OPACITY, 0, 0, 255);
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_RADIUS = 20;
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_DELAY_MS = 500;
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_DURATION_MS = 250;

struct MouseHighlighterSettings
{
    winrt::Windows::UI::Color leftButtonColor = MOUSE_HIGHLIGHTER_DEFAULT_LEFT_BUTTON_COLOR;
    winrt::Windows::UI::Color rightButtonColor = MOUSE_HIGHLIGHTER_DEFAULT_RIGHT_BUTTON_COLOR;
    int radius = MOUSE_HIGHLIGHTER_DEFAULT_RADIUS;
    int fadeDelayMs = MOUSE_HIGHLIGHTER_DEFAULT_DELAY_MS;
    int fadeDurationMs = MOUSE_HIGHLIGHTER_DEFAULT_DURATION_MS;
};

int MouseHighlighterMain(HINSTANCE hinst, MouseHighlighterSettings settings);
void MouseHighlighterDisable();
bool MouseHighlighterIsEnabled();
void MouseHighlighterSwitch();
void MouseHighlighterApplySettings(MouseHighlighterSettings settings);
