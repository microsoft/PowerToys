// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;

namespace HostsUILib.Helpers
{
    public static class PingStatusHelper
    {
        public static Visibility SucceededVisibility(bool? ping) => ping == true ? Visibility.Visible : Visibility.Collapsed;

        public static Visibility FailedVisibility(bool? ping) => ping == false ? Visibility.Visible : Visibility.Collapsed;
    }
}
