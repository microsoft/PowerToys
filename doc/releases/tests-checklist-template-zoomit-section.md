[Go back](tests-checklist-template.md)

## ZoomIt

 * Enable ZoomIt in Settings.
   - [ ] Verify ZoomIt tray icon appears in the tray icons, and that when you left-click or right-click, it just shows the 4 action entries: "Break Timer", "Draw", "Zoom" and "Record".
   - [ ] Turn the "Show tray icon" option off and verify the tray icon is gone.
   - [ ] Turn the "Show tray icon" option on and verify the tray icon is back.
 * Test the base modes through a shortcuts:
   - [ ] Press the Zoom Toggle Hotkey and verify ZoomIt zooms in on the mouse. You can exit Zoom by pressing Escape or the Hotkey again.
   - [ ] Press the Live Zoom Toggle Hotkey and verify ZoomIt zooms in on the mouse, while the screen still updates instead of showing a still image. You can exit Live Zoom by pressing the Hotkey again.
   - [ ] Press the Draw without Zoom Hotkey and verify you can draw. You can leave this mode by pressing the Escape.
   - [ ] Select a text file as the Input file for Demo Type, focus notepad and press the Demo Type hotkey. It should start typing the text file. You can exit Demo Type by pressing Escape.
   - [ ] Press the Start Break Timer Hotkey and verify it starts the Timer. You can exit by pressing Escape.
   - [ ] Press the Record Toggle Hotkey to start recording a screen. Press the Record Toggle Hotkey again to exit the mode and save the recording to a file.
   - [ ] Press the Snip Toggle Hotkey to take a snip of the screen. Paste it to Paint to verify a snip was taken.
 * Test some Settings to verify the types are being passed correctly to ZoomIt:
   - [ ] Change the "Animate zoom in and zoom out" setting and activate Zoom mode to verify it applies.
   - [ ] Change the "Specify the initial level of magnification when zooming in" and activate Zoom mode to verify it applies.
   - [ ] Change the Type Font to another font. Enter Break mode to quickly verify the font changed.
   - [ ] Change the Demo Type typing speed and verify the change applies.
   - [ ] Change the timer Opacity for Break mode and verify that the change applies.
   - [ ] Change the timer Position for Break mode and verify that the change applies.
   - [ ] Select a Background Image file as background for Break mode and verify that the change applies.
   - [ ] Turn on "Play Sound on Expiration", select a sound file, aset the timer to 1 minute, activate the Break Mode and verify the sound plays after 1 minute. (Alarm1.wav from "C:\Windows\Media" should be long enough to notice)
   - [ ] Open the Microphone combo box in the Record section and verify it lists your microphones.
 * Test the tray icon actions:
   - [ ] Verify pressing "Break Timer" enters Break mode.
   - [ ] Verify pressing "Draw" enters Draw mode.
   - [ ] Verify pressing "Zoom" enters Zoom mode.
   - [ ] Verify pressing "Record" enters Record mode.