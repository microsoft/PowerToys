#pragma once
#include "pch.h"

enum struct FindMyMouseActivationMethod : int
{
    DoubleLeftControlKey = 0,
    DoubleRightControlKey = 1,
    ShakeMouse = 2,
    Shortcut = 3,
    EnumElements = 4, // number of elements in the enum, not counting this
};

constexpr bool FIND_MY_MOUSE_DEFAULT_DO_NOT_ACTIVATE_ON_GAME_MODE = true;
// Default colors now include full alpha. Opacity is encoded directly in color alpha (legacy overlay_opacity migrated into A channel)
const winrt::Windows::UI::Color FIND_MY_MOUSE_DEFAULT_BACKGROUND_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(128, 0, 0, 0);
const winrt::Windows::UI::Color FIND_MY_MOUSE_DEFAULT_SPOTLIGHT_COLOR = winrt::Windows::UI::ColorHelper::FromArgb(128, 255, 255, 255);
constexpr int FIND_MY_MOUSE_DEFAULT_SPOTLIGHT_RADIUS = 100;
constexpr int FIND_MY_MOUSE_DEFAULT_ANIMATION_DURATION_MS = 500;
constexpr int FIND_MY_MOUSE_DEFAULT_SPOTLIGHT_INITIAL_ZOOM = 9;
constexpr FindMyMouseActivationMethod FIND_MY_MOUSE_DEFAULT_ACTIVATION_METHOD = FindMyMouseActivationMethod::DoubleLeftControlKey;
constexpr bool FIND_MY_MOUSE_DEFAULT_INCLUDE_WIN_KEY = false;
constexpr int FIND_MY_MOUSE_DEFAULT_SHAKE_MINIMUM_DISTANCE = 1000;
constexpr int FIND_MY_MOUSE_DEFAULT_SHAKE_INTERVAL_MS = 1000;
constexpr int FIND_MY_MOUSE_DEFAULT_SHAKE_FACTOR = 400; // 400 percent

struct FindMyMouseSettings
{
    FindMyMouseActivationMethod activationMethod = FIND_MY_MOUSE_DEFAULT_ACTIVATION_METHOD;
    bool includeWinKey = FIND_MY_MOUSE_DEFAULT_INCLUDE_WIN_KEY;
    bool doNotActivateOnGameMode = FIND_MY_MOUSE_DEFAULT_DO_NOT_ACTIVATE_ON_GAME_MODE;
    winrt::Windows::UI::Color backgroundColor = FIND_MY_MOUSE_DEFAULT_BACKGROUND_COLOR;
    winrt::Windows::UI::Color spotlightColor = FIND_MY_MOUSE_DEFAULT_SPOTLIGHT_COLOR;
    int spotlightRadius = FIND_MY_MOUSE_DEFAULT_SPOTLIGHT_RADIUS;
    int animationDurationMs = FIND_MY_MOUSE_DEFAULT_ANIMATION_DURATION_MS;
    int spotlightInitialZoom = FIND_MY_MOUSE_DEFAULT_SPOTLIGHT_INITIAL_ZOOM;
    int shakeMinimumDistance = FIND_MY_MOUSE_DEFAULT_SHAKE_MINIMUM_DISTANCE;
    int shakeIntervalMs = FIND_MY_MOUSE_DEFAULT_SHAKE_INTERVAL_MS;
    int shakeFactor = FIND_MY_MOUSE_DEFAULT_SHAKE_FACTOR;
    std::vector<std::wstring> excludedApps;
};

int FindMyMouseMain(HINSTANCE hinst, const FindMyMouseSettings& settings);
void FindMyMouseDisable();
bool FindMyMouseIsEnabled();
void FindMyMouseApplySettings(const FindMyMouseSettings& settings);
HWND GetSonarHwnd() noexcept;
