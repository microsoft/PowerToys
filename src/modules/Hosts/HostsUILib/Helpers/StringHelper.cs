// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace HostsUILib.Helpers
{
    public static class StringHelper
    {
        public static string GetHostsWithComment(string hosts, string comment)
        {
            return string.IsNullOrWhiteSpace(comment) ? hosts : string.Concat(hosts, " - ", comment);
        }
    }
}
