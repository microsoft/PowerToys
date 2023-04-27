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
        private static readonly SettingsUtils ModuleSettings = new();

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
    }
}
