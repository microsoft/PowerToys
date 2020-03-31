
# PowerToys and running as Administrator

## Too long, Didn't Read 😁

If you're running any application as an administrator and PowerToys is not, a few things may not work correctly when the elevated applications are in focus or trying to interact with a PowerToys feature like FancyZones.

## Having PowerToys keep functioning properly

We understand users will run applications elevated. We do as well.  We have two options for you when this scenario happens:

1. **Recommended:** PowerToys will prompt when we detect a process that is elevated.  Click relaunch, accept the UAC prompt and it be running as admin :)
2. Enable "Always run as administrator" in the PowerToys settings.

## What is "Run as Administrator" / Elevated processes

This is when a process runs with "elevated" privileges.  Typically this would be associated with the administrator accounts on a system.

Basically it runs with additional access to the operating system.  Most things do not need run elevated. A common scenario would be needing to run certain PowerShell commands or edit the registry.

How do i know my application is "elevated"?  If you see this prompt (User Access Control prompt), the application is requesting it:

![alt text][uac]

At times also, elevated terminals for instance, they will typically have the phrase "Administrator" appended to the title bar. Be warned, this isn't always the case it will be appended.

![alt text][elevatedWindow]

## When does PowerToys need this

PowerToys in itself does not.  It only needs to be elevated when it has to interact with other applications that are running elevated. If those applications are in focus, PowerToys may not function unless it is elevated as well.

These are the two scenarios we will not work in:

1. Intercepting some styles of keyboard strokes, namely low-level keyboard hooks
2. Resizing / Moving windows

### PowerToys affected

1. FancyZones
   - Snapping a window into a zone
   - Moving the window to a different zone
2. Shortcut guide
   - Display shortcut
3. Keyboard remapper
   - key to key remapping
   - Global level shortcuts remapping
   - App-targeted shortcuts remapping

[uac]: ../images/runAsAdmin/uac.png "User access control (UAC)"
[elevatedWindow]: ../images/runAsAdmin/elevatedWindows.png "Run as admin"
