# Mouse Highlighter

Mouse Highlighter is a utility that visualizes mouse clicks by displaying a highlight effect around the cursor when clicked.

## Implementation

Mouse Highlighter runs within the PowerToys Runner process and draws visual indicators (typically circles) around the mouse cursor when the user clicks.

### Key Files
- `src/modules/MouseUtils/MouseHighlighter/MouseHighlighter.cpp` - Contains the main implementation
- Key function: `WndProc` - Handles window messages and mouse events

### Enabling Process

When the utility is enabled:

1. A background thread is created to run the mouse highlighter logic asynchronously:
   ```cpp
   std::thread([=]() { MouseHighlighterMain(m_hModule, m_highlightSettings); }).detach();
   ```

2. The Highlighter instance is initialized and configured with user settings:
   ```cpp
   Highlighter highlighter;
   Highlighter::instance = &highlighter;
   highlighter.ApplySettings(settings);
   highlighter.MyRegisterClass(hInstance);
   ```

3. A highlighter window is created:
   ```cpp
   instance->CreateHighlighter();
   ```

4. The utility:
   - Registers a custom window class 
   - Creates a transparent window for drawing visuals
   - Handles the `WM_CREATE` message to initialize the Windows Composition API (Compositor, visuals, and target)

### Activation Process

The activation process works as follows:

1. **Shortcut Detection**
   - The system detects when the activation shortcut is pressed
   - A global hotkey listener (registered with `RegisterHotKey` or similar hook) detects the shortcut

2. **Message Transmission**
   - A message (like `WM_SWITCH_ACTIVATION_MODE`) is sent to the highlighter window via `PostMessage()` or `SendMessage()`

3. **Window Procedure Handling**
   - The `WndProc` of the highlighter window receives the message and toggles between start and stop drawing modes:
     ```cpp
     case WM_SWITCH_ACTIVATION_MODE:
         if (instance->m_visible)
             instance->StopDrawing();
         else
             instance->StartDrawing();
     ```

4. **Drawing Activation**
   - If turning ON, `StartDrawing()` is called, which:
     - Moves the highlighter window to the topmost position
     - Slightly offsets the size to avoid transparency bugs
     - Shows the transparent drawing window
     - Hooks into global mouse events
     - Starts drawing visual feedback around the mouse

   - If turning OFF, `StopDrawing()` is called, which:
     - Hides the drawing window
     - Removes the mouse hook
     - Stops rendering highlighter visuals

### Drawing Process

When the mouse highlighter is active:
1. A low-level mouse hook detects mouse button events
2. On click, the highlighter draws a circle (or other configured visual) at the cursor position
3. The visual effect fades over time according to user settings
4. Each click can be configured to show different colors based on the mouse button used

## Debugging

To debug Mouse Highlighter:
- Attach to the PowerToys Runner process directly
- Set breakpoints in the `MouseHighlighter.cpp` file
- Be aware that visual effects may appear different or stuttery during debugging due to the debugger's overhead

## Known Issues

- There is a reported bug where the highlight color stays on after toggling opacity to 0
- This issue has been present for more than six months and can still be reproduced in recent PowerToys releases
