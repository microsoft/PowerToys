@{
    # Copy this file to vm.config.psd1 in the same folder. Never commit vm.config.psd1.
    # The administrator password is never stored here: it is imported from the DPAPI credential file.

    VmName = 'PowerToysUiTest-Win11'
    ComputerName = 'PTUITEST'

    # Guest storage. Keep it outside the repository.
    # Prefer NTFS. Keeping a VHDX on a Dev Drive has been observed to wedge the Hyper-V management
    # service until vmms is restarted or the host is rebooted. Hyper-V on plain ReFS is supported, so
    # New-UiTestVm.ps1 refuses ReFS only as a conservative proxy for Dev Drive; pass
    # -AllowReFsVolume to override. The scaffold and shared exchange have no such restriction.
    VmPath = 'D:\PowerToysUiTestVm\vm'
    VhdPath = 'D:\PowerToysUiTestVm\vm\PowerToysUiTest.vhdx'
    DiskSizeGB = 128

    # Default resource profile. Lower to 1 vCPU / 4 GB only after the suite is green.
    MemoryStartupGB = 8
    ProcessorCount = 4
    ConstrainedMemoryStartupGB = 4
    ConstrainedProcessorCount = 1

    # Set to '' for a fully isolated guest. PowerShell Direct works without any network adapter.
    SwitchName = 'Default Switch'

    # Accounts. The password comes from the DPAPI credential file, not from this file.
    AdminUserName = 'PTAdmin'
    StandardUser = 'PTUser'

    # arm64 or amd64. Must match the installation media and the guest payloads.
    ProcessorArchitecture = 'amd64'
    Locale = 'en-US'
    TimeZone = 'UTC'

    # Standard checkpoints capture memory, so restoring returns to a logged-on desktop immediately.
    BaselineCheckpointName = 'provisioned-baseline'

    # Optional. Read only by Initialize-LocalVmMultimon.ps1; every key has the default shown, and
    # angle brackets stand for the VM root and the repository root. See references\multimon.md.
    # Multimon = @{
    #     # Host folder with the built IndirectDisplay sample driver package. Build-IddSampleDriver.ps1
    #     # writes it here.
    #     DriverPath = '<VmRoot>\multimon\IddSampleDriver'
    #     # A Set-MonitorLayout.ps1 preset other than Single; references\multimon.md lists them.
    #     Preset = 'SideBySide'
    #     # Host folder of images, one per monitor in file-name order. Empty generates the 75%
    #     # color-bar colors: gray, yellow, cyan, green, magenta, red, blue.
    #     WallpaperPath = ''
    #     # Host path of the built PowerToys.MonitorReportTool.exe. Its report is skipped when absent.
    #     # <Platform> is ARM64 when ProcessorArchitecture is arm64, else x64.
    #     MonitorReportToolPath = '<RepositoryRoot>\<Platform>\Release\MonitorReportTool\PowerToys.MonitorReportTool.exe'
    #     # Letters, digits, '.', '_', and '-'. It must differ from BaselineCheckpointName, because
    #     # the multimon checkpoint replaces any checkpoint of the same name.
    #     CheckpointName = 'multimon-baseline'
    #     # $true disconnects every network adapter before the checkpoint, so the baseline cannot
    #     # reboot for Windows Update mid-run. PowerShell Direct and Copy-VMFile do not need the
    #     # network. Only $true or $false is accepted.
    #     DisconnectNetwork = $false
    # }
}
