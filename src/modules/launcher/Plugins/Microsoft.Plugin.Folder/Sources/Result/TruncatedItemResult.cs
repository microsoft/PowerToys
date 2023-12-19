// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Wox.Plugin;

namespace Microsoft.Plugin.Folder.Sources.Result
{
    public class TruncatedItemResult : IItemResult
    {
        private static readonly CompositeFormat MicrosoftPluginFolderTruncationWarningSubtitle = System.Text.CompositeFormat.Parse(Properties.Resources.Microsoft_plugin_folder_truncation_warning_subtitle);

        public int PreTruncationCount { get; set; }

        public int PostTruncationCount { get; set; }

        public string WarningIconPath { get; set; }

        public string Search { get; set; }

        public Wox.Plugin.Result Create(IPublicAPI contextApi)
        {
            return new Wox.Plugin.Result
            {
                Title = Properties.Resources.Microsoft_plugin_folder_truncation_warning_title,
                QueryTextDisplay = Search,

                // Using CurrentCulture since this is user facing
                SubTitle = string.Format(CultureInfo.CurrentCulture, MicrosoftPluginFolderTruncationWarningSubtitle, PostTruncationCount, PreTruncationCount),
                ToolTipData = new ToolTipData(Properties.Resources.Microsoft_plugin_folder_truncation_warning_title, string.Format(CultureInfo.CurrentCulture, MicrosoftPluginFolderTruncationWarningSubtitle, PostTruncationCount, PreTruncationCount)),
                IcoPath = WarningIconPath,
            };
        }
    }
}
