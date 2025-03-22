// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;

namespace RegistryPreviewUILib
{
    /// <summary>
    /// Class representing an each item in the list view, each one a Registry Value.
    /// </summary>
    public class RegistryValue
    {
        // Static members
        private static Uri uriStringValue = new Uri("ms-appx:///Assets/RegistryPreview/string32.png");
        private static Uri uriBinaryValue = new Uri("ms-appx:///Assets/RegistryPreview/data32.png");
        private static Uri uriDeleteValue = new Uri("ms-appx:///Assets/RegistryPreview/deleted-value32.png");
        private static Uri uriErrorValue = new Uri("ms-appx:///Assets/RegistryPreview/error32.png");

        public string Key { get; set; }

        public string Name { get; set; }

        public string Type { get; set; }

        public string Value { get; set; }

        public bool IsEmptyBinary { private get; set; }

        public string ValueOneLine => Value.Replace('\r', ' ');

        public string ToolTipText { get; set; }

        public Uri ImageUri
        {
            // Based off the Type of the item, pass back the correct image Uri used by the Binding of the DataGrid
            get
            {
                switch (Type)
                {
                    case "REG_SZ":
                    case "REG_EXPAND_SZ":
                    case "REG_MULTI_SZ":
                        return uriStringValue;
                    case "ERROR":
                        return uriErrorValue;
                    case "":
                        return uriDeleteValue;
                }

                return uriBinaryValue;
            }
        }

        public bool ShowPreviewButton =>
            Type != "ERROR" && Type != string.Empty &&
            Value != string.Empty && IsEmptyBinary != true;

        public RegistryValue(string name, string type, string value, string key)
        {
            this.Name = name;
            this.Type = type;
            this.Value = value;
            this.ToolTipText = string.Empty;
            this.Key = key;
        }

        // Commands
        public ICommand CopyToClipboardEntry_Click => new RelayCommand(CopyToClipboardEntry);

        public ICommand CopyToClipboardWithPath_Click => new RelayCommand(CopyToClipboardEntryWithPath);

        public ICommand CopyToClipboardName_Click => new RelayCommand(CopyToClipboardName);

        public ICommand CopyToClipboardType_Click => new RelayCommand(CopyToClipboardType);

        public ICommand CopyToClipboardData_Click => new RelayCommand(CopyToClipboardData);

        private void CopyToClipboardEntry()
        {
            ClipboardHelper.CopyToClipboardAction($"{Name}\r\n{Type}\r\n{Value}");
        }

        private void CopyToClipboardEntryWithPath()
        {
            ClipboardHelper.CopyToClipboardAction($"{Key}\r\n{Name}\r\n{Type}\r\n{Value}");
        }

        private void CopyToClipboardName()
        {
            ClipboardHelper.CopyToClipboardAction(Name);
        }

        private void CopyToClipboardType()
        {
            ClipboardHelper.CopyToClipboardAction(Type);
        }

        private void CopyToClipboardData()
        {
            ClipboardHelper.CopyToClipboardAction(Value);
        }
    }
}
