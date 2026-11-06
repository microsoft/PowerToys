// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using ImageResizer.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Tests.Properties
{
    [TestClass]
    public sealed class WrappedJsonReaderTests
    {
        [TestMethod]
        [DataRow("{\"number\":{\"value\":3}}")]
        [DataRow("{\"number\":{\"ignored\":\"metadata\",\"value\":3}}")]
        public void ReadValueFirstOrAfterStringMetadata_PreservesNumber(string json)
        {
            var result = JsonSerializer.Deserialize<WrappedIntegerSetting>(json);

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Number);
        }

        [TestMethod]
        [DataRow("{\"number\":{}}")]
        [DataRow("{\"number\":{\"Value\":3}}")]
        [DataRow("{\"number\":{\"ignored\":false,\"value\":3}}")]
        [DataRow("{\"number\":{\"value\":3,\"ignored\":\"metadata\"}}")]
        [DataRow("{\"number\":3}")]
        public void ReadMalformedWrapper_ThrowsJsonException(string json)
        {
            Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<WrappedIntegerSetting>(json));
        }

        public sealed class WrappedIntegerSetting
        {
            [JsonPropertyName("number")]
            [JsonConverter(typeof(WrappedJsonValueConverter))]
            public int Number { get; set; }
        }
    }
}
