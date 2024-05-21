// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AdvancedPaste.AIModels.Whisper
{
    public static class WhisperChunking
    {
        private static readonly int SAMPLERATE = 16000;
        private static readonly float STARTTHRESHOLD = 0.25f;
        private static readonly float ENDTHRESHOLD = 0.25f;
        private static readonly int MINSILENCEDURATIONMS = 1000;
        private static readonly int SPEECHPADMS = 400;
        private static readonly int WINDOWSIZESAMPLES = 3200;

        private static readonly double MAXCHUNKS = 29;
        private static readonly double MINCHUNKS = 5;

        public static List<WhisperChunk> SmartChunking(byte[] audioBytes)
        {
            SlieroVadDetector vadDetector;
            vadDetector = new SlieroVadDetector(STARTTHRESHOLD, ENDTHRESHOLD, SAMPLERATE, MINSILENCEDURATIONMS, SPEECHPADMS);

            int bytesPerSample = 2;
            int bytesPerWindow = WINDOWSIZESAMPLES * bytesPerSample;

            float totalSeconds = audioBytes.Length / (SAMPLERATE * 2);
            var result = new List<DetectionResult>();
            for (int offset = 0; offset + bytesPerWindow <= audioBytes.Length; offset += bytesPerWindow)
            {
                byte[] data = new byte[bytesPerWindow];
                Array.Copy(audioBytes, offset, data, 0, bytesPerWindow);

                // Simulating the process as if data was being read in chunks
                try
                {
                    var detectResult = vadDetector.Apply(data, true);

                    // iterate over detectResult and apply the data to result:
                    foreach (var (key, value) in detectResult)
                    {
                        result.Add(new DetectionResult { Type = key, Seconds = value });
                    }
                }
                catch (Exception e)
                {
                    // Depending on the need, you might want to break out of the loop or just report the error
                    Console.Error.WriteLine($"Error applying VAD detector: {e.Message}");
                }
            }

            var stamps = GetTimeStamps(result, totalSeconds, MAXCHUNKS, MINCHUNKS);
            return stamps;
        }

        private static List<WhisperChunk> GetTimeStamps(List<DetectionResult> voiceAreas, double totalSeconds, double maxChunkLength, double minChunkLength)
        {
            if (totalSeconds <= maxChunkLength)
            {
                return new List<WhisperChunk> { new WhisperChunk(0, totalSeconds) };
            }

            voiceAreas = voiceAreas.OrderBy(va => va.Seconds).ToList();

            List<WhisperChunk> chunks = new List<WhisperChunk>();

            double nextChunkStart = 0.0;
            while (nextChunkStart < totalSeconds)
            {
                double idealChunkEnd = nextChunkStart + maxChunkLength;
                double chunkEnd = idealChunkEnd > totalSeconds ? totalSeconds : idealChunkEnd;

                var validVoiceAreas = voiceAreas.Where(va => va.Seconds > nextChunkStart && va.Seconds <= chunkEnd).ToList();

                if (validVoiceAreas.Count != 0)
                {
                    chunkEnd = validVoiceAreas.Last().Seconds;
                }

                chunks.Add(new WhisperChunk(nextChunkStart, chunkEnd));
                nextChunkStart = chunkEnd + 0.1;
            }

            return MergeSmallChunks(chunks, maxChunkLength, minChunkLength);
        }

        private static List<WhisperChunk> MergeSmallChunks(List<WhisperChunk> chunks, double maxChunkLength, double minChunkLength)
        {
            for (int i = 1; i < chunks.Count; i++)
            {
                // Check if current chunk is small and can be merged with previous
                if (chunks[i].Length < minChunkLength)
                {
                    double prevChunkLength = chunks[i - 1].Length;
                    double combinedLength = prevChunkLength + chunks[i].Length;

                    if (combinedLength <= maxChunkLength)
                    {
                        chunks[i - 1].End = chunks[i].End; // Merge with previous chunk
                        chunks.RemoveAt(i); // Remove current chunk
                        i--; // Adjust index to recheck current position now pointing to next chunk
                    }
                }
            }

            return chunks;
        }
    }
}
