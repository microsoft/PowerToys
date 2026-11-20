# Sound Manager

[Public overview - Microsoft Learn](https://learn.microsoft.com/en-us/windows/powertoys/sound-manager)

## Quick Links

[All Issues](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Sound%20Manager%22)<br>
[Bugs](https://github.com/microsoft/PowerToys/issues?q=is%3Aopen%20label%3A%22Product-Sound%20Manager%22%20label%3AIssue-Bug)<br>
[Pull Requests](https://github.com/microsoft/PowerToys/pulls?q=is%3Apr+is%3Aopen++label%3A%22Product-Sound+Manager%22)

## Overview

Sound Manager is a PowerToys module with an audio mixer of its own. The window opens on one hotkey,
shows what is making noise right now, and lets any of it be changed from there.

Windows 11 ships per-application audio controls in **Settings → System → Sound → Volume mixer**.
That page covers per-application volume, mute, output device and input device, and it remembers all
of it per executable path across restarts. A fair amount of this module therefore runs alongside a
feature Windows already ships. What Windows still does not do is the reason the module exists:

- **No level meters.** Nothing shows which application is making noise and how loud.
- **No dedicated mixer window.** The mixer is a page inside Settings. `Win+Ctrl+V` opens the Quick
  Settings sound panel, which is closer, but it is a system flyout and it shows no meters.
- **The default device is on another page.** Choosing a device, making it the default, and making it
  the default for calls all live on the Sound page, one device at a time.
- **Output and input devices are in two different places**, so they cannot be seen together.
- **Silent applications drop off.** Windows lists an application only once it has actually produced
  audio, so a running application with a live but quiet session is invisible.

The old `sndvol.exe` mixer is still there too, in the small Win32 dialog it has always been.

## Features

| Feature | Why Windows cannot copy it |
|---|---|
| Live per-application level meters | Nothing in Windows shows who is making noise right now |
| A dedicated window on a hotkey | The mixer is a page in Settings; `Win+Ctrl+V` is a system flyout |
| Output and input devices in one window | Windows splits them across two pages |
| Applications that stay listed while silent | Windows drops an application once it stops making audio |
| The default device, and the device for calls, in one click | Windows buries both on the Sound page, one device at a time |
| A shortcut of its own for every program | The Windows mixer has no per-program hotkey at all |
| Per-application left and right balance | Windows has no per-application balance control |
| Mono for one application or one device | Windows has no Mono for a program, and cannot fold one program's stereo into a single channel |
| Play a microphone through the speakers | Reachable only through the Listen tab of the old Sound dialog |
| A test sound for a device | The old Sound dialog only plays the Windows sound while you pick a device |
| What Windows does with the other sounds during a call | Buried under Sound → Output as "Adaptive communication sound levels" |

The window is a normal floating window, opened by the hotkey, from the **Open Sound Manager** button on
the settings page, or from the Quick access list on the Home page. It is not a panel that slides up
from the taskbar and it is not always on top, so it behaves like every other window on the desktop.

## Architecture

The module follows the usual PowerToys pattern: a C++ module-interface DLL that owns the audio
engine, and a WinUI 3 C# app that is a thin view over it.

```
src/modules/SoundManager/
├── SoundManagerModuleInterface/     # C++ DLL implementing PowertoyModuleIface
│   ├── dllmain.cpp                  # powertoy_create() export, enable/disable, hotkeys
│   ├── audio_engine.h/.cpp          # WASAPI sessions and devices, volume, meters, routing
│   └── pipe_server.h/.cpp           # named pipe that carries the mixer state
└── SoundManager/                    # WinUI 3 C# app — the mixer window
    ├── Program.cs                   # activation handoff between copies of the app
    ├── MixerPipeClient.cs           # the pipe client and the snapshot it renders
    ├── SoundManagerXAML/MainWindow.xaml(.cs)
    ├── ViewModels/AudioRowViewModel.cs   # balance, Mono, the expanding row
    └── ViewModels/AppViewModel.cs
```

The settings page lives in the settings window like every other module. The icons of the programs
are read by code in `src/common/Common.UI.Controls/Icons/`, because both the mixer window and the
settings page need them.

### Communication between the DLL and the app

The module interface owns the audio engine and stays resident for the life of the module. The C#
app is a view over it. They talk over a **named pipe**, the same mechanism the Advanced Paste and
Power Display modules use.

- The pipe name is `PowerToys-SoundManager-<runner process id>`, so a development build and an
  installed copy of PowerToys never talk to each other.
- Every message is a **single JSON document on its own line**. JSON never contains a raw newline, so
  one line is one message. The module interface builds the state with the WinRT JSON API that
  `src/common/utils/json.h` wraps, the app parses it with `System.Text.Json`.
- A **snapshot** (`{"command":"get_snapshot"}`) carries the current devices and sessions. It has to
  enumerate every endpoint and every session, so it is asked for every 300 ms.
- The **peak levels** travel on a separate request (`{"command":"get_peaks"}`) that only reads the
  levels of the sessions the module already knows. That is cheap enough to ask for every 60 ms,
  which is what keeps the meters moving.
- **Changes travel the other way** as commands, for example
  `{"command":"set_app_volume","processId":1234,"sessionId":"…","value":0.5}`.
- A command that is missing a value is **refused** rather than carried out with a default.
- Reading from the pipe blocks, so the app does it on a background thread and hands the result to
  the window through its `DispatcherQueue`.

**Optimistic changes.** A change the user makes is shown right away and remembered until the module
interface confirms it. Without this the control jumps back to the old value every time a snapshot
arrives that was taken before the change reached Windows, which looks like the control is fighting
the user. A change that is refused puts the old value back and says so.

### Settings persistence

- **Path:** `%LOCALAPPDATA%\Microsoft\PowerToys\Sound Manager\settings.json`
- **C++:** `PowerToysSettings::PowerToyValues::load_from_settings_file` from
  `src/common/SettingsAPI/settings_objects.h`. The key is the module key, not the display name.
- **C#:** `SettingsUtils.Default` with `GetSettingsPath(powertoy, fileName)`.
- **Shape:** `{ "name": …, "version": "1.0", "properties": { "<Prop>": { "value": … } } }`.

The file holds the shortcuts only. The volume, the mute state, the balance and the routing of an
application are **not** kept here — they are written to the Windows store, so the Sound Manager and
the Windows mixer cannot disagree.

### Hotkeys and the Open button

The shortcut that opens the window is set on the settings page and stored in the module's own
settings file; the module interface reads it in `set_config()`.

The module uses the **list** form of the hotkey API — `get_hotkeys()` and `on_hotkey(hotkeyId)` —
and not the older `GetHotkeyEx()`. It matters: the runner keeps a map of every shortcut of every
module so that it can find the clashes, and `UpdateHotkeyEx()` takes the module out of that map, so
a module on the old API would never have a clash reported for its own shortcuts. With the list form
the runner numbers every shortcut of the module, and the number is the place of the shortcut in the
list. The list holds the shortcut that opens the window first and the shortcuts of the programs
after it; `on_hotkey(0)` opens the window and `on_hotkey(1)` and up silence the program that belongs
to that place in the list.

The settings page also has an **Open** button, which sends the custom action `open-sound-manager`
to the runner. The Quick access entry on the Home page sends the same action, so there is exactly
one way to open the mixer from outside the module.

Both the button and the shortcut open the window, or bring it to the front when it is already open.
Bringing it to the front is less simple than it looks. The runner is a background process, and the
request usually comes from the settings window, which owns the foreground, so Windows refuses a
`SetForegroundWindow` from a process that is not the foreground one. The module interface joins the
input queues of the foreground window and of its own thread for a moment, the way the runner already
does it for its bug report dialog. A window that was minimized is restored first.

### Registering the module

Like every new module, this one touches shared files outside its own folder. The procedure is in
`doc/devdocs/development/new-powertoy.md`.

| Area | Files |
|---|---|
| Runner | `src/runner/main.cpp` (known modules), `src/runner/settings_window.h` and `.cpp` (the page name) |
| Shared | `src/common/ManagedCommon/ModuleType.cs`, `src/common/utils/gpo.h`, `src/common/GPOWrapper/GPOWrapper.idl` / `.h` / `.cpp`, `src/common/logger/logger_settings.h` |
| Settings library | `Settings.UI.Library/EnabledModules.cs`, `Helpers/ModuleHelper.cs`, `SettingsSerializationContext.cs` |
| Settings window | `Settings.UI/Helpers/ModuleGpoHelper.cs` (both copies), `SettingsXAML/App.xaml.cs`, `SettingsXAML/Views/ShellPage.xaml`, `ViewModels/DashboardViewModel.cs` |
| Quick access | `Settings.UI.Controls/QuickAccess/*`, `QuickAccess.UI/Services/*` |
| First run | `OOBE/Enums/PowerToysModules.cs`, `OOBE/ViewModel/OobeShellViewModel.cs`, `SettingsXAML/OobeWindow.xaml(.cs)`, `SettingsXAML/OOBE/Views/OobeSoundManager.xaml(.cs)` |
| Installer | `installer/PowerToysSetupVNext/SoundManager.wxs`, `Product.wxs`, `generateAllFileComponents.ps1`, `PowerToysInstallerVNext.wixproj`, `CustomAction.cpp` |
| Signing and build | `.pipelines/ESRPSigning_core.json`, `PowerToys.slnx` |

The group policy `ConfigureEnabledUtilitySoundManager` runs from `gpo.h` through the GPO wrapper to
the module, so an administrator can switch the module off for everybody on the machine.

## Implementation Details

### The Windows audio APIs

Everything the module does is standard WASAPI, with one exception. Each of these was compiled and
run on the machine this was built on, before any of the module was written.

| Capability | API |
|---|---|
| Enumerate output and input devices | `IMMDeviceEnumerator` |
| Enumerate application sessions | `IAudioSessionManager2` → `IAudioSessionEnumerator` |
| Tell active from idle applications | `IAudioSessionControl::GetState` |
| Volume and mute of a session | `ISimpleAudioVolume` |
| **Level meters** | `IAudioMeterInformation::GetPeakValue` |
| **Left and right balance of a session** | `IChannelAudioVolume::Get/SetChannelVolume` |
| **Left and right balance of a device** | `IAudioEndpointVolume::Get/SetChannelVolumeLevelScalar` |
| **Routing of an application** | `Windows.Media.Internal.AudioPolicyConfig` |

The parts that are easy to get wrong:

- `IAudioSessionManager2` lives in `<audiopolicy.h>`, not `<audioclient.h>`, and it is not pulled in
  transitively. `IAudioMeterInformation` lives in `<endpointvolume.h>`.
- `CoInitializeEx` is mandatory. `CoCreateInstance(MMDeviceEnumerator)` fails with `0x800401F0`
  (class not available) if COM is not initialised first, which is a misleading error.
- The `AudioPolicyConfig` WinRT class name is **40 characters**, not the 38 that appears in older
  write-ups. `WindowsCreateString` takes an explicit length, so a wrong constant silently produces
  an empty HSTRING and `RoGetActivationFactory` fails with `0x80040154`.
- The **vtable layout has to be exactly right.** The 21H2 and later interface
  (`ab3d4648-e242-459f-b02f-541c70306324`) is
  `IUnknown(3) + IInspectable(3) + 19 × __incomplete__ + Set + Get + Clear`. With 18 or 20
  placeholders `Set` silently calls `Get`. The downlevel variant uses a **different IID**
  (`2a59116d-6c4f-45e0-a74f-707e3fef9258`) with the same method count.
- `Set` takes an HSTRING device id and `Get` returns an HSTRING. Passing a raw `LPCWSTR` produces
  `E_INVALIDARG` on restore.
- The device id handed to the policy API is **not** `IMMDevice::GetId()`. It has to be reformatted
  into the device-interface path
  `\\?\SWD#MMDEVAPI#<device-id>#{e6327cad-dcec-4949-ae8a-991e976a79d2}`.

`Windows.Media.Internal.AudioPolicyConfig` is **undocumented**. It is the interface the Windows
volume mixer uses itself and it has been stable since Windows 10 1803, but it is not a public
contract, and a reviewer will treat it as a risk. It sits behind one small C++ class, so if a
public replacement ever ships only that one file has to change. If the activation fails the module
falls back to volume, mute and meters, and the routing control is hidden instead of throwing.

### Sharing state with the Windows mixer

Windows keeps two stores of per-application audio state. Both are written by the same undocumented
component that the Settings mixer uses.

**The routing** lives in `HKCU\Software\Microsoft\Multimedia\Audio\DefaultEndpoint\<hash>_0`. The
`(Default)` value holds the full path of the application, `<role>_<index>` holds the endpoint the
application is pinned to (`001_000` is the multimedia role, `000_000` is the console role), and
`<role>_<index>_p` holds a GUID. This store is written by
`Windows.Media.Internal.AudioPolicyConfig` → `SetPersistedDefaultAudioEndpoint`, which is the same
call the module already makes, so **the routing lands in the Windows store by itself**.

**The volume, the mute state and the balance** live in
`HKCU\Software\Microsoft\Internet Explorer\LowRegistry\Audio\PolicyConfig\PropertyStore\<hash>_0`.
The `(Default)` value holds `{2}.<device-interface-id>|<application-path>%b{<guid>}` and the
subkey `{219ED5A0-9CBF-4F3A-B927-37C9E5C5F14F}` holds three binary values: `3` is the scalar
volume, `4` the per-channel volumes, and `5` the mute state. The key is per **device and
application together**.

The Windows store is therefore the **single source of truth**, and the module keeps no file of its
own. Windows persists the volume of a session when the session ends, so the number the module
writes through `ISimpleAudioVolume` becomes the number Windows keeps, and the two windows agree by
construction. Where a stored value cannot be read back, the window says so rather than guessing.

Looking at that store turned up two defects in the module. Both are fixed:

- The routing has to be written for **both** roles, `eMultimedia` and `eConsole`, so that the
  application is pinned whichever role the system reads back.
- Clearing the override does not move audio that is already playing, so choosing "System default"
  has to move the live session as well.

There is **no public API that reads the persisted per-application volume** — it appears to exist
only in the registry blobs above — so reading it back is best effort.

### The balance and Mono

Windows cannot fold another program's stereo into mono, so the word can only mean one thing here:
both ears at the same level. What makes it worth having is what happens when it is switched off
again. Mono on sets both ears to the average of the two levels and remembers the pan that was
there; Mono off puts the remembered pan back. While Mono is on the slider sits in the middle and
does nothing, because a pan and mono together would contradict each other.

**The mathematics.** The slider is a position, not a level. Moving it keeps the louder ear where it is
and moves the other one, because a pan that walks both ears together sounds like the volume
changing rather than like the sound moving. The position is `(right - left) / max * 100`, so 0 is
the centre and -100 is everything on the left. The slider carries the whole of that range: its
value runs from 0 to 100, so the control never holds a negative number and the two ends of the
track are the two ears.

The row carries a **badge** that says where the sound sits: a pill that says `Mono`, or a small track
with a dot on it. The dot travels on the same scale as the slider, so the two always point at the
same place. The badge only shows when the sound is not in the middle.

Two things had to be measured to line the controls up. The WinUI slider draws its track about four
and a half pixels **above** the middle of the control, so it is pushed down by five pixels, and the
pill of the Mono switch sat one pixel higher than the word beside it, so that one is pushed down by
one.

### The microphone that is played through the speakers

Windows can play a microphone through the speakers. It is the **Listen to this device** box of the
Listen tab of the old Sound dialog, and it is the only way to hear what a microphone is picking up
without recording it. The module puts it on every microphone row as a button, and the sound goes
to the default output device of Windows.

**It is a setting of the device, not of a program.** Every program that records from that
microphone is heard, which is what the setting means and what the old dialog does.

Windows keeps it in the property store of the endpoint, under a key of its own that is not in the
public SDK:

| Key | What it holds |
|---|---|
| `{24DBB0FC-9311-4B3D-9CF0-18FF155639D4}, 1` | Whether the microphone is played through the speakers |
| `{24DBB0FC-9311-4B3D-9CF0-18FF155639D4}, 0` | The output device it is played through. An empty name is the default output device of Windows |

The old dialog writes exactly these two, so the Sound Manager and the old dialog read and write the
same setting and cannot disagree.

### What Windows does with the other sounds while a program talks

Windows has a setting for what happens to the other sounds when a program starts talking. It is the
**Communications tab** of the old Sound dialog, called **Adaptive communication sound levels** in
Windows 11 and buried under Sound → Output.

It is not in the mixer window. It is a setting of the whole machine and has nothing to do with one
device or one program, so it sits on the settings page in a group called **Calls and chats**: a
switch that turns the feature off, and a dropdown for the amount — mute the others, lower by 80
percent (what Windows does by default), or lower by 50 percent.

Windows keeps the choice in the registry of the user, under
`HKEY_CURRENT_USER\Software\Microsoft\Multimedia\Audio` → `UserDuckingPreference` (REG_DWORD): `0`
mutes the others, `1` lowers them by 80 percent, `2` by 50 percent and `3` does nothing. The value
is not written on a fresh machine, and then Windows holds the other sounds down by 80 percent. The
old dialog and the Sound Manager read and write the same value.

The setting says **what happens, not when**. Windows decides for itself that a program is talking,
by watching what a program does with the microphone.

### What needs a driver, and therefore is out of scope

A true per-application frequency equalizer is **not achievable inside a PowerToys module**. Three
things stand in the way:

1. **Windows mixes all applications before an equalizer could act.** The only pre-mix hook is an
   Audio Processing Object, which is driver-level, installed via an INF and must be signed.
   PowerToys ships no drivers.
2. **The documented alternatives are per-device, not per-application.** Equalizer APO is attached to
   a *device*, so one curve covers the whole PC.
3. **Loopback capture cannot separate applications.** Capturing the default endpoint yields the
   already-mixed buffer, so only one curve can ever be applied.

**Boosting above 100 percent is not possible** either: `SetChannelVolume` rejects any value above
1.0 with `E_INVALIDARG` on a live session, and the scalar application volume cannot go above 1.0
either. It can be done by taking the audio path away from Windows — with a driver, by capturing a
program and playing it again louder, or through a second endpoint — but every one of those breaks
the promise the module is built on, that Windows and the Sound Manager never disagree.

Sending one application to several devices at once needs a loopback driver, and capturing what you
hear is a different product.

## Settings

| Setting | Description |
|---|---|
| Enable Sound Manager | The switch at the top of the page, under the module name. Can be set by group policy. |
| Open Sound Manager | Opens the mixer window from the settings page. |
| Activation shortcut | Opens the mixer window, or brings it to the front. `Win+Ctrl+Shift+V` by default. |
| Mute toggle shortcuts | Under **Shortcuts**: one shortcut per program, each of which silences that program and brings it back. The first shortcut offered is one that does not clash with anything. |
| Calls and chats | What Windows does with the other sounds while a program is talking: a switch for the feature and a dropdown for the amount. |

## Known issues and limitations

- **The program picker can only offer the programs that are making sound at that moment.** A quiet
  program has no audio session, so there is nothing to recognise it by, and a shortcut given to a
  program that is not playing does nothing. Listing every running program instead would put the
  list of every process on the machine on the pipe and on the page, and a program that is not
  running cannot be listed at all without asking the user to type a file name.
- **Choosing "System default" for an application that is playing right now does nothing until the
  application makes a sound again.** The stored route is written at once, but the Windows SDK has no
  method that moves a running stream, so this is Windows' own behaviour.
- **The volume of an application is kept per device.** An application that was turned down on a
  headset comes back at full volume when it plays through the speakers. That is what Windows does,
  and it is what the module does too, so the two agree.
- **Windows can take a few seconds to start the sound** of a microphone that is played through the
  speakers. The switch itself is written at once.
- **The ducking setting is honoured by Windows for new sessions.** Writing
  `UserDuckingPreference` changes what the old dialog shows straight away, but the audio engine
  reads it when a session starts.

## Related issues

The pull request is not going to point at one issue. It opens with a paragraph that ties the
mentions of sound and audio together as the history of the request, and the issues are linked from
there. These are the roots of that history.

| Issue | What it asks for |
|---|---|
| [#1011](https://github.com/microsoft/PowerToys/issues/1011) "Sound mixer as a toy" (2019) | The root of the family: a mixer that can silence a program, change the volume of a program and choose the devices. Closed as "built into Windows" when Windows 11 shipped a mixer of its own — the built-in mixer was never for Windows 10, and the parts that are missing are the ones this module does. |
| [#4964](https://github.com/microsoft/PowerToys/issues/4964) "Change Audio Output with Hotkey" (2020, open) | The root of the other family: the device that Windows plays through. |
| [#30045](https://github.com/microsoft/PowerToys/issues/30045) "Application-Specific Volume Mixer" (2023) | The volume of every program, kept between restarts. |
| [#6015](https://github.com/microsoft/PowerToys/issues/6015) "Volume Control & Mute/Un-Mute In Window Titlebar" (2020, open) | The volume and the mute of a program in the place where the program is. |
| [#44712](https://github.com/microsoft/PowerToys/issues/44712) "Volume mixer shortcut" (2026, open) | A shortcut that opens the mixer. |
| [#49273](https://github.com/microsoft/PowerToys/issues/49273) "Volume mute/control on windows producing sound" (2026) | The speaker icon on the windows that make sound, and one key to silence them. |
| [#7584](https://github.com/microsoft/PowerToys/issues/7584) "Shortcut to mute/unmute current application" (2020) | The shortcut for the application in front of you. Closed into #1011, with the thread arguing that a mixer and this are two different things. |
| [#29440](https://github.com/microsoft/PowerToys/issues/29440) "Correlation between multiple monitors, speakers and application placement" (2023) | The fullest description of the routing that exists in the repository. |
| [#6785](https://github.com/microsoft/PowerToys/issues/6785) "Audio Tools" (2020) | The only upstream mention of per-program left and right balance: "All that is missing, is balance left/right per app". |
| [#41190](https://github.com/microsoft/PowerToys/issues/41190) "Swap audio channels" (2025, open) | The nearest thing to a request of its own about the channels. |
| [#47916](https://github.com/microsoft/PowerToys/issues/47916) "Sound Switcher Feature" (2026, open) | The output device, asked for in a different way. |
| [#16237](https://github.com/microsoft/PowerToys/issues/16237) "Sound Profiles" (2022, open) | The combinations of playback device and microphone kept. |

A further thirteen issues ask for a shortcut that changes the output device and were closed into
#4964: #14717, #16165, #16931, #19896, #20872, #22095, #22819, #22958, #23322, #32429, #45941 and
#9628.

Two warnings about the history. #1011 and #30045 were both closed as "built into Windows", so the
pull request must not say that Windows already does this. And #37833, #38502 and #13064, which are
all about a mixer of their own, were closed into issues about something else entirely — the
mistaken closing is not a reason to leave them out, but it is not a reason to cite the wrong parent
either.

## Debugging

The mixer window is a separate WinUI 3 app. To debug it:

1. Set the **Runner** project (`src/runner`) as the startup project in Visual Studio and launch it.
2. Enable **Sound Manager** in the settings window.
3. Start **Debug → Attach to Process** (`Ctrl+Alt+P`) and attach to
   `PowerToys.SoundManager.exe` (select the **Managed (.NET Core)** debugger).

Started without the runner there is no pipe and no hotkey, so the window comes up and waits for the
module interface. The module interface is a native DLL inside the runner, so it is debugged by
attaching to `PowerToys.exe`.

## UI tests

There are no automated UI tests for this module yet. The window was driven through UI Automation
by hand while it was built; a suite should follow the pattern of the other modules that have a
WinUI 3 window.

## Future Improvements

- Command Palette integration for quick volume and mute control, in a separate pull request.
- A global equalizer curve through a virtual audio device, behind a toggle.
