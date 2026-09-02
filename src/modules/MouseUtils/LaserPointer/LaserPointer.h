#pragma once
#include "pch.h"

// Default activation shortcut is Win+Shift+L (see dllmain.cpp).
constexpr int LASER_POINTER_DEFAULT_SIZE = 10;
constexpr int LASER_POINTER_DEFAULT_DECAY_TIME_MS = 1000;
constexpr int LASER_POINTER_DEFAULT_DECAY_LENGTH = 50;
constexpr int LASER_POINTER_DEFAULT_STREAMLINE_PERCENT = 75;
constexpr bool LASER_POINTER_DEFAULT_AUTO_ACTIVATE = false;
constexpr bool LASER_POINTER_DEFAULT_GLOW_ENABLED = true;
// Off by default: the pen tip has to touch the screen. Turning it on starts the trail as
// soon as the pen is near enough for the digitizer to report it.
constexpr bool LASER_POINTER_DEFAULT_PEN_RENDER_WHEN_CLOSE = false;
constexpr bool LASER_POINTER_DEFAULT_SUPPRESS_ACTIVATION_BUTTON = true;

// Which physical button draws the trail while held.
//
// Windows only ever reports five mouse buttons: WH_MOUSE_LL and Raw Input's RAWMOUSE
// both cap out here. Mice advertising more buttons either have them remapped to
// keystrokes by their vendor software, or emit them as raw HID Button-page usages that
// never reach the mouse message stream at all.
enum class LaserPointerButton : int
{
    None = -1,
    Left = 0,
    Right = 1,
    Middle = 2,
    X1 = 3,
    X2 = 4,
};

// The two activation shortcuts are independent and exclusive in effect: the mouse
// shortcut arms only the mouse, the pen shortcut arms only the pen. Leaving the pen
// shortcut unset is how pen support is turned off.
//
// The activation shortcut arms and disarms the tool. Arming on its own draws nothing:
// the trail is only ever drawn while the configured mouse button (or the pen tip) is
// held down, and it fades out once released.
struct LaserPointerSettings
{
    // Only consulted while the activation shortcut has armed the mouse. Any of the five
    // buttons is allowed here because arming is explicit and temporary.
    LaserPointerButton activationButton = LaserPointerButton::Middle;
    // Draws whenever it is held, with no shortcut needed. Left and Right are rejected:
    // permanently swallowing either would make the machine unusable. None disables the
    // mode, and the mouse hook is not installed on its behalf.
    LaserPointerButton alwaysOnButton = LaserPointerButton::None;
    // Swallow the activation button so the app underneath does not also react to it
    // (X1/X2 are Back/Forward in most browsers, middle click opens links in tabs).
    bool suppressActivationButton = LASER_POINTER_DEFAULT_SUPPRESS_ACTIVATION_BUTTON;
    bool autoActivate = LASER_POINTER_DEFAULT_AUTO_ACTIVATE;
    bool glowEnabled = LASER_POINTER_DEFAULT_GLOW_ENABLED;
    // false: the trail is drawn only while the tip is actually touching.
    // true: the trail follows the pen as soon as it is in range.
    bool penRenderWhenClose = LASER_POINTER_DEFAULT_PEN_RENDER_WHEN_CLOSE;

    winrt::Windows::UI::Color laserColor = winrt::Windows::UI::ColorHelper::FromArgb(153, 255, 45, 45);
    int size = LASER_POINTER_DEFAULT_SIZE;
    int decayTimeMs = LASER_POINTER_DEFAULT_DECAY_TIME_MS;
    int decayLength = LASER_POINTER_DEFAULT_DECAY_LENGTH;
    int streamlinePercent = LASER_POINTER_DEFAULT_STREAMLINE_PERCENT;
};

int LaserPointerMain(HINSTANCE hinst, LaserPointerSettings settings);
void LaserPointerDisable();
bool LaserPointerIsEnabled();
// Arms/disarms the mouse. The pen has its own shortcut so it can be used on its own.
void LaserPointerSwitch();
void LaserPointerSwitchPen();
// Independent of the laser: mirrors the window in front into an off-screen window that
// screen sharing can pick up, and keeps mirroring it until switched off again.
void LaserPointerSwitchPresenter();

// The same toggle, invoked from another window - the Quick Access flyout. That window is
// in front and dismisses itself immediately afterwards, so the target cannot come from
// the cursor the way the shortcut's does.
void LaserPointerSwitchPresenterExternal();
void LaserPointerApplySettings(LaserPointerSettings settings);
