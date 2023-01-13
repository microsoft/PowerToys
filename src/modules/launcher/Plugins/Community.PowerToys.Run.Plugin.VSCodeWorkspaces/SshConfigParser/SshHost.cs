// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.VSCodeWorkspaces.SshConfigParser
{
    public class SshHost
    {
        public string IdentityFile
        {
            get => this[nameof(IdentityFile)]?.ToString();
            set => this[nameof(IdentityFile)] = value;
        }

        public string Host
        {
            get => this[nameof(Host)]?.ToString();
            set => this[nameof(Host)] = value;
        }

        public string HostName
        {
            get => this[nameof(HostName)]?.ToString();
            set => this[nameof(HostName)] = value;
        }

        public string User
        {
            get => this[nameof(User)]?.ToString();
            set => this[nameof(User)] = value;
        }

        public string ForwardAgent
        {
            get => this[nameof(ForwardAgent)]?.ToString();
            set => this[nameof(ForwardAgent)] = value;
        }

        internal Dictionary<string, object> Properties { get; } = new Dictionary<string, object>();

        public object this[string key]
        {
            get
            {
                if (Properties.TryGetValue(key, out var value))
                {
                    return value;
                }

                return null;
            }

            set
            {
                Properties[key] = value;
            }
        }

        public IEnumerable<string> Keys => Properties.Keys;
    }
}
