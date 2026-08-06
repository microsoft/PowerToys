@{
    # Copy this file to vm.config.psd1 in the same folder. Never commit vm.config.psd1.
    # The administrator password is never stored here: it is imported from the DPAPI credential file.

    VmName = 'PowerToysUiTest-Win11'
    ComputerName = 'PTUITEST'

    # Guest storage. Keep it outside the repository.
    # This must be an NTFS volume. A ReFS volume, which includes every Dev Drive, is intended for
    # source trees and build output; hosting a VHDX there can wedge the Hyper-V management service
    # until vmms is restarted or the host is rebooted. New-UiTestVm.ps1 refuses ReFS by default.
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
