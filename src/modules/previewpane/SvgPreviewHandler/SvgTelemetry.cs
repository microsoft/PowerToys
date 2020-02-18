// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using PreviewHandlerCommon.Telemetry;

namespace SvgPreviewHandler
{
    /// <summary>
    /// Telemetry helper class for Svg renderer.
    /// </summary>
    public class SvgTelemetry : TelemetryBase
    {
        /// <summary>
        /// Name for ETW event.
        /// </summary>
        private const string EventSourceName = "Microsoft.PowerToys";

        /// <summary>
        /// ETW event name when Svg is previewed.
        /// </summary>
        private const string SvgFilePreviewedEventName = "PowerPreview_SVGRenderer_Previewed";

        /// <summary>
        /// ETW event name when error is thrown during Svg preview.
        /// </summary>
        private const string SvgFilePreviewErrorEventName = "PowerPreview_SVGRenderer_Error";

        /// <summary>
        /// Initializes a new instance of the <see cref="SvgTelemetry"/> class.
        /// </summary>
        public SvgTelemetry()
            : base(EventSourceName)
        {
            return;
        }

        /// <summary>
        /// Gets an instance of the <see cref="SvgTelemetry"/> class.
        /// </summary>
        public static SvgTelemetry Log { get; } = new SvgTelemetry();

        /// <summary>
        /// Publishes ETW event when svg is previewed successfully.
        /// </summary>
        public void SvgFilePreviewed()
        {
            this.Write(SvgFilePreviewedEventName, new EventSourceOptions()
            {
                Keywords = ProjectKeywordMeasure,
                Tags = ProjectTelemetryTagProductAndServicePerformance,
            });
        }

        /// <summary>
        /// Publishes ETW event when svg could not be previewed.
        /// </summary>
        public void SvgFilePreviewError(string message)
        {
            this.Write(
                SvgFilePreviewErrorEventName,
                new EventSourceOptions()
                {
                    Keywords = ProjectKeywordMeasure,
                    Tags = ProjectTelemetryTagProductAndServicePerformance,
                },
                new { Message = message, });
        }
    }
}
