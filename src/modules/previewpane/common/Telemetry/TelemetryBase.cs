// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Eventing.Reader;
using System.Diagnostics.Tracing;

namespace PreviewHandlerCommon.Telemetry
{
    /// <summary>
    /// Base class for telemetry events.
    /// </summary>
    public class TelemetryBase : EventSource
    {
        /// <summary>
        /// The event tag for this event source.
        /// </summary>
        public const EventTags ProjectTelemetryTagProductAndServicePerformance = (EventTags)0x0u;

        /// <summary>
        /// The event keyword for this event source.
        /// </summary>
        public const EventKeywords PROJECTKEYWORDMEASURE = (EventKeywords)0x0;

        private static readonly string[] PowerToysTelemetryTraits = { "ETW_GROUP", "{42749043-438c-46a2-82be-c6cbeb192ff2}" };

        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryBase"/> class.
        /// </summary>
        /// <param name="eventSourceName">.</param>
        public TelemetryBase(
            string eventSourceName)
            : base(
            eventSourceName,
            EventSourceSettings.EtwSelfDescribingEventFormat,
            PowerToysTelemetryTraits)
        {
            return;
        }
    }
}
