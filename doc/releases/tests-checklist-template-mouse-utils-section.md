[Go back](tests-checklist-template.md)

## Mouse Utils

Find My Mouse:
  * Enable FindMyMouse. Then, without moving your mouse:
    - [ ] Press Left Ctrl twice and verify the overlay appears.
    - [ ] Press any other key and verify the overlay disappears.
    - [ ] Press Left Ctrl twice and verify the overlay appears.
    - [ ] Press a mouse button and verify the overlay disappears.
  * Disable FindMyMouse. Verify the overlay no longer appears when you press Left Ctrl twice.
  * Enable FindMyMouse. Then, without moving your mouse:
    - [ ] Press Left Ctrl twice and verify the overlay appears.
  * Enable the "Do not activate on game mode" option. Start playing a game that uses CG native full screen.
    - [ ] Verify the overlay no longer appears when you press Left Ctrl twice.
  * Disable the "Do not activate on game mode" option. Start playing the same game.
    - [ ] Verify the overlay appears when you press Left Ctrl twice. (though it'll likely minimize the game)
  * Test the different settings and verify they apply:
    - [ ] Overlay opacity
    - [ ] Background color
    - [ ] Spotlight color
    - [ ] Spotlight radius
    - [ ] Spotlight initial zoom (1x vs 9x will show the difference)
    - [ ] Animation duration
    - [ ] Change activation method to shake and activate by shaking your mouse pointer
    - [ ] Excluded apps

Mouse Highlighter:
  * Enable Mouse Highlighter. Then:
    - [ ] Press the activation shortcut and press left and right click somewhere, verifying the hightlights are applied.
    - [ ] With left mouse button pressed, drag the mouse and verify the hightlight is dragged with the pointer.
    - [ ] With right mouse button pressed, drag the mouse and verify the hightlight is dragged with the pointer.
    - [ ] Press the activation shortcut again and verify no highlights appear when the mouse buttons are clicked.
    - [ ] Disable Mouse Highlighter and verify that the module is not activated when you press the activation shortcut.
  * Test the different settings and verify they apply:
    - [ ] Change activation shortcut and test it
    - [ ] Left button highlight color
    - [ ] Right button highlight color
    - [ ] Opacity
    - [ ] Radius
    - [ ] Fade delay
    - [ ] Fade duration
    - [ ] Hilight mode switch to "Spotlight", verify there is a transparent circle and outside it is colored with "Always highlight color", and make sure performance is ok.

Mouse Pointer Crosshairs:
  * Enable Mouse Pointer Crosshairs. Then:
    - [ ] Press the activation shortcut and verify the crosshairs appear, and that they follow the mouse around.
    - [ ] Press the activation shortcut again and verify the crosshairs disappear.
    - [ ] Disable Mouse Pointer Crosshairs and verify that the module is not activated when you press the activation shortcut.
  * Test the different settings and verify they apply:
    - [ ] Change activation shortcut and test it
    - [ ] Crosshairs color
    - [ ] Crosshairs opacity
    - [ ] Crosshairs center radius
    - [ ] Crosshairs thickness
    - [ ] Crosshairs border color
    - [ ] Crosshairs border size

Mouse Jump:
  * Enable Mouse Jump. Then:
    - [ ] Press the activation shortcut and verify the screens preview appears.
    - [ ] Change activation shortcut and verify that new shorctut triggers Mouse Jump.
    - [ ] Click around the screen preview and ensure that mouse cursor jumped to clicked location.
    - [ ] Reorder screens in Display settings and confirm that Mouse Jump reflects the change and still works correctly.
    - [ ] Change scaling of screens and confirm that Mouse Jump still works correctly.
    - [ ] Unplug additional monitors and confirm that Mouse Jump still works correctly.
    - [ ] Disable Mouse Jump and verify that the module is not activated when you press the activation shortcut.
