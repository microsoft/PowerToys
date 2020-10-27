// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Plugin.Indexer.Interface;

namespace Microsoft.Plugin.Indexer.DriveDetection
{
    public class IndexerDriveDetection
    {
        private bool IsEnhancedModeEnabled { get; set; }

        private readonly IRegistryWrapper _registryHelper;

        private readonly IDriveInfoWrapper _driveHelper;

        public bool IsDriveDetectionWarningCheckBoxSelected { get; set; }

        public IndexerDriveDetection(IRegistryWrapper registryHelper, IDriveInfoWrapper driveHelper)
        {
            _registryHelper = registryHelper;
            _driveHelper = driveHelper;
            GetEnhancedModeStatus();
        }

        // To display the warning when Enhanced mode is disabled and the Disable Drive detection check box in settings is unchecked
        public bool DisplayWarning()
        {
            return !(IsDriveDetectionWarningCheckBoxSelected || IsEnhancedModeEnabled || (_driveHelper.GetDriveCount() == 1));
        }

        // To look up the registry entry for enhanced search
        private void GetEnhancedModeStatus()
        {
            string registryLocation = @"Software\Microsoft\Windows Search\Gather\Windows\SystemIndex";
            string valueName = "EnableFindMyFiles";
            IsEnhancedModeEnabled = _registryHelper.GetHKLMRegistryValue(registryLocation, valueName) == 0 ? false : true;
        }
    }
}
