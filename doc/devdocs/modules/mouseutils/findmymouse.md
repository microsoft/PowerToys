# Find My Mouse

Find My Mouse is a utility that helps users locate their mouse pointer by creating a spotlight effect when activated. It is based on Raymond Chen's SuperSonar utility.

## Implementation

Find My Mouse displays a spotlight effect centered on the cursor location when activated via a keyboard shortcut (typically a double-press of the Ctrl key).

### Key Files
- `src/modules/MouseUtils/FindMyMouse/FindMyMouse.cpp` - Contains the main implementation
- Key function: `s_WndProc` - Handles window messages for the utility

### Enabling Process

When the utility is enabled:

1. A background thread is created to run the Find My Mouse logic asynchronously:
   ```cpp
   // Enable the PowerToy
   virtual void enable()
   {
       m_enabled = true;  // Mark the module as enabled
       Trace::EnableFindMyMouse(true);  // Enable telemetry
       std::thread([=]() { FindMyMouseMain(m_hModule, m_findMyMouseSettings); }).detach();  // Run main logic in background
   }
   ```

2. The `CompositionSpotlight` instance is initialized with user settings:
   ```cpp
   CompositionSpotlight sonar;
   sonar.ApplySettings(settings, false);  // Apply settings
   if (!sonar.Initialize(hinst))
   {
       Logger::error("Couldn't initialize a sonar instance.");
       return 0;
   }

   m_sonar = &sonar;
   ```

3. The utility listens for raw input events using `WM_INPUT`, which provides more precise and responsive input detection than standard mouse events.

### Activation Process

The activation process works as follows:

1. **Keyboard Hook Detects Shortcut**
   - A global low-level keyboard hook is set up during initialization
   - The hook monitors for the specific activation pattern (double Ctrl press)
   - Once matched, it sends a `WM_PRIV_SHORTCUT` message to the sonar window:
     ```cpp
     virtual void OnHotkeyEx() override
     {
         Logger::trace("OnHotkeyEx()");
         HWND hwnd = GetSonarHwnd();
         if (hwnd != nullptr)
         {
             PostMessageW(hwnd, WM_PRIV_SHORTCUT, NULL, NULL);
         }
     }
     ```

2. **Message Handler Triggers Action**
   - The custom message is routed to `BaseWndProc()`
   - The handler toggles the sonar animation:
     ```cpp
     if (message == WM_PRIV_SHORTCUT)
     {
         if (m_sonarStart == NoSonar)
             StartSonar();  // Trigger sonar animation
         else
             StopSonar();   // Cancel if already running
     }
     ```

3. **Sonar Animation**
   - `StartSonar()` uses `CompositionSpotlight` to display a highlight (ripple/pulse) centered on the mouse pointer
   - The animation is temporary and fades automatically or can be cancelled by user input

### Event Handling

The Find My Mouse utility handles several types of events:

- **Mouse Events**: Trigger sonar animations (e.g., after a shake or shortcut)
- **Keyboard Events**: May cancel or toggle the effect
- **Custom Shortcut Messages**: Handled to allow toggling Find My Mouse using a user-defined hotkey

When the main window receives a `WM_DESTROY` message (on shutdown or disable), the sonar instance is properly cleaned up, and the message loop ends gracefully.

## Debugging

To debug Find My Mouse:
- Attach to the PowerToys Runner process directly
- Set breakpoints in the `FindMyMouse.cpp` file
- When debugging the spotlight effect, visual artifacts may occur due to the debugger's overhead
