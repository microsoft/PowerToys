# [Monitor info report tool](/tools/MonitorReportTool)

A small diagnostic tool which helps identifying WinAPI bugs related to the physical monitor detection. When launched, it creates a log file like this:

```text
GetSystemMetrics = 2
GetMonitorInfo OK
EnumDisplayDevices OK:
        DeviceID = \\?\DISPLAY#VSCBD34#5&25664547&0&UID4355#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
        DeviceKey = \Registry\Machine\System\CurrentControlSet\Control\Class\{4d36e96e-e325-11ce-bfc1-08002be10318}\0002
        DeviceName = \\.\DISPLAY1\Monitor0
        DeviceString = Generic PnP Monitor
GetMonitorInfo OK
EnumDisplayDevices OK:
        DeviceID = \\?\DISPLAY#ENC2682#5&25664547&0&UID4357#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}
        DeviceKey = \Registry\Machine\System\CurrentControlSet\Control\Class\{4d36e96e-e325-11ce-bfc1-08002be10318}\0003
        DeviceName = \\.\DISPLAY2\Monitor0
        DeviceString = Generic PnP Monitor
EnumDisplayMonitors OK
```

and also duplicates the info to `stdout`.
