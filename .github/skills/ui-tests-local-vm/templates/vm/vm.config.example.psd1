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
}
