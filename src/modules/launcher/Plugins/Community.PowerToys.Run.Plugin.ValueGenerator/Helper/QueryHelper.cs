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
            { "uuid", "Generate a random UUID" },
            { "uuidv1", "Generate a version 1 UUID" },
            { "uuidv3", "Generate a version 3 UUID" },
            { "uuidv4", "Generate a version 4 UUID" },
            { "uuidv5", "Generate a version 5 UUID" },
            { "md5", "Hash a string with MD5" },
            { "sha1", "Hash a string with SHA1" },
            { "sha256", "Hash a string with SHA256" },
            { "sha384", "Hash a string with SHA384" },
            { "sha512", "Hash a string with SHA512" },
            { "base64", "Encode a string with Base64" },
            { "base64d", "Decode a string with Base64" },
            { "url", "Encode an URL" },
            { "urld", "Decode an URL" },
            { "esc:data", "Escape a data string" },
            { "uesc:data", "Unescape a data string" },
            { "esc:hex", "Escape a single hex character" },
            { "uesc:hex", "Unescape a single hex character" },
        };
    }
}
