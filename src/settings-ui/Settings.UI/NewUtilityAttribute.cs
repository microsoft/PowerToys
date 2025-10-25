// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.PowerToys.Settings.UI
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class NewUtilityAttribute : Attribute
    {
        public string Version { get; }

        public NewUtilityAttribute(string version)
        {
            Version = version;
        }
    }
}
