// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NReco.VideoConverter;
using Windows.Storage;

namespace AdvancedPaste.AIModels.Whisper
{
    public static class Whisper
    {
        private static InferenceSession _inferenceSession;

        private static InferenceSession InitializeModel()
        {
            // model generated from https://github.com/microsoft/Olive/blob/main/examples/whisper/README.md
            // var modelPath = $@"{AppDomain.CurrentDomain.BaseDirectory}AIModelAssets\whisper\whisper_tiny.onnx";
            var modelPath = $@"{AppDomain.CurrentDomain.BaseDirectory}AIModelAssets\whisper\whisper_small.onnx";

            SessionOptions options = new SessionOptions();
            options.RegisterOrtExtensions();
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;

            var session = new InferenceSession(modelPath, options);

            return session;
        }

        private static List<WhisperTranscribedChunk> TranscribeChunkAsync(byte[] pcmAudioData, string inputLanguage, WhisperTaskType taskType, int offsetSeconds = 30)
        {
#pragma warning disable CA1861 // Avoid constant arrays as arguments
            if (_inferenceSession == null)
            {
                _inferenceSession = InitializeModel();
            }

            var audioTensor = new DenseTensor<byte>(pcmAudioData, [1, pcmAudioData.Length]);
            var timestampsEnableTensor = new DenseTensor<int>(1);
            timestampsEnableTensor.Fill(1);

            int task = (int)taskType;
            int langCode = WhisperUtils.GetLangId(inputLanguage);
            var decoderInputIds = new int[] { 50258, langCode, task };
            var langAndModeTensor = new DenseTensor<int>(decoderInputIds, [1, 3]);

            var minLengthTensor = new DenseTensor<int>(1);
            minLengthTensor.Fill(0);

            var maxLengthTensor = new DenseTensor<int>(1);
            maxLengthTensor.Fill(448);

            var numBeamsTensor = new DenseTensor<int>(1);
            numBeamsTensor.Fill(1);

            var numReturnSequencesTensor = new DenseTensor<int>(1);
            numReturnSequencesTensor.Fill(1);

            var lengthPenaltyTensor = new DenseTensor<float>(1);
            lengthPenaltyTensor.Fill(1.0f);

            var repetitionPenaltyTensor = new DenseTensor<float>(1);
            repetitionPenaltyTensor.Fill(1.2f);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("audio_stream", audioTensor),
                NamedOnnxValue.CreateFromTensor("min_length", minLengthTensor),
                NamedOnnxValue.CreateFromTensor("max_length", maxLengthTensor),
                NamedOnnxValue.CreateFromTensor("num_beams", numBeamsTensor),
                NamedOnnxValue.CreateFromTensor("num_return_sequences", numReturnSequencesTensor),
                NamedOnnxValue.CreateFromTensor("length_penalty", lengthPenaltyTensor),
                NamedOnnxValue.CreateFromTensor("repetition_penalty", repetitionPenaltyTensor),
                NamedOnnxValue.CreateFromTensor("logits_processor", timestampsEnableTensor),
                NamedOnnxValue.CreateFromTensor("decoder_input_ids", langAndModeTensor),
            };
#pragma warning restore CA1861 // Avoid constant arrays as arguments

            // for multithread need to try AsyncRun
            try
            {
                using var results = _inferenceSession.Run(inputs);
                var result = results[0].AsTensor<string>().GetValue(0);
                return WhisperUtils.ProcessTranscriptionWithTimestamps(result, offsetSeconds);
            }
            catch (Exception)
            {
                // return empty list in case of exception
                return new List<WhisperTranscribedChunk>();
            }
        }

        public static List<WhisperTranscribedChunk> TranscribeAsync(StorageFile audioFile, int startSeconds, int durationSeconds, EventHandler<float> progress = null)
        {
            var transcribedChunks = new List<WhisperTranscribedChunk>();

            var sw = Stopwatch.StartNew();

            var audioBytes = LoadAudioBytes(audioFile.Path, startSeconds, durationSeconds);

            sw.Stop();
            Debug.WriteLine($"Loading took {sw.ElapsedMilliseconds} ms");
            sw.Start();

            var dynamicChunks = WhisperChunking.SmartChunking(audioBytes);

            sw.Stop();
            Debug.WriteLine($"Chunking took {sw.ElapsedMilliseconds} ms");

            for (var i = 0; i < dynamicChunks.Count; i++)
            {
                var chunk = dynamicChunks[i];

                var audioSegment = ExtractAudioSegment(audioFile.Path, chunk.Start, chunk.End - chunk.Start);

                var transcription = TranscribeChunkAsync(audioSegment, "en", WhisperTaskType.Transcribe, (int)chunk.Start);

                transcribedChunks.AddRange(transcription);

                progress?.Invoke(null, (float)i / dynamicChunks.Count);
            }

            return transcribedChunks;
        }

        private static byte[] LoadAudioBytes(string file, int startSeconds, int durationSeconds)
        {
            var ffmpeg = new FFMpegConverter();
            var output = new MemoryStream();

            var extension = Path.GetExtension(file).Substring(1);

            // Convert to PCM
            if (startSeconds == 0 && durationSeconds == 0)
            {
                ffmpeg.ConvertMedia(
                inputFile: file,
                inputFormat: null,
                outputStream: output,
                outputFormat: "s16le",
                new ConvertSettings()
                {
                    AudioCodec = "pcm_s16le",
                    AudioSampleRate = 16000,
                    CustomOutputArgs = "-ac 1",
                });
            }
            else
            {
                ffmpeg.ConvertMedia(
                inputFile: file,
                inputFormat: null,
                outputStream: output,
                outputFormat: "s16le",
                new ConvertSettings()
                {
                    Seek = (float?)startSeconds,
                    MaxDuration = (float?)durationSeconds,
                    AudioCodec = "pcm_s16le",
                    AudioSampleRate = 16000,
                    CustomOutputArgs = "-ac 1",
                });
            }

            return output.ToArray();
        }

        private static byte[] ExtractAudioSegment(string inPath, double startTimeInSeconds, double segmentDurationInSeconds)
        {
            try
            {
                var extension = System.IO.Path.GetExtension(inPath).Substring(1);
                var output = new MemoryStream();

                var convertSettings = new ConvertSettings
                {
                    Seek = (float?)startTimeInSeconds,
                    MaxDuration = (float?)segmentDurationInSeconds,
                    AudioSampleRate = 16000,
                    CustomOutputArgs = "-vn -ac 1",
                };

                var ffMpegConverter = new FFMpegConverter();
                ffMpegConverter.ConvertMedia(
                    inputFile: inPath,
                    inputFormat: null,
                    outputStream: output,
                    outputFormat: "wav",
                    convertSettings);

                return output.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during the audio extraction: " + ex.Message);
                return Array.Empty<byte>(); // Return an empty array in case of exception
            }
        }
    }

    internal enum WhisperTaskType
    {
        Translate = 50358,
        Transcribe = 50359,
    }
}
