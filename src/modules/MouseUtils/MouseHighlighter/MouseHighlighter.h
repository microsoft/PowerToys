#pragma once
#include "pch.h"

#include "Keystrokes/KeystrokeTypes.h"

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
// Input Highlighter keystroke-overlay defaults.
const winrt::Windows::UI::Color INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_TEXT_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(255, 255, 255, 255);
const winrt::Windows::UI::Color INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_BACKGROUND_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(128, 0, 0, 0);
const winrt::Windows::UI::Color INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_STROKE_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(0, 255, 255, 255);
constexpr int INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_STROKE_THICKNESS = 0;
constexpr bool INPUT_HIGHLIGHTER_DEFAULT_SHOW_MOUSE = true;
constexpr bool INPUT_HIGHLIGHTER_DEFAULT_SHOW_KEYSTROKES = true;
constexpr int INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_DISPLAY_MODE = 0; // Last5
constexpr int INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_POSITION = 4; // BottomCenter
constexpr int INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_TIMEOUT_MS = 3000;
constexpr int INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_TEXT_SIZE = 24;
constexpr bool INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_DRAGGABLE = true;

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
    // Input Highlighter sub-toggles + keystroke overlay settings.
    bool showMouse = INPUT_HIGHLIGHTER_DEFAULT_SHOW_MOUSE;
    bool showKeystrokes = INPUT_HIGHLIGHTER_DEFAULT_SHOW_KEYSTROKES;
    int keystrokeDisplayMode = INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_DISPLAY_MODE;
    int keystrokePosition = INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_POSITION;
    int keystrokeTimeoutMs = INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_TIMEOUT_MS;
    int keystrokeTextSize = INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_TEXT_SIZE;
    winrt::Windows::UI::Color keystrokeTextColor = INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_TEXT_COLOR;
    winrt::Windows::UI::Color keystrokeBackgroundColor = INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_BACKGROUND_COLOR;
    winrt::Windows::UI::Color keystrokeStrokeColor = INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_STROKE_COLOR;
    int keystrokeStrokeThickness = INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_STROKE_THICKNESS;
    bool keystrokeDraggable = INPUT_HIGHLIGHTER_DEFAULT_KEYSTROKE_DRAGGABLE;
    // Overlay control shortcuts (handled inside the keystroke capture hook).
    // Defaults mirror the C# model: Ctrl+Win+/ and Ctrl+Win+D.
    InputHighlighter::HotkeyChord keystrokeSwitchMonitorHotkey{ true, false, false, true, 0xBF };
    InputHighlighter::HotkeyChord keystrokeSwitchDisplayModeHotkey{ true, false, false, true, 0x44 };
};

int MouseHighlighterMain(HINSTANCE hinst, MouseHighlighterSettings settings);
void MouseHighlighterDisable();
bool MouseHighlighterIsEnabled();
void MouseHighlighterSwitch();
void MouseHighlighterApplySettings(MouseHighlighterSettings settings);
