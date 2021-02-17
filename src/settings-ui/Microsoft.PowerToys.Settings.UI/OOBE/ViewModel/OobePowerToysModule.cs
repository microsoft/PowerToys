// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.Settings.UI.OOBE.ViewModel
{
    public class OobePowerToysModule
    {
        public string ModuleName { get; set; }

        public string Tag { get; set; }

        public bool IsNew { get; set; }

        public string Image { get; set; }

        public int NavIndex { get; set; }

        public string Icon { get; set; }

        public string FluentIcon { get; set; }

        public string PreviewImageSource { get; set; }

        public string Description { get; set; }

        public string Link { get; set; }

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
            NavIndex = other.NavIndex;
            Icon = other.Icon;
            FluentIcon = other.FluentIcon;
            PreviewImageSource = other.PreviewImageSource;
            Description = other.Description;
            Link = other.Link;
        }
    }
}
