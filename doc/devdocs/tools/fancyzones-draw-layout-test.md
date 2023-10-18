# [FancyZones_DrawLayoutTest](/tools/FancyZones_DrawLayoutTest/)

This test tool is created in order to debug issues related to the drawing of zone layout on screen.

Currently, only column layout is supported with modifiable number of zones. Pressing **w** key toggles zone appearance on primary screen (multi monitor support not yet in place). Pressing **q** key exits application.

Application is DPI unaware which means that application does not scale for DPI changes and it always assumes to have a scale factor of 100% (96 DPI). Scaling will be automatically performed by the system.
