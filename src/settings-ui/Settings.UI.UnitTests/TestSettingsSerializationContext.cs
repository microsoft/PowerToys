// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UnitTest;

namespace Microsoft.PowerToys.Settings.UI.UnitTests
{
    /// <summary>
    /// JSON serialization context for unit tests.
    /// This context provides source-generated serialization for test-specific types.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        IncludeFields = true)]
    [JsonSerializable(typeof(BasePTSettingsTest))]
    public partial class TestSettingsSerializationContext : JsonSerializerContext
    {
    }
}
