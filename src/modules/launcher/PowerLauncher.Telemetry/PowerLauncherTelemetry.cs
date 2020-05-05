// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using PreviewHandlerCommon.Telemetry;

namespace Microsoft.PowerLauncher.Telemetry
{
    /// <summary>
    /// Telemetry helper class for Svg renderer.
    /// </summary>
    public class PowerLauncherTelemetry : TelemetryBase
    {

        /// <summary>
        /// Name for ETW event.
        /// </summary>
        private const string EventSourceName = "Microsoft.PowerToys";

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerLauncherTelemetry"/> class.
        /// </summary>
        public PowerLauncherTelemetry()
            : base(EventSourceName)
        {
        }

        /// <summary>
        /// Gets an instance of the <see cref="PowerLauncherTelemetry"/> class.
        /// </summary>
        public static PowerLauncherTelemetry Log = new PowerLauncherTelemetry();

        /// <summary>
        /// Publishes ETW event when an action is triggered on 
        /// </summary>
        public void WriteEvent<T>(T telemetryEvent) 
            where T : IEvent
        {
            this.Write<T>(telemetryEvent.EventName, new EventSourceOptions()
            {
                Keywords = ProjectKeywordMeasure,
                Tags = ProjectTelemetryTagProductAndServicePerformance,
            },
            telemetryEvent);
        }
    }
}
