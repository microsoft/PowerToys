// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AdvancedPaste.AIModels.Whisper
{
    public class WhisperTranscribedChunk
    {
        public string Text { get; set; }

        public double Start { get; set; }

        public double End { get; set; }

        public double Length => End - Start;
    }
}
