// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.Helper
{
    /// <summary>
    /// Helper class to easier work with queries
    /// </summary>
    internal static class QueryHelper
    {
        /// <summary>
        /// A list that contain all of the value generators and its descriptions
        /// </summary>
        internal static readonly IReadOnlyDictionary<string, string> GeneratorList = new Dictionary<string, string>(18)
        {
            { "uuid", "Generate a random UUID - Example: uuid or guid" },
            { "uuidv1", "Generate a version 1 UUID - Example: uuidv1 or uuid1" },
            { "uuidv3", "Generate a version 3 UUID - Example: uuidv3 ns:<DNS, URL, OID, or X500> <your input>" },
            { "uuidv4", "Generate a version 4 UUID - Example: uuidv4 or uuid4" },
            { "uuidv5", "Generate a version 5 UUID - Example: uuidv5 ns:<DNS, URL, OID, or X500> <your input>" },
            { "md5", "Hash a string with MD5 - Example: md5 <your input>" },
            { "sha1", "Hash a string with SHA1 - Example: sha1 <your input>" },
            { "sha256", "Hash a string with SHA256 - Example: sha256 <your input>" },
            { "sha384", "Hash a string with SHA384 - Example: sha384 <your input>" },
            { "sha512", "Hash a string with SHA512 - Example: sha512 <your input>" },
            { "base64", "Encode a string with Base64 - Example: base64 <your input>" },
            { "base64d", "Decode a string with Base64 - Example: base64d <your input>" },
            { "url", "Encode a URL - Example: url https://bing.com/?q=My Test query" },
            { "urld", "Decode a URL - Example: urld https://bing.com/?q=My+Test+query" },
            { "esc:data", "Escape a data string - Example: esc:data C:\\Program Files\\PowerToys\\PowerToys.exe" },
            { "uesc:data", "Unescape a data string - Example: uesc:data C%3A%5CProgram%20Files%5CPowerToys%5CPowerToys.exe" },
            { "esc:hex", "Escape a single hex character - Example: esc:hex z" },
            { "uesc:hex", "Unescape a single hex character - Example: uesc:hex %7A" },
        };
    }
}
