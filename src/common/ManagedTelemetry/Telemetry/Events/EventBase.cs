// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using System.Reflection;

namespace Microsoft.PowerToys.Telemetry.Events
{
    /// <summary>
    /// A base class to implement properties that are common to all telemetry events.
    /// </summary>
    [EventData]
    public class EventBase
    {
        public bool UTCReplace_AppSessionGuid => true;

        public string EventName { get; set; }

        private string _version;

        public string Version
        {
            get
            {
                if (string.IsNullOrEmpty(_version))
                {
                    _version = GetVersionFromAssembly();
                }

                return _version;
            }
        }

        private string GetVersionFromAssembly()
        {
            // For consistency this should be formatted the same way as
            // https://github.com/microsoft/PowerToys/blob/710f92d99965109fd788d85ebf8b6b9e0ba1524a/src/common/common.cpp#L635
            var version = Assembly.GetExecutingAssembly()?.GetName()?.Version ?? new Version();
            return $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
