// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Plugin.Indexer.DriveDetection
{
    public class IndexerDriveDetection
    {
        private bool IsEnhancedModeEnabled { get; set; }

        private readonly IRegistryWrapper _registryHelper;

        public bool IsDriveDetectionWarningCheckBoxSelected { get; set; }

        public IndexerDriveDetection(IRegistryWrapper registryHelper)
        {
            _registryHelper = registryHelper;
            GetEnhancedModeStatus();
        }

        // To display the warning when Enhanced mode is disabled and the Disable Drive detection check box in settings is unchecked
        public bool DisplayWarning()
        {
            return !(IsDriveDetectionWarningCheckBoxSelected || IsEnhancedModeEnabled);
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
