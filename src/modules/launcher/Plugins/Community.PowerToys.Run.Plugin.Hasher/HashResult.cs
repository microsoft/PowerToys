// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;

namespace Community.PowerToys.Run.Plugin.Hasher
{
    public struct HashResult
    {
        public string Algorithm { get; set; }

        public byte[] Content { get; set; }

        public byte[] Hash { get; set; }

        public string GetHashAsString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var b in Hash)
            {
                sb.Append(b.ToString("X2", null));
            }

            return sb.ToString();
        }

        public string ToString(System.IFormatProvider provider = null)
        {
            StringBuilder sb = new();
            foreach (var b in Hash)
            {
                sb.Append(b.ToString("X2", provider));
            }

            return $"{Algorithm}: {sb}";
        }
    }
}
