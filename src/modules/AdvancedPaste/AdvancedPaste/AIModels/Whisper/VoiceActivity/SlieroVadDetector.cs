// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace AdvancedPaste.AIModels.Whisper
{
    public class SlieroVadDetector : IDisposable
    {
        private readonly SlieroVadOnnxModel model;
        private readonly float startThreshold;
        private readonly float endThreshold;
        private readonly int samplingRate;
        private readonly float minSilenceSamples;
        private readonly float speechPadSamples;
        private bool triggered;
        private int tempEnd;
        private int currentSample;

        public SlieroVadDetector(
            float startThreshold,
            float endThreshold,
            int samplingRate,
            int minSilenceDurationMs,
            int speechPadMs)
        {
            if (samplingRate != 8000 && samplingRate != 16000)
            {
                throw new ArgumentException("does not support sampling rates other than [8000, 16000]");
            }

            this.model = new SlieroVadOnnxModel();
            this.startThreshold = startThreshold;
            this.endThreshold = endThreshold;
            this.samplingRate = samplingRate;
            this.minSilenceSamples = samplingRate * minSilenceDurationMs / 1000f;
            this.speechPadSamples = samplingRate * speechPadMs / 1000f;

            Reset();
        }

        public void Reset()
        {
            model.ResetStates();
            triggered = false;
            tempEnd = 0;
            currentSample = 0;
        }

        public Dictionary<string, double> Apply(byte[] data, bool returnSeconds)
        {
            float[] audioData = new float[data.Length / 2];
            for (int i = 0; i < audioData.Length; i++)
            {
                audioData[i] = ((data[i * 2] & 0xff) | (data[(i * 2) + 1] << 8)) / 32767.0f;
            }

            int windowSizeSamples = audioData.Length;
            currentSample += windowSizeSamples;

            float speechProb = 0;
            try
            {
                speechProb = model.Call(new float[][] { audioData }, samplingRate)[0];
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An error occurred while calling the model", ex);
            }

            if (speechProb >= startThreshold && tempEnd != 0)
            {
                tempEnd = 0;
            }

            if (speechProb >= startThreshold && !triggered)
            {
                triggered = true;
                int speechStart = (int)(currentSample - speechPadSamples);
                speechStart = Math.Max(speechStart, 0);

                Dictionary<string, double> result = new Dictionary<string, double>();
                if (returnSeconds)
                {
                    double speechStartSeconds = speechStart / (double)samplingRate;
                    double roundedSpeechStart = Math.Round(speechStartSeconds, 1, MidpointRounding.AwayFromZero);
                    result["start"] = roundedSpeechStart;
                }
                else
                {
                    result["start"] = speechStart;
                }

                return result;
            }

            if (speechProb < endThreshold && triggered)
            {
                if (tempEnd == 0)
                {
                    tempEnd = currentSample;
                }

                if (currentSample - tempEnd < minSilenceSamples)
                {
                    return new Dictionary<string, double>();
                }
                else
                {
                    int speechEnd = (int)(tempEnd + speechPadSamples);
                    tempEnd = 0;
                    triggered = false;

                    Dictionary<string, double> result = new Dictionary<string, double>();

                    if (returnSeconds)
                    {
                        double speechEndSeconds = speechEnd / (double)samplingRate;
                        double roundedSpeechEnd = Math.Round(speechEndSeconds, 1, MidpointRounding.AwayFromZero);
                        result["end"] = roundedSpeechEnd;
                    }
                    else
                    {
                        result["end"] = speechEnd;
                    }

                    return result;
                }
            }

            return new Dictionary<string, double>();
        }

        public void Close()
        {
            Reset();
            model.Close();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.model.Dispose();
        }
    }
}
