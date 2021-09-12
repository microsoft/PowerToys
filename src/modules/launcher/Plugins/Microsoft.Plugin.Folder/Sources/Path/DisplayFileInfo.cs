// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Plugin.Folder.Sources
{
    public struct DisplayFileInfo : IEquatable<DisplayFileInfo>
    {
        public string Name { get; set; }

        public string FullName { get; set; }

        public DisplayType Type { get; set; }

        public override bool Equals(object obj)
        {
            return obj is DisplayFileInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, FullName, (int)Type);
        }

        public bool Equals(DisplayFileInfo other)
        {
            return Name == other.Name && FullName == other.FullName && Type == other.Type;
        }

        public static bool operator ==(DisplayFileInfo a, DisplayFileInfo b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(DisplayFileInfo a, DisplayFileInfo b)
        {
            return !(a == b);
        }
    }
}
