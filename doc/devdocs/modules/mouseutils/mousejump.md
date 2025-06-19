# Mouse Jump

Mouse Jump is a utility that allows users to quickly move their cursor to any location on screen using a grid-based overlay interface.

## Implementation

Unlike the other Mouse Utilities that run within the PowerToys Runner process, Mouse Jump operates as a separate process that communicates with the Runner via events.

### Key Files
- `src/modules/MouseUtils/MouseJump` - Contains the Runner interface for Mouse Jump
- `src/modules/MouseUtils/MouseJumpUI` - Contains the UI implementation
- `src/modules/MouseUtils/MouseJumpUI/MainForm.cs` - Main UI form implementation
- `src/modules/MouseUtils/MouseJump.Common` - Shared code between the Runner and UI components

### Enabling Process

When the utility is enabled:

1. A separate UI process is launched for Mouse Jump:
   ```cpp
   void launch_process()
   {
       Logger::trace(L"Starting MouseJump process");
       unsigned long powertoys_pid = GetCurrentProcessId();

       std::wstring executable_args = L"";
       executable_args.append(std::to_wstring(powertoys_pid));

       SHELLEXECUTEINFOW sei{ sizeof(sei) };
       sei.fMask = { SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI };
       sei.lpFile = L"PowerToys.MouseJumpUI.exe";
       sei.nShow = SW_SHOWNORMAL;
       sei.lpParameters = executable_args.data();
       if (ShellExecuteExW(&sei))
       {
           Logger::trace("Successfully started the Mouse Jump process");
       }
       else
       {
           Logger::error(L"Mouse Jump failed to start. {}", get_last_error_or_default(GetLastError()));
       }

       m_hProcess = sei.hProcess;
   }
   ```

2. The Runner creates shared events for communication with the UI process:
   ```cpp
   m_hInvokeEvent = CreateDefaultEvent(CommonSharedConstants::MOUSE_JUMP_SHOW_PREVIEW_EVENT);
   m_hTerminateEvent = CreateDefaultEvent(CommonSharedConstants::TERMINATE_MOUSE_JUMP_SHARED_EVENT);
   ```

### Activation Process

The activation process works as follows:

1. **Shortcut Detection**
   - When the activation shortcut is pressed, the Runner signals the shared event `MOUSE_JUMP_SHOW_PREVIEW_EVENT`

2. **UI Display**
   - The MouseJumpUI process listens for this event and displays a screen overlay when triggered
   - The overlay shows a grid or other visual aid to help select a destination point

3. **Mouse Movement**
   - User selects a destination point on the overlay
   - The UI process moves the mouse cursor to the selected position

4. **Termination**
   - When the utility needs to be disabled or PowerToys is shutting down, the Runner signals the `TERMINATE_MOUSE_JUMP_SHARED_EVENT`
   - The UI process responds by cleaning up and exiting

### User Interface

The Mouse Jump UI is implemented in C# using Windows Forms:
- Displays a semi-transparent overlay over the entire screen
- May include grid lines, quadrant divisions, or other visual aids to help with precision selection
- Captures mouse and keyboard input to allow for selection and cancellation
- Moves the mouse cursor to the selected location upon confirmation

## Debugging

To debug Mouse Jump:

1. Start by debugging the Runner process directly
2. Then attach the debugger to the MouseJumpUI process when it launches
3. Note: Debugging MouseJumpUI directly is challenging because it requires the Runner's process ID to be passed as a parameter at launch

## Community Contributions

Mouse Jump was initially contributed by Michael Clayton (@mikeclayton) and is based on his FancyMouse utility.
