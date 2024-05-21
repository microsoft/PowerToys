// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;
using AdvancedPaste.AIModels.Whisper;
using Windows.Storage;

namespace AdvancedPaste.Helpers
{
    public class AILocalModelsHelper
    {
        public Task<string> DoWhisperInference(StorageFile file)
        {
            return Task.Run(() =>
            {
                var results = Whisper.TranscribeAsync(file, 0, 0);
                return string.Join("\n", results.Select(r => r.Text));
            });
        }

        public Task<string> DoWhisperInference(StorageFile file, int startSeconds, int durationSeconds)
        {
            return Task.Run(() =>
            {
                var results = Whisper.TranscribeAsync(file, startSeconds, durationSeconds);
                return string.Join("\n", results.Select(r => r.Text));
            });
        }
    }
}
