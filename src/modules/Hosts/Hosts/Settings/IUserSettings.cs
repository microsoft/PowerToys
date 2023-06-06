// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Settings.UI.Library.Enumerations;

namespace Hosts.Settings
{
    public interface IUserSettings
    {
        public bool ShowStartupWarning { get; }

        public bool LoopbackDuplicates { get; }

        public HostsAdditionalLinesPosition AdditionalLinesPosition { get; }

        public HostsEncoding Encoding { get; }

        event EventHandler LoopbackDuplicatesChanged;
    }
}
