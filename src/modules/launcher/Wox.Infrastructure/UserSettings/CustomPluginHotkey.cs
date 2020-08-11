// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Wox.Plugin;

namespace Wox.Infrastructure.UserSettings
{
    public class CustomPluginHotkey : BaseModel
    {
        public string Hotkey { get; set; }

        public string ActionKeyword { get; set; }
    }
}
