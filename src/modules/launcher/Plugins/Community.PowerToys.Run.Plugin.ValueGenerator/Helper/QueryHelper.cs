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
        internal static readonly List<GeneratorData> GeneratorDataList =
        [
            new()
            {
                Keyword = "uuid",
                Description = "Generate a random UUID",
                Example = "uuid or guid",
            },
            new()
            {
                Keyword = "uuidv1",
                Description = "Generate a version 1: Time based UUID",
                Example = "uuidv1 or uuid1",
            },
            new()
            {
                Keyword = "uuidv3",
                Description = "Generate a version 3 (MD5): Namespace and name based UUID",
                Example = "uuidv3 ns:<DNS, URL, OID, or X500> <your input>",
            },
            new()
            {
                Keyword = "uuidv4",
                Description = "Generate a version 4: Randomly generated UUID",
                Example = "uuidv4 or uuid4",
            },
            new()
            {
                Keyword = "uuidv5",
                Description = "Generate a version 5 (SHA1): Namespace and name based UUID",
                Example = "uuidv5 ns:<DNS, URL, OID, or X500> <your input>",
            },
            new()
            {
                Keyword = "md5",
                Description = "Hash a string with MD5",
                Example = "md5 <your input>",
            },
            new()
            {
                Keyword = "sha1",
                Description = "Hash a string with SHA1",
                Example = "sha1 <your input>",
            },
            new()
            {
                Keyword = "sha256",
                Description = "Hash a string with SHA256",
                Example = "sha256 <your input>",
            },
            new()
            {
                Keyword = "sha384",
                Description = "Hash a string with SHA384",
                Example = "sha384 <your input>",
            },
            new()
            {
                Keyword = "sha512",
                Description = "Hash a string with SHA512",
                Example = "sha512 <your input>",
            },
            new()
            {
                Keyword = "base64",
                Description = "Encode a string with Base64",
                Example = "base64 <your input>",
            },
            new()
            {
                Keyword = "base64d",
                Description = "Decode a string with Base64",
                Example = "base64d <your input>",
            },
            new()
            {
                Keyword = "url",
                Description = "Encode a URL",
                Example = "url https://bing.com/?q=My Test query",
            },
            new()
            {
                Keyword = "urld",
                Description = "Decode a URL",
                Example = "urld https://bing.com/?q=My+Test+query",
            },
            new()
            {
                Keyword = "esc:data",
                Description = "Escape a data string",
                Example = "esc:data C:\\Program Files\\PowerToys\\PowerToys.exe",
            },
            new()
            {
                Keyword = "uesc:data",
                Description = "Unescape a data string",
                Example = "uesc:data C%3A%5CProgram%20Files%5CPowerToys%5CPowerToys.exe",
            },
            new()
            {
                Keyword = "esc:hex",
                Description = "Escape a single hex character",
                Example = "esc:hex z",
            },
            new()
            {
                Keyword = "uesc:hex",
                Description = "Unescape a single hex character",
                Example = "uesc:hex %7A",
            },
        ];
    }
}
