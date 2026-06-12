# PDF Fullscreen Toggle Fix - Architecture

## Event Flow Diagram

```
User clicks PDF fullscreen button
              ↓
    WebView2 (CoreWebView2)
    ContainsFullScreenElement = true/false
              ↓
    ContainsFullScreenElementChanged event fires
              ↓
┌─────────────────────────────────────────────────┐
│  BrowserControl.xaml.cs                         │
│  CoreWebView2_ContainsFullScreenElementChanged  │
│    → FullScreenChanged?.Invoke(isFullScreen)    │
└─────────────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────────────┐
│  FilePreview.xaml.cs                            │
│  BrowserPreview_FullScreenChanged               │
│    → FullScreenChanged?.Invoke(this, ...)       │
└─────────────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────────────┐
│  MainWindow.xaml.cs                             │
│  FilePreviewer_FullScreenChanged                │
│    → AppWindow.SetPresenter(FullScreen/Default) │
└─────────────────────────────────────────────────┘
              ↓
         Window toggles
    FullScreen ↔ Windowed mode
```

## Component Responsibilities

### BrowserControl
- **Role**: WebView2 wrapper, direct interface with CoreWebView2
- **Responsibility**: Listen to WebView2 events, expose them as custom events
- **Key Methods**:
  - `CoreWebView2_ContainsFullScreenElementChanged()` - Event handler
  - Raises `FullScreenChanged` event with boolean parameter

### FilePreview
- **Role**: Preview coordinator, manages different preview types
- **Responsibility**: Route events from preview controls to main window
- **Key Methods**:
  - `BrowserPreview_FullScreenChanged()` - Event handler
  - Propagates `FullScreenChanged` event upward

### MainWindow
- **Role**: Application window, manages window-level behavior
- **Responsibility**: Handle window presentation mode changes
- **Key Methods**:
  - `FilePreviewer_FullScreenChanged()` - Event handler
  - Calls `AppWindow.SetPresenter()` to change window mode

## Key APIs Used

### CoreWebView2.ContainsFullScreenElementChanged
- **Type**: Event
- **When**: Fires when PDF viewer's fullscreen button is clicked
- **Property**: `CoreWebView2.ContainsFullScreenElement` (bool)
- **Documentation**: Part of Microsoft.Web.WebView2.Core namespace

### AppWindow.SetPresenter()
- **Type**: Method
- **Parameter**: `AppWindowPresenterKind` enum
- **Values**:
  - `AppWindowPresenterKind.FullScreen` - True fullscreen, hides titlebar
  - `AppWindowPresenterKind.Default` - Normal overlapped window
- **Documentation**: Part of Microsoft.UI.Windowing namespace (WinUI 3)

## Design Decisions

### Why Event Propagation?
Instead of directly accessing MainWindow from BrowserControl:
1. **Separation of Concerns**: BrowserControl shouldn't know about MainWindow
2. **Reusability**: BrowserControl can be used in other contexts
3. **Testability**: Each component can be tested independently
4. **Maintainability**: Clear data flow, easy to debug

### Why AppWindow.SetPresenter?
Alternative approaches considered:
1. ~~Manual hiding of titlebar~~ - Complex, doesn't handle all edge cases
2. ~~Maximize window~~ - Not true fullscreen, taskbar still visible
3. **AppWindow.SetPresenter** ✓ - Native WinUI 3 API, handles everything correctly

### Why Not Just Hide TitleBar?
Setting `ExtendsContentIntoTitleBar = false` and hiding the titlebar isn't sufficient:
- Window still has borders
- Taskbar remains visible
- Window doesn't cover entire screen
- Need to manually restore all settings when exiting

`AppWindowPresenterKind.FullScreen` handles all of this automatically.

## Edge Cases Handled

1. **Rapid toggling**: Events are properly subscribed/unsubscribed
2. **Navigation between files**: Each file gets its own fullscreen state
3. **Escape key**: Existing behavior preserved (closes Peek)
4. **Non-PDF files**: Event only fires for PDF viewer, others unaffected
5. **Dispose/cleanup**: Properly unsubscribe from events to prevent leaks

## Future Enhancements

Potential improvements for consideration:
1. **Remember fullscreen preference**: Store user's last fullscreen state
2. **Keyboard shortcut**: Add F11 or similar to toggle fullscreen
3. **Context menu option**: Right-click menu item for fullscreen
4. **Status indicator**: Show fullscreen status in UI
5. **Per-file-type settings**: Remember fullscreen preference per file type

## Related Issues

This fix addresses:
- [Peek] How do I un-fullscreen a PDF? (Main issue)
- Any future WebView2 fullscreen requirements in PowerToys
- Pattern for handling WebView2 element state changes

## References

- WebView2 API: https://learn.microsoft.com/en-us/microsoft-edge/webview2/
- WinUI 3 AppWindow: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow
- PowerToys Peek: https://github.com/microsoft/PowerToys/tree/main/src/modules/peek
