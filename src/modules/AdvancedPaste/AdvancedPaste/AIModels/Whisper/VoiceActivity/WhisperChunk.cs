// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.AIModels.Whisper
{
    public class WhisperChunk
    {
        public double Start { get; set; }

        public double End { get; set; }

        public WhisperChunk(double start, double end)
        {
            this.Start = start;
            this.End = end;
        }

        public double Length => End - Start;
    }
}
