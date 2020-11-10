// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

        // To display the drive detection warning only when enhanced mode is disabled on a system which has multiple drives.
        // Currently the warning would not be displayed if the enhanced mode is disabled when the user system has only a single fixed drive. However, this warning may be added in the future.
        // This warning can be disabled by checking the disabled drive detection warning checkbox in settings.
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
