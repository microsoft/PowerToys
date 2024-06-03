// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace HostsUILib.Settings
{
    public interface IUserSettings
    {
        public bool ShowStartupWarning { get; }

        public bool LoopbackDuplicates { get; }

        public HostsAdditionalLinesPosition AdditionalLinesPosition { get; }

        public HostsEncoding Encoding { get; }

        event EventHandler LoopbackDuplicatesChanged;

        public delegate void OpenSettingsFunction();
    }
}
