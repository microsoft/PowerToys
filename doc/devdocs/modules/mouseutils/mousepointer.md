# Mouse Pointer Crosshairs

Mouse Pointer Crosshairs is a utility that displays horizontal and vertical lines that intersect at the mouse cursor position, making it easier to track the cursor location on screen.

## Implementation

Mouse Pointer Crosshairs runs within the PowerToys Runner process and draws crosshair lines that follow the cursor in real-time.

### Key Files
- `src/modules/MouseUtils/MousePointerCrosshairs/InclusiveCrosshairs.cpp` - Contains the main implementation
- Key function: `WndProc` - Handles window messages and mouse events

### Enabling Process

When the utility is enabled:

1. A background thread is created to run the crosshairs logic asynchronously:
   ```cpp
   std::thread([=]() { InclusiveCrosshairsMain(hInstance, settings); }).detach();
   ```

2. The InclusiveCrosshairs instance is initialized and configured with user settings:
   ```cpp
   InclusiveCrosshairs crosshairs;
   InclusiveCrosshairs::instance = &crosshairs;
   crosshairs.ApplySettings(settings, false);
   crosshairs.MyRegisterClass(hInstance);
   ```

3. The utility:
   - Creates the crosshairs visuals using Windows Composition API inside `CreateInclusiveCrosshairs()`
   - Handles the `WM_CREATE` message to initialize the Windows Composition API (Compositor, visuals, and target)
   - Creates a transparent, layered window for drawing the crosshairs with specific extended window styles (e.g., `WS_EX_LAYERED`, `WS_EX_TRANSPARENT`)

### Activation Process

The activation process works as follows:

1. **Shortcut Detection**
   - When the activation shortcut is pressed, the window procedure (`WndProc`) receives a custom message `WM_SWITCH_ACTIVATION_MODE`

2. **Toggle Drawing State**
   ```cpp
   case WM_SWITCH_ACTIVATION_MODE:
       if (instance->m_drawing)
       {
           instance->StopDrawing();
       }
       else
       {
           instance->StartDrawing();
       }
       break;
   ```

3. **Start Drawing Function**
   - The `StartDrawing()` function is called to:
     - Log the start of drawing
     - Update the crosshairs position
     - Check if the cursor should be auto-hidden, and set a timer for auto-hide if enabled
     - Show the crosshairs window if the cursor is visible
     - Set a low-level mouse hook for immediate mouse movement updates
     - Start a 16 ms timer to track cursor movement from pen and Windows Ink input

   ```cpp
   void InclusiveCrosshairs::StartDrawing()
   {
       Logger::info("Start drawing crosshairs.");
       UpdateCrosshairsPosition();

       m_hiddenCursor = false;
       if (m_crosshairs_auto_hide)
       {
           CURSORINFO cursorInfo{};
           cursorInfo.cbSize = sizeof(cursorInfo);
           if (GetCursorInfo(&cursorInfo))
           {
               m_hiddenCursor = !(cursorInfo.flags & CURSOR_SHOWING);
           }

           SetAutoHideTimer();
       }

       if (!m_hiddenCursor)
       {
           ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
       }

       m_drawing = true;
       m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, m_hinstance, 0);
       SetTimer(m_hwnd, CURSOR_POSITION_TIMER_ID, CURSOR_POSITION_TIMER_INTERVAL_MS, NULL);
   }
   ```

4. **Stop Drawing Function**
   - The `StopDrawing()` function is called to:
     - Remove the mouse hook
     - Kill the auto-hide and cursor position timers
     - Hide the crosshairs window
     - Log the stop of drawing

### Cursor Tracking

While active, the low-level mouse hook (`WH_MOUSE_LL`) provides immediate mouse movement updates. A separate timer samples `GetCursorPos()` approximately every 16 ms to track pen and Windows Ink input, which can move the system cursor without producing low-level mouse events. The timer redraws only when the screen position differs from the last drawn position.

While external control is enabled for Gliding Cursor, the timer skips position sampling; Gliding Cursor requests its own position updates. Stopping the crosshairs removes the mouse hook and stops both timers.

## Debugging

To debug Mouse Pointer Crosshairs:
- Attach to the PowerToys Runner process directly
- Set breakpoints in the `InclusiveCrosshairs.cpp` file
- Be aware that during debugging, moving the mouse may cause unexpected or "strange" visual behavior because:
  - The mouse hook (`MouseHookProc`) updates the crosshairs position on every `WM_MOUSEMOVE` event
  - This frequent update combined with the debugger's overhead or breakpoints can cause visual glitches or stutters
