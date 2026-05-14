# Testing Notes for PDF Fullscreen Toggle Fix

## Issue
[Peek] How do I un-fullscreen a PDF? (#Issue number to be filled)

## Summary of Changes
Fixed the PDF fullscreen toggle functionality in Peek. When viewing PDFs in Peek, clicking the fullscreen button now properly toggles between fullscreen and windowed modes.

## Files Changed
1. `src/modules/peek/Peek.FilePreviewer/Controls/BrowserControl.xaml.cs`
   - Added `FullScreenChangedHandler` delegate and `FullScreenChanged` event
   - Subscribed to `CoreWebView2.ContainsFullScreenElementChanged` event
   - Added event handler `CoreWebView2_ContainsFullScreenElementChanged`

2. `src/modules/peek/Peek.FilePreviewer/FilePreview.xaml`
   - Wired up `FullScreenChanged` event handler to BrowserControl

3. `src/modules/peek/Peek.FilePreviewer/FilePreview.xaml.cs`
   - Added `FullScreenChanged` event
   - Added `BrowserPreview_FullScreenChanged` event handler to propagate state

4. `src/modules/peek/Peek.UI/PeekXAML/MainWindow.xaml`
   - Wired up `FullScreenChanged` event handler to FilePreview

5. `src/modules/peek/Peek.UI/PeekXAML/MainWindow.xaml.cs`
   - Added `FilePreviewer_FullScreenChanged` event handler
   - Toggles between `AppWindowPresenterKind.FullScreen` and `AppWindowPresenterKind.Default`

## Manual Testing Steps

### Prerequisites
1. Build PowerToys with the changes
2. Have a PDF file ready for testing

### Test Case 1: Enter and Exit Fullscreen
1. Open File Explorer
2. Select a PDF file
3. Press `Ctrl+Space` to open Peek
4. Verify PDF is displayed correctly
5. **Click the fullscreen button in the PDF viewer** (usually in bottom-right corner)
6. **EXPECTED**: Window enters fullscreen mode, titlebar disappears
7. **Click the fullscreen button again**
8. **EXPECTED**: Window exits fullscreen mode, titlebar reappears

### Test Case 2: Escape Key Still Works
1. Open a PDF in Peek
2. Click the fullscreen button to enter fullscreen
3. Press `Escape` key
4. **EXPECTED**: Window exits fullscreen AND Peek closes (existing behavior)

### Test Case 3: Multiple Toggle Cycles
1. Open a PDF in Peek
2. Click fullscreen button (enter fullscreen)
3. Click fullscreen button (exit fullscreen)
4. Click fullscreen button (enter fullscreen again)
5. Click fullscreen button (exit fullscreen again)
6. **EXPECTED**: All toggles work correctly without any stuck states

### Test Case 4: Navigation Between Files
1. Select multiple PDF files in File Explorer
2. Open Peek with `Ctrl+Space`
3. Enter fullscreen mode
4. Use arrow keys to navigate to next/previous PDF
5. **EXPECTED**: Fullscreen state persists across file navigation

### Test Case 5: Non-PDF Files
1. Open a non-PDF file (e.g., image, text file) in Peek
2. **EXPECTED**: No fullscreen button appears, behavior unchanged

## Expected Behavior

### Before Fix
- Clicking fullscreen button hides titlebar but doesn't properly put window in fullscreen
- Clicking fullscreen button again does nothing (stuck in pseudo-fullscreen)
- Only way to exit is pressing Escape (which closes Peek entirely)

### After Fix
- Clicking fullscreen button properly enters fullscreen mode
- Titlebar automatically hides
- Window fills entire screen
- Clicking fullscreen button again exits fullscreen
- Titlebar reappears
- Window returns to previous size/position

## Technical Details

The fix leverages WebView2's `ContainsFullScreenElementChanged` event which fires when:
1. User clicks the PDF viewer's fullscreen button (enters fullscreen)
2. User clicks the fullscreen button again (exits fullscreen)

The event chain is:
1. `CoreWebView2.ContainsFullScreenElementChanged` fires in BrowserControl
2. BrowserControl raises `FullScreenChanged` event
3. FilePreview propagates the event to MainWindow
4. MainWindow calls `AppWindow.SetPresenter()` with appropriate presenter kind

## Verification
After testing, verify:
- [ ] PDF fullscreen button enters fullscreen mode
- [ ] PDF fullscreen button exits fullscreen mode (toggle works)
- [ ] Titlebar visibility toggles correctly
- [ ] Window size changes correctly
- [ ] Escape key still closes Peek when in fullscreen
- [ ] Navigation between PDFs maintains fullscreen state
- [ ] No regressions with other file types
