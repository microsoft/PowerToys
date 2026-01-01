// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;

namespace Microsoft.PowerToys.FilePreviewCommon.PianoRoll
{
    public static class PianoRollHelper
    {
        public static string MidiSvg(string midiPath)
        {
            var project = DecodeMidiFile(midiPath);
            var svgFactory = new SvgEncoder
            {
                PixelPerBeat = 48,
                NoteHeight = 24,
            }.Generate(project);
            return svgFactory.Dumps();
        }

        private static Project DecodeMidiFile(string midiPath)
        {
            var midiFile = MidiFile.Read(midiPath);
            var resolution = (midiFile.TimeDivision as TicksPerQuarterNoteTimeDivision)?.TicksPerQuarterNote ?? 480;
            return new Project
            {
                TrackList = midiFile.GetTrackChunks()
                    .Select(tr => DecodeMidiTrack(tr, resolution))
                    .ToList(),
            };
        }

        private static Track DecodeMidiTrack(TrackChunk trackChunk, int resolution)
        {
            return new Track
            {
                NoteList = trackChunk.GetNotes()
                    .Select(n => DecodeMidiNote(n, resolution))
                    .ToList(),
            };
        }

        private static Note DecodeMidiNote(Melanchall.DryWetMidi.Interaction.Note midiNote, int resolution)
        {
            int startPos = (int)(midiNote.Time * 480 / resolution);
            return new Note
            {
                StartPos = startPos,
                Length = (int)((midiNote.Time + midiNote.Length) * 480 / resolution) - startPos,
                KeyNumber = midiNote.NoteNumber,
            };
        }
    }
}
