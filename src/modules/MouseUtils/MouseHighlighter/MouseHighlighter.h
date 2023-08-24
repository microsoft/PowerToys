#pragma once
#include "pch.h"

const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_LEFT_BUTTON_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(166, 255, 255, 0);
const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_RIGHT_BUTTON_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(166, 0, 0, 255);
const winrt::Windows::UI::Color MOUSE_HIGHLIGHTER_DEFAULT_ALWAYS_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(0, 255, 0, 0);
constexpr int MOUSE_HIGHLIGHTER_DEFAULT_RADIUS = 20;
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
};

int MouseHighlighterMain(HINSTANCE hinst, MouseHighlighterSettings settings);
void MouseHighlighterDisable();
bool MouseHighlighterIsEnabled();
void MouseHighlighterSwitch();
void MouseHighlighterApplySettings(MouseHighlighterSettings settings);
