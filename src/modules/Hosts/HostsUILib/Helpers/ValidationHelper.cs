// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.RegularExpressions;

namespace HostsUILib.Helpers
{
    public static partial class ValidationHelper
    {
        /// <summary>
        /// Determines whether the address is a valid IPv4
        /// </summary>
        public static bool ValidIPv4(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            return IPv4Regex().IsMatch(address);
        }

        /// <summary>
        /// Determines whether the address is a valid IPv6
        /// </summary>
        public static bool ValidIPv6(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            return IPv6Regex().IsMatch(address);
        }

        /// <summary>
        /// Determines whether the hosts are valid
        /// </summary>
        public static bool ValidHosts(string hosts, bool validateHostsLength)
        {
            if (string.IsNullOrWhiteSpace(hosts))
            {
                return false;
            }

            var splittedHosts = hosts.Split(' ');

            if (validateHostsLength && splittedHosts.Length > Consts.MaxHostsCount)
            {
                return false;
            }

            foreach (var host in splittedHosts)
            {
                if (Uri.CheckHostName(host) == UriHostNameType.Unknown)
                {
                    return false;
                }
            }

            return true;
        }

        [GeneratedRegex("^(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$")]
        private static partial Regex IPv4Regex();

        [GeneratedRegex("^(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))$")]
        private static partial Regex IPv6Regex();
    }
}
