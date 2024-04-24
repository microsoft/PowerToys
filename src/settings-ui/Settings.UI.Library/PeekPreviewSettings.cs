// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Settings.UI.Library
{
    public class PeekPreviewSettings : ISettingsConfig
    {
        public const string FileName = "preview-settings.json";

        public BoolProperty SourceCodeWrapText { get; set; }

        public BoolProperty SourceCodeTryFormat { get; set; }

        public IntProperty SourceCodeFontSize { get; set; }

        public BoolProperty SourceCodeStickyScroll { get; set; }

        public PeekPreviewSettings()
        {
            SourceCodeWrapText = new BoolProperty(false);
            SourceCodeTryFormat = new BoolProperty(false);
            SourceCodeFontSize = new IntProperty(14);
            SourceCodeStickyScroll = new BoolProperty(true);
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public string GetModuleName()
        {
            return PeekSettings.ModuleName;
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
