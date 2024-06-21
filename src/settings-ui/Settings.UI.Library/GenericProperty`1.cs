// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class GenericProperty<T> : ICmdLineRepresentable
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Adding ICmdLineRepresentable support")]
        public static bool TryParseFromCmd(string cmd, out object result)
        {
            result = null;

            if (ICmdLineRepresentable.TryParseFromCmdFor(typeof(T), cmd, out var value))
            {
                result = new GenericProperty<T> { Value = (T)value };
                return true;
            }

            return false;
        }

        public bool TryToCmdRepresentable(out string result)
        {
            result = Value.ToString();
            return true;
        }
    }
}
