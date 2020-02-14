// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using PreviewHandlerCommon.Telemetry;

namespace MarkdownPreviewHandler
{
    /// <summary>
    /// Telemetry helper class for markdown renderer.
    /// </summary>
    public class MarkdownTelemetry : TelemetryBase
    {
        /// <summary>
        /// Name for ETW event.
        /// </summary>
        private const string EventSourceName = "Microsoft.PowerToys";

        /// <summary>
        /// ETW event name when markdown is previewed.
        /// </summary>
        private const string MarkdownFilePreviewedEventName = "PowerPreview_MDRenderer_Previewed";

        /// <summary>
        /// ETW event name when error is thrown during markdown preview.
        /// </summary>
        private const string MarkdownFilePreviewErrorEventName = "PowerPreview_MDRenderer_Error";

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkdownTelemetry"/> class.
        /// </summary>
        public MarkdownTelemetry()
            : base(EventSourceName)
        {
            return;
        }

        /// <summary>
        /// Gets an instance of the <see cref="MarkdownTelemetry"/> class.
        /// </summary>
        public static MarkdownTelemetry Log { get; } = new MarkdownTelemetry();

        /// <summary>
        /// Publishes ETW event when markdown is previewed successfully.
        /// </summary>
        public void MarkdownFilePreviewed()
        {
            this.Write(MarkdownFilePreviewedEventName, new EventSourceOptions()
            {
                Keywords = ProjectKeywordMeasure,
                Tags = ProjectTelemetryTagProductAndServicePerformance,
            });
        }

        /// <summary>
        /// Publishes ETW event when markdown could not be previewed.
        /// </summary>
        public void MarkdownFilePreviewError(string message)
        {
            this.Write(
                MarkdownFilePreviewErrorEventName,
                new EventSourceOptions()
                {
                    Keywords = ProjectKeywordMeasure,
                    Tags = ProjectTelemetryTagProductAndServicePerformance,
                },
                new { Message = message, });
        }
    }
}
