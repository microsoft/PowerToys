// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;

namespace Microsoft.PowerToys.FilePreviewCommon.PianoRoll
{
    internal sealed class SvgEncoder
    {
        public int PixelPerBeat { get; set; }

        public int NoteHeight { get; set; }

        public SvgFactory Generate(Project project)
        {
            var coordinateHelper = new CoordinateHelper
            {
                PixelPerBeat = PixelPerBeat,
                NoteHeight = NoteHeight,
            };
            coordinateHelper.CalculateRange(project);
            var svgFactory = new SvgFactory()
            {
                CoordinateHelper = coordinateHelper,
            };
            svgFactory.ApplyStyle(project.TrackList.Count);
            foreach (var track in project.TrackList.OfType<Track>())
            {
                svgFactory.DrawTrack(track);
            }

            return svgFactory;
        }
    }
}
