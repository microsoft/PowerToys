[Go back](tests-checklist-template.md)

## Workspaces
* Settings
   - [x] Launch the Editor by clicking the button on the settings page.
   - [x] Launch the Editor from quick access.
   - [x] Launch the Editor by the Activation shortcut.
   - [x] Disable the module and and verify it won't launch by the shortcut.

* Snapshot tool: try with elevated and non-elevated PT
   * Open non-packaged apps, e.g., VisualStudio, VisualStudioCode.
   * Open packaged apps, e.g., Notepad, Settings.
   * Run any app as an administrator.
   * Minimize any app.
   * Click `Create Workspace`.
   * Open any other window.
   * Click `Capture`
   - [x] Verify Editor shows all opened windows (the elevated window will be captured if PT is also elevated).
   - [x] Verify windows are in the correct positions.
   - [ ] Verify elevated app has the `Admin` box checked (if captured).

* Editor
   - [x] Verify that the new Workspace appears in the list after capturing.
   - [x] Verify that the new Workspace doesn't appear after canceling the Capture.
   - [x] Verify `Search` filters Workspaces (by workspace name or app name).
   - [x] Verify `SortBy` works.
   - [x] Verify `SortBy` keeps its value when you close and open the editor again.
   - [x] Verify `Remove` removes the Workspace from the list.
   - [x] Verify `Edit` opens the Workspace editing page.
   - [x] Verify clicking at the Workspace opens the Workspace editing page.
   
   * Editing page
   - [x] `Remove` an app and verify it disappeared on the preview.
   - [x] `Remove` and `Add back` an app, verify it's returned back to the preview.
   - [x] Set an app minimized, check the preview.
   - [x] Set an app maximized, check the preview.
   - [ ] Check `Launch as admin` for the app where it's available.
   - [ ] Add CLI args, e.g. path to the PowerToys.sln file for VisualStudio.
   - [ ] Manually change the position for the app, check the preview.
   - [x] Change the Workspace name.
   - [x] Verify `Save` and `Cancel` work as expected.
   - [x] Change anything in the project, click at the `Workspaces` on the top of the page, and verify you returned to the main page without saving any changes.
   - [x] Check `Create desktop shortcut`, save the project, verify the shortcut appears on the desktop.
   - [ ] Verify that `Create desktop shortcut` is checked when the shortcut is on the desktop and unchecked when there is no shortcut on the desktop.
   - [x] Click `Launch and Edit`, wait for the apps to launch, click `Capture`, verify opened apps are added to the project.

* Launcher
   - [x] Click `Launch` in the editor, verify the Workspace apps launching.
   - [ ] Launch Workspace by a shortcut, verify the Workspace apps launching.
   - [ ] Verify a window with launching progress is shown while apps are launching and presents the correct launching state (launching, launched, not launched) for every app.
   - [x] Click `Cancel launch`, verify launching is stopped at the current state (opened apps will stay opened), and the window is closed.
   - [x] Click `Dismiss` and verify apps keep launching, but the LauncherUI window is closed.
   
* To verify that the launcher works correctly with different apps, try to capture and launch:  
   - [ ] Non-packaged app, e.g., VisualStudio code
      - [ ] As admin
      - [ ] With CLI args
    - [ ] Packaged app, e.g. Terminal
      - [ ] As admin
      - [ ] With CLI args

* Try to launch the Workspace with a different setup
   - [ ] Create a Workspace with one monitor connected, connect the second monitor, launch the Workspace, verify apps are opened on the first one, as captured.
   - [ ] Create a Workspace with two monitors connected, disconnect a monitor, verify apps are opened on the remaining one.
