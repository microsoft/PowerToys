// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

using Microsoft.PowerToys.Settings.UI.Library;
using Settings.UI.Library.Enumerations;

namespace MeasureToolUI
{
    public sealed class Settings
    {
        private static readonly SettingsUtils ModuleSettings = SettingsUtils.Default;

        public MeasureToolMeasureStyle DefaultMeasureStyle
        {
            get
            {
                try
                {
                    return (MeasureToolMeasureStyle)ModuleSettings.GetSettings<MeasureToolSettings>(MeasureToolSettings.ModuleName).Properties.DefaultMeasureStyle.Value;
                }
                catch (FileNotFoundException)
                {
                    return MeasureToolMeasureStyle.None;
                }
            }
        }

        /// <summary>
        /// Gets the configured toolbar anchor, reloaded from disk on every access (in particular,
        /// once per Screen Ruler summon - see MainWindow's constructor) so a setting changed while
        /// the toolbar was last dismissed takes effect on the next summon.
        /// </summary>
        public int ToolbarPosition
        {
            get
            {
                try
                {
                    return ModuleSettings.GetSettings<MeasureToolSettings>(MeasureToolSettings.ModuleName).Properties.ToolbarPosition.Value;
                }
                catch (FileNotFoundException)
                {
                    return (int)MeasureToolToolbarPosition.TopCenter;
                }
            }
        }
    }
}
