// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace Hosts.Helpers
{
    public static class ValidationHelper
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

            var regex = new Regex("^(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");
            return regex.IsMatch(address);
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

            var regex = new Regex("^(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))$");
            return regex.IsMatch(address);
        }

        /// <summary>
        /// Determines whether the hosts are valid
        /// </summary>
        public static bool ValidHosts(string hosts)
        {
            if (string.IsNullOrWhiteSpace(hosts))
            {
                return false;
            }

            foreach (var host in hosts.Split(' '))
            {
                if (ValidHostName(host))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determine whether a single host is valid
        /// </summary>
        public static bool ValidHostName(string hostname)
        {
            if (string.IsNullOrEmpty(hostname))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(hostname))
            {
                return false;
            }

            string[] labels = hostname.Split('.');

            if (labels.Length < 2)
            {
                return false;
            }

            foreach (string label in labels)
            {
                if (!IsValidHostnameLabel(label))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determine whether a hostname label is valid
        /// </summary>
        public static bool IsValidHostnameLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            if (label == "*")
            {
                return true;
            }

            if (label[0] == '-' || label[label.Length - 1] == '-')
            {
                return false;
            }

            foreach (char c in label)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-'))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
