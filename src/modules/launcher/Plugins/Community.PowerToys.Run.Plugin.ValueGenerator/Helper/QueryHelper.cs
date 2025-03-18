// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;

using Community.PowerToys.Run.Plugin.ValueGenerator.Properties;

namespace Community.PowerToys.Run.Plugin.ValueGenerator.Helper
{
    /// <summary>
    /// Helper class to easier work with queries
    /// </summary>
    internal static class QueryHelper
    {
        /// <summary>
        /// a list of all value generator descriptions
        /// </summary>
        private static readonly string GeneratorDescriptionUuid = Resources.generator_description_uuid;
        private static readonly string GeneratorDescriptionUuidv1 = Resources.generator_description_uuidv1;
        private static readonly string GeneratorDescriptionUuidv3 = Resources.generator_description_uuidv3;
        private static readonly string GeneratorDescriptionUuidv4 = Resources.generator_description_uuidv4;
        private static readonly string GeneratorDescriptionUuidv5 = Resources.generator_description_uuidv5;
        private static readonly string GeneratorDescriptionUuidv7 = Resources.generator_description_uuidv7;
        private static readonly string GeneratorDescriptionHash = Resources.generator_description_hash;
        private static readonly string GeneratorDescriptionBase64 = Resources.generator_description_base64;
        private static readonly string GeneratorDescriptionBase64d = Resources.generator_description_base64d;
        private static readonly string GeneratorDescriptionUrl = Resources.generator_description_url;
        private static readonly string GeneratorDescriptionUrld = Resources.generator_description_urld;
        private static readonly string GeneratorDescriptionEscData = Resources.generator_description_esc_data;
        private static readonly string GeneratorDescriptionUescData = Resources.generator_description_uesc_data;
        private static readonly string GeneratorDescriptionEscHex = Resources.generator_description_esc_hex;
        private static readonly string GeneratorDescriptionUescHex = Resources.generator_description_uesc_hex;
        private static readonly string GeneratorDescriptionYourInput = Resources.generator_description_your_input;
        private static readonly string GeneratorExample = Resources.generator_example;
        private static readonly string Or = Resources.or;

        private static string GetStringFormat(string value, string arg)
        {
            return string.Format(CultureInfo.CurrentCulture, value, arg);
        }

        private static string GetStringFormat(string value)
        {
            return string.Format(CultureInfo.CurrentCulture, value);
        }

        internal static string GetResultTitle(GeneratorData generatorData)
        {
            return $"{generatorData.Keyword} - {generatorData.Description}";
        }

        internal static string GetResultSubtitle(GeneratorData generatorData)
        {
            return GetStringFormat(GeneratorExample, generatorData.Example);
        }

        /// <summary>
        /// A list that contain all of the value generators and its descriptions
        /// </summary>
        internal static readonly List<GeneratorData> GeneratorDataList =
        [
            new()
            {
                Keyword = "uuid",
                Description = GetStringFormat(GeneratorDescriptionUuid),
                Example = $"uuid {GetStringFormat(Or)} guid",
            },
            new()
            {
                Keyword = "uuidv1",
                Description = GetStringFormat(GeneratorDescriptionUuidv1),
                Example = $"uuidv1 {GetStringFormat(Or)} uuid1",
            },
            new()
            {
                Keyword = "uuidv3",
                Description = GetStringFormat(GeneratorDescriptionUuidv3),
                Example = $"uuidv3 ns:<DNS, URL, OID, {GetStringFormat(Or)} X500> <{GetStringFormat(GeneratorDescriptionYourInput)}>",
            },
            new()
            {
                Keyword = "uuidv4",
                Description = GetStringFormat(GeneratorDescriptionUuidv4),
                Example = $"uuidv4 {GetStringFormat(Or)} uuid4",
            },
            new()
            {
                Keyword = "uuidv5",
                Description = GetStringFormat(GeneratorDescriptionUuidv5),
                Example = $"uuidv5 ns:<DNS, URL, OID, {GetStringFormat(Or)} X500> <{GetStringFormat(GeneratorDescriptionYourInput)}>",
            },
            new()
            {
                Keyword = "uuidv7",
                Description = GetStringFormat(GeneratorDescriptionUuidv7),
                Example = $"uuidv7 {GetStringFormat(Or)} uuid7",
            },
            new()
            {
                Keyword = "md5",
                Description = GetStringFormat(GeneratorDescriptionHash, "MD5"),
                Example = $"md5 <{GetStringFormat(GeneratorDescriptionYourInput)}>",
            },
            new()
            {
                Keyword = "sha1",
                Description = GetStringFormat(GeneratorDescriptionHash, "SHA1"),
                Example = $"sha1 <{GetStringFormat(GeneratorDescriptionYourInput)}>",
            },
            new()
            {
                Keyword = "sha256",
                Description = GetStringFormat(GeneratorDescriptionHash, "SHA256"),
                Example = $"sha256 <{GetStringFormat(GeneratorDescriptionYourInput)}>",
            },
            new()
            {
                Keyword = "sha384",
                Description = GetStringFormat(GeneratorDescriptionHash, "SHA384"),
                Example = $"sha384 <{GetStringFormat(GeneratorDescriptionYourInput)}>",
            },
            new()
            {
                Keyword = "sha512",
                Description = GetStringFormat(GeneratorDescriptionHash, "SHA512"),
                Example = $"sha512 <{GetStringFormat(GeneratorDescriptionYourInput)}>",
            },
            new()
            {
                Keyword = "base64",
                Description = GetStringFormat(GeneratorDescriptionBase64),
                Example = $"base64 <{GetStringFormat(GeneratorDescriptionYourInput)}>",
            },
            new()
            {
                Keyword = "base64d",
                Description = GetStringFormat(GeneratorDescriptionBase64d),
                Example = $"base64d <{GetStringFormat(GeneratorDescriptionYourInput)}>",
            },
            new()
            {
                Keyword = "url",
                Description = GetStringFormat(GeneratorDescriptionUrl),
                Example = "url https://bing.com/?q=My Test query",
            },
            new()
            {
                Keyword = "urld",
                Description = GetStringFormat(GeneratorDescriptionUrld),
                Example = "urld https://bing.com/?q=My+Test+query",
            },
            new()
            {
                Keyword = "esc:data",
                Description = GetStringFormat(GeneratorDescriptionEscData),
                Example = "esc:data C:\\Program Files\\PowerToys\\PowerToys.exe",
            },
            new()
            {
                Keyword = "uesc:data",
                Description = GetStringFormat(GeneratorDescriptionUescData),
                Example = "uesc:data C%3A%5CProgram%20Files%5CPowerToys%5CPowerToys.exe",
            },
            new()
            {
                Keyword = "esc:hex",
                Description = GetStringFormat(GeneratorDescriptionEscHex),
                Example = "esc:hex z",
            },
            new()
            {
                Keyword = "uesc:hex",
                Description = GetStringFormat(GeneratorDescriptionUescHex),
                Example = "uesc:hex %7A",
            },
        ];
    }
}
