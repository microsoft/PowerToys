// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.Settings.UI.OOBE.ViewModel
{
    public class OobePowerToysModule
    {
        private System.Diagnostics.Stopwatch timeOpened = new System.Diagnostics.Stopwatch();

        public string ModuleName { get; set; }

        public string Tag { get; set; }

        public bool IsNew { get; set; }

        public string Image { get; set; }

        public string Icon { get; set; }

        public string FluentIcon { get; set; }

        public string PreviewImageSource { get; set; }

        public string Description { get; set; }

        public string Link { get; set; }

        public string DescriptionLink { get; set; }

        public OobePowerToysModule()
        {
        }

        public OobePowerToysModule(OobePowerToysModule other)
        {
            if (other == null)
            {
                return;
            }

            ModuleName = other.ModuleName;
            Tag = other.Tag;
            IsNew = other.IsNew;
            Image = other.Image;
            Icon = other.Icon;
            FluentIcon = other.FluentIcon;
            PreviewImageSource = other.PreviewImageSource;
            Description = other.Description;
            Link = other.Link;
            DescriptionLink = other.DescriptionLink;
            timeOpened = other.timeOpened;
        }

        public void LogOpeningSettingsEvent()
        {
            PowerToysTelemetry.Log.WriteEvent(new OobeSettingsEvent() { ModuleName = this.ModuleName });
        }

        public void LogRunningModuleEvent()
        {
            PowerToysTelemetry.Log.WriteEvent(new OobeModuleRunEvent() { ModuleName = this.ModuleName });
        }

        public void LogOpeningModuleEvent()
        {
            timeOpened.Start();
        }

        public void LogClosingModuleEvent()
        {
            timeOpened.Stop();
            PowerToysTelemetry.Log.WriteEvent(new OobeSectionEvent() { Section = this.ModuleName, TimeOpenedMs = timeOpened.ElapsedMilliseconds });
        }
    }
}
