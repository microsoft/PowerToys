// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Microsoft.PowerToys.FilePreviewCommon.PianoRoll
{
    internal sealed class CoordinateHelper
    {
        public const int TICKSPERBEAT = 480;
        public const int PADDING = 4;

        public int PixelPerBeat { get; set; }

        public int NoteHeight { get; set; }

        private int positionRangeStart;
        private int positionRangeEnd;
        private int minKey;
        private int maxKey;

        public void CalculateRange(Project project)
        {
            positionRangeStart = project.TrackList
                .Where(tr => tr.NoteList.Count > 0)
                .Min(tr => tr.NoteList[0].StartPos);
            positionRangeEnd = project.TrackList
                .SelectMany(tr => tr.NoteList)
                .Max(n => n.StartPos + n.Length);
            minKey = project.TrackList
                .SelectMany(tr => tr.NoteList)
                .Min(n => n.KeyNumber);
            maxKey = project.TrackList
                .SelectMany(tr => tr.NoteList)
                .Max(n => n.KeyNumber);
        }

        public NotePositionParameters GetNotePositionParameters(Note note)
        {
            return new NotePositionParameters
            {
                Point1 = new Tuple<double, double>(
                    1.0 * (note.StartPos - positionRangeStart) * PixelPerBeat / TICKSPERBEAT,
                    (maxKey - note.KeyNumber) * NoteHeight),
                Point2 = new Tuple<double, double>(
                    1.0 * (note.StartPos + note.Length - positionRangeStart) * PixelPerBeat / TICKSPERBEAT,
                    (maxKey - note.KeyNumber + 1) * NoteHeight),
            };
        }

        public Tuple<double, double> GetSize()
        {
            return new Tuple<double, double>(
                1.0 * (positionRangeEnd - positionRangeStart) * PixelPerBeat / TICKSPERBEAT,
                (maxKey - minKey + 1) * NoteHeight);
        }
    }
}
