// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Peek.UI
{
    public interface IUserSettings
    {
        public bool AlwaysOnTop { get; }

        public bool ShowTaskbarIcon { get; }

        public bool CloseAfterLosingFocus { get; }

        public bool ConfirmFileDelete { get; set; }

        public event EventHandler? Changed;
    }
}
