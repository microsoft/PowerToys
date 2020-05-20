# Quick Screen Resolution
An application that allows users to quickly switch between display settings per monitor. Written using WPF with a C++ backend. See the design spec [here](#design-specification). 

## What works and what doesn't
The brightness and resolution can be changed programmatically using the win32 api. 

Most of the functionality cannot be controlled effectively from the front end because we couldn't figure out how to get the interops working. 

## Technical Breakdown Details
### Front End
Developed with WPF in c#.

WPF context menus are used to implement the settings palette. 

Our goal was to add an "on hover" event when the user moused over a monitor name, so that we could provide a visual signal showing which monitor the user was targetting. Unfortunately, since context menus aren't allowed to be associated with multiple events, triggering the event closed the context menu.

WPF popup menus look more promising, but we didn't get a chance to explore the implementation.

### Back End 
Developed with C++.

The back end implements the "Change resolution" and "Change brightness" functionality. It provides an API to the C# frontend to control the settings. 

It uses Windows graphics device interface (GDI) to change the resolution, and it uses the high level Monitor Configuration API to change the brightness. 

#### Vendor Specific Issues
When it comes to changing physical monitor settings, Win32 depends on the monitor vendor following Display Data Channel (DDC/CI VESA) standards for communication between the monitor and the host. https://milek7.pl/ddcbacklight/ddcci.pdf 

It happens that some vendors do not end up following these standards, so Win32 api cannot also provide the same functionality across all devices. Separate implementation strategies would have to be written in those cases to provide the functionality.  

#### Interops
We used P/invoke interops in order to establish communication between the backend and the frontend. This proved to be quite challenging whenever the transfered data was anything more complex than primitive types and simple objects with simple fields. We were not able to transfer arrays of any kind between the two sides without rewiring them entirely. 

## Design Specification

### Overview
#### elevator pitch
John is developing a windows application. To make the application more accessible for users on different devices, he would like to quickly test it under different screen resolutions. On one monitor, John is using his IDE to develop the application, on his second monitor, he loads the application under test, quickly switching the screen resolution from his system tray as he codes.

#### Customer 
Power users who require the ability to quickly change their display settings

#### Problem Statement
Power users need a way to quickly change their display settings so that they can efficiently test apps under different conditions or so that they can consume content such as videos and games at their preferred settings. 

#### Existing Solutions
Users currently have to go to search, type display settings, open the Display menu, then change the settings. Customers will approach the solution with the expectation that it is faster and more convenient then what windows already provides.

#### Definition of Success
Target users of power toys use this tool instead of the traditional display menu to change their display settings.

### Requirements

* Ability to quickly change the following settings fromt the settings tray:
    * screen resolution. 
    * monitor brightness
    * DPI 
    * darkmode
* Settings can be switched per monitor
* Signal which monitor the user is targetting on the context menu

Our implementation focused on quickly changing the screen resolution and brightness, but this menu can be extended to support other potentially more valuable features combinations.



