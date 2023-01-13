// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Settings.UI.Library.Enumerations;

namespace Hosts.Settings
{
    public interface IUserSettings
    {
        public bool ShowStartupWarning { get; }

        public AdditionalLinesPosition AdditionalLinesPosition { get; }
    }
}
