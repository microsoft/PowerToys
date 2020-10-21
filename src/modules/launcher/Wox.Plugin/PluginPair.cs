// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Wox.Plugin
{
    public class PluginPair
    {
        public IPlugin Plugin { get; internal set; }

        public PluginMetadata Metadata { get; internal set; }

        public override string ToString()
        {
            return Metadata.Name;
        }

        public override bool Equals(object obj)
        {
            if (obj is PluginPair r)
            {
                // Using Ordinal since this is used internally
                return string.Equals(r.Metadata.ID, Metadata.ID, StringComparison.Ordinal);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            // Using Ordinal since this is used internally
            var hashcode = Metadata.ID?.GetHashCode(StringComparison.Ordinal) ?? 0;
            return hashcode;
        }
    }
}
