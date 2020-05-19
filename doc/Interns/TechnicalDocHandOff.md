# Multi-monitor feature
## How to use
- Select the display in the dropdown menu in the editor
- Select layout
- Press apply
## Design
- We decided to maintain how layouts are applied in v0.18 and just pass a different device id based on the dropdown menu (This allows virtual desktops to be treated as different displays)
- We build a HashMap that is based on zone-settings.json that contains the display number vs device uuid 
## Things left to do before merging to production
- Default the drop down menu to the focused monitor
- Implement the identify button that allows users to know what Display x or Flash the layout on selected display
- Defensive measures against bad user input
- Unit tests

More details https://github.com/microsoft/PowerToys/pull/2921

# Hotkey feature
## How to use
- Create a new custom layout (canvas or editing existing grid layout)
- Input 0-9 in respective hotkey textbox
- Press apply
- Press on Alt + 0-9 to apply layout on monitor in focus
## Design
- We store the hotkey id + event id in zone-settings.json. This allows the user to dynamically set hotkeys on desired layouts and persist it upon restart. (We attempted using existing HotKeyObject but ran into serious issues persisting data)
## Things left to do before merging to production
- Allow users to specify entire hotkey not just digit appending to "Alt" (right now it is mapped to alt + 0-9)
- Update hotkey configuration registration on proper update event (right now the mapping is updated when the editor is toggled event)
- Update notification messages for mismatch of screen resolution
- Generate unique event ids for hotkey events (right now we have it on random int)
- Checking custom hotkey selection for overwriting existing hotkeys
- Defensive measures against bad user input
- Unit tests

More details https://github.com/microsoft/PowerToys/pull/3174
