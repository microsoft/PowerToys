// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Settings.UI.Library
{
    public enum EntryType
    {
        SettingsPage,
        SettingsCard,
        SettingsExpander,
    }

    public struct SettingEntry
    {
        public EntryType Type { get; set; }

        public string Header { get; set; }

        public string PageTypeName { get; set; }

        public string ElementName { get; set; }

        public string ElementUid { get; set; }

        public string ParentElementName { get; set; }

        public string Description { get; set; }

        public string Icon { get; set; }

        public SettingEntry(EntryType type, string header, string pageTypeName, string elementName, string elementUid, string parentElementName = null, string description = null, string icon = null)
        {
            Type = type;
            Header = header;
            PageTypeName = pageTypeName;
            ElementName = elementName;
            ElementUid = elementUid;
            ParentElementName = parentElementName;
            Description = description;
            Icon = icon;
        }
    }
}
