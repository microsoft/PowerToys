using System.Collections.Generic;

namespace Microsoft.PowerToys.Run.Plugin.VSCodeWorkspaces.SshConfigParser
{
    public class SshHost
    {
        /// <summary>
        /// Identity file for the host.
        /// </summary>
        public string IdentityFile 
        {
            get => this[nameof(IdentityFile)]?.ToString();
            set => this[nameof(IdentityFile)] = value;
        }

        /// <summary>
        /// The host alias
        /// </summary>
        public string Host 
        {
            get => this[nameof(Host)]?.ToString();
            set => this[nameof(Host)] = value;
        }

        /// <summary>
        /// Full host name
        /// </summary>
        public string HostName 
        {
            get => this[nameof(HostName)]?.ToString();
            set => this[nameof(HostName)] = value;
        }

        /// <summary>
        /// User
        /// </summary>
        public string User {
            get => this[nameof(User)]?.ToString();
            set => this[nameof(User)] = value;
        }

        /// <summary>
        /// The ForwardAgent setting
        /// </summary>
        public string ForwardAgent {
            get => this[nameof(ForwardAgent)]?.ToString();
            set => this[nameof(ForwardAgent)] = value;
        }

        /// <summary>
        /// A collection of all the properties for this host, including those explicitly defined.
        /// </summary>
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
            set { Properties[key] = value; }
        }

        /// <summary>
        /// Keys of all items in the SSH host.
        /// </summary>
        public IEnumerable<string> Keys => Properties.Keys;
    }
}