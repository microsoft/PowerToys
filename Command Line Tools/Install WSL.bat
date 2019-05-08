powershell -Command "Start-Process 'cmd' -Verb RunAs -ArgumentList '/k PowerShell Enable-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux && shutdown -r && exit'"
