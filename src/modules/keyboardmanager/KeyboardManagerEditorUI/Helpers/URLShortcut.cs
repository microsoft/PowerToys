// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyboardManagerEditorUI.Helpers
{
    public class URLShortcut : IToggleableShortcut
    {
        public List<string> Shortcut { get; set; } = new List<string>();

        public string URL { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public string Id { get; set; } = string.Empty;

        public bool IsAllApps { get; set; } = true;

        public string AppName { get; set; } = string.Empty;

        /// <summary>
        /// Gets a display-friendly label for the URL(s). Shows the first URL and a count of additional URLs.
        /// </summary>
        public string DisplayUrl
        {
            get
            {
                var urls = URL.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (urls.Length <= 1)
                {
                    return URL.Trim();
                }

                return $"{urls[0].Trim()} (+{urls.Length - 1} more)";
            }
        }

        /// <summary>
        /// Gets the full list of URLs formatted for a tooltip (one per line).
        /// </summary>
        public string TooltipText
        {
            get
            {
                var urls = URL.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                return string.Join(Environment.NewLine, urls.Select(u => u.Trim()));
            }
        }
    }
}
