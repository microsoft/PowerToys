# Indexer Plugin
The indexer plugin is used to search for files within the indexed locations of the system.

### [Drive Detection](src/modules/launcher/Plugins/Microsoft.Plugin.Indexer/DriveDetection)
- There are two indexing modes in Windows:
    1. **Classic mode**: Only the desktop and certain customizable locations in the system are indexed. All the systems have the classic mode enabled by default.
    2. **Enhanced Mode**: This mode indexes the entire PR when enabled. The user can exclude certain locations from being indexed in this mode from the Windows Search settings options.
- A drive detection warning is displayed to the users when only the custom mode is enabled on the system informing the user that not all the locations on their PC are indexed as this could lead to some results not showing up.
- The [`IndexerDriveDetection.cs`](src/modules/launcher/Plugins/Microsoft.Plugin.Indexer/DriveDetection/IndexerDriveDetection.cs) file gets the status of the drive detection checkbox in the settings UI and depending on whether the enhanced mode is enabled or disabled, displays the warning.
- To determine whether the `EnhancedMode` is enabled or not, we check the local machine registry entry for `EnableFindMyFiles`. 

### OleDBSearch


### WindowsSearchAPI


