// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;

using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    public partial class MouseUtilsViewModel : PageViewModelBase
    {
        /// <summary>Gets the CursorBrake edge editor.</summary>
        public CursorBrakeEdgesViewModel CursorBrakeEdges { get; private set; }

        /// <summary>Reads the module's detected edges again, as when the file changes.</summary>
        public void RefreshCursorBrakeEdges()
        {
            CursorBrakeEdges.Reload();
        }

        private void InitializeCursorBrakeEdges(IFileSystem fileSystem)
        {
            CursorBrakeEdges = new CursorBrakeEdgesViewModel(
                CursorBrakeSettingsConfig.Properties,
                fileSystem,
                SettingsUtils.GetSettingsFilePath(CursorBrakeSettings.ModuleName, CursorBrakeDetectedEdges.FileName),
                SaveCursorBrakeSettings,
                SendCursorBrakeCustomAction,
                name => Helpers.ResourceLoaderInstance.ResourceLoader.GetString(name));
            CursorBrakeEdges.GlobalPushDistance = _cursorBrakePushDistance;
            CursorBrakeEdges.EdgeFilter = _cursorBrakeEdgeFilter;
            CursorBrakeEdges.CrossModifier = _cursorBrakeCrossModifier;
            CursorBrakeEdges.Reload();
        }

        private void SaveCursorBrakeSettings()
        {
            SndCursorBrakeSettings outsettings = new SndCursorBrakeSettings(CursorBrakeSettingsConfig);
            SndModuleSettings<SndCursorBrakeSettings> ipcMessage = new SndModuleSettings<SndCursorBrakeSettings>(outsettings);
            SendConfigMSG(ipcMessage.ToJsonString());
            SettingsUtils.SaveSettings(CursorBrakeSettingsConfig.ToJsonString(), CursorBrakeSettings.ModuleName);
        }

        private void SendCursorBrakeCustomAction(string actionName)
        {
            SendConfigMSG("{\"action\":{\"CursorBrake\":{\"action_name\":\"" + actionName + "\", \"value\":\"\"}}}");
        }
    }
}
