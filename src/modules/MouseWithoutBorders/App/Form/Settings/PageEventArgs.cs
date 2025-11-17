// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseWithoutBorders.Form.Settings
{
    public class PageEventArgs : EventArgs
    {
        public SettingsFormPage Page { get; private set; }

        public PageEventArgs(SettingsFormPage page)
        {
            Page = page;
        }
    }
}
