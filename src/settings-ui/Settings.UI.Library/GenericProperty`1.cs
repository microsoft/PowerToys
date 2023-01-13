// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class GenericProperty<T>
    {
        [JsonPropertyName("value")]
        public T Value { get; set; }

        public GenericProperty(T value)
        {
            Value = value;
        }

        // Added a parameterless constructor because of an exception during deserialization. More details here: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json-how-to#deserialization-behavior
        public GenericProperty()
        {
        }
    }
}
