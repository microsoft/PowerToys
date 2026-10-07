# Display Profiles Command Palette extension

Monitor profiles are stored in:

```text
%LOCALAPPDATA%\MonitorPower\profiles
```

Each profile stores the selected display target IDs and, when available, the
display layout captured when the profile is saved: device name, resolution,
position, orientation, refresh rate, color depth, and primary-display state.
Profiles created by older builds contain only target IDs and remain supported.

The last pre-switch recovery snapshot is stored in:

```text
%LOCALAPPDATA%\MonitorPower\snapshot.json
```

`Snapshot-Display.ps1` can replace this file with a manually verified layout.
The snapshot is a recovery artifact; named profiles keep their own layout and
must not depend on this file for normal activation.
