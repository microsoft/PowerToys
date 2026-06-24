// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Base class for all PowerToys module settings.
    /// </summary>
    /// <remarks>
    /// <para><strong>IMPORTANT for Native AOT compatibility:</strong></para>
    /// <para>When creating a new class that inherits from <see cref="BasePTModuleSettings"/>,
    /// you MUST register it in <see cref="SettingsSerializationContext"/> by adding a
    /// <c>[JsonSerializable(typeof(YourNewSettingsClass))]</c> attribute.</para>
    /// <para>Failure to register the type will cause <see cref="ToJsonString"/> to throw
    /// <see cref="InvalidOperationException"/> at runtime.</para>
    /// <para>See <see cref="SettingsSerializationContext"/> for registration instructions.</para>
    /// </remarks>
    public abstract class BasePTModuleSettings
    {
        // Cached JsonSerializerOptions for Native AOT compatibility
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = SettingsSerializationContext.Default,
        };

        // Gets or sets name of the powertoy module.
        [JsonPropertyName("name")]
        public string Name { get; set; }

        // Gets or sets the powertoys version.
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// Converts the current settings object to a JSON string.
        /// </summary>
        /// <returns>JSON string representation of this settings object.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the runtime type is not registered in <see cref="SettingsSerializationContext"/>.
        /// All derived types must be registered with <c>[JsonSerializable(typeof(YourType))]</c> attribute.
        /// </exception>
        /// <remarks>
        /// This method uses Native AOT-compatible JSON serialization. The runtime type must be
        /// registered in <see cref="SettingsSerializationContext"/> for serialization to work.
        /// </remarks>
        public virtual string ToJsonString()
        {
            // By default JsonSerializer will only serialize the properties in the base class. This can be avoided by passing the object type (more details at https://stackoverflow.com/a/62498888)
            var runtimeType = GetType();

            // For Native AOT compatibility, get JsonTypeInfo from the TypeInfoResolver
            var typeInfo = _jsonSerializerOptions.TypeInfoResolver?.GetTypeInfo(runtimeType, _jsonSerializerOptions);

            if (typeInfo == null)
            {
                throw new InvalidOperationException($"Type {runtimeType.FullName} is not registered in SettingsSerializationContext. Please add it to the [JsonSerializable] attributes.");
            }

            // Use AOT-friendly serialization
            return JsonSerializer.Serialize(this, typeInfo);
        }

        public override int GetHashCode()
        {
            return ToJsonString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var settings = obj as BasePTModuleSettings;
            return settings?.ToJsonString() == ToJsonString();
        }
    }
}
