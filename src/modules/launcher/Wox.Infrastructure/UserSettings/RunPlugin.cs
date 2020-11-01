// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Wox.Infrastructure.UserSettings
{
    public class RunPlugin
    {
        public string ID { get; set; }

        public string Name { get; set; }

        private List<string> _actionKeywords;

        public RunPlugin(List<string> actionKeywords = null)
        {
            _actionKeywords = actionKeywords;
        }

        public List<string> GetActionKeywords()
        {
            return _actionKeywords;
        }

        public void SetActionKeywords(List<string> value)
        {
            _actionKeywords = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether used only to save the state of the plugin in settings
        /// </summary>
        public bool Disabled { get; set; }
    }
}
