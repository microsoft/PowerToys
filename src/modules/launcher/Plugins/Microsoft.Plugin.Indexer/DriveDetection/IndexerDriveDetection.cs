using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Plugin.Indexer.DriveDetection;
using Microsoft.Plugin.Indexer.Interface;

namespace Microsoft.Plugin.Indexer.DriveDetection
{
    public class IndexerDriveDetection
    {
        private bool IsEnhancedModeEnabled { get; set; } = false;
        private IRegistryWrapper _registryHelper;
        public bool IsDriveDetectionWarningCheckBoxSelected { get; set; } = false;

        public IndexerDriveDetection(IRegistryWrapper registryHelper)
        {
            _registryHelper = registryHelper;
            GetEnhancedModeStatus();
        }

        // To display the results if either enhanced mode is on or if the disable drive detection checkbox is checked
        public bool DisplayResults()
        {
            return IsDriveDetectionWarningCheckBoxSelected || IsEnhancedModeEnabled;
        }

        // To look up the registry entry for 
        private void GetEnhancedModeStatus()
        {
            string registryLocation = @"Software\Microsoft\Windows Search\Gather\Windows\SystemIndex";
            string valueName = "EnableFindMyFiles";
            IsEnhancedModeEnabled = _registryHelper.GetHKLMRegistryValue(registryLocation, valueName) == 0 ? false : true;
        }
    }
}
