// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AdvancedPaste.AIModels.Whisper
{
    internal static class WhisperUtils
    {
        private static Dictionary<string, string> languageCodes = new()
            {
                { "English", "en" },
                { "Serbian", "sr" },
                { "Hindi", "hi" },
                { "Spanish", "es" },
                { "Russian", "ru" },
                { "Korean", "ko" },
                { "French", "fr" },
                { "Japanese", "ja" },
                { "Portuguese", "pt" },
                { "Turkish", "tr" },
                { "Polish", "pl" },
                { "Catalan", "ca" },
                { "Dutch", "nl" },
                { "Arabic", "ar" },
                { "Swedish", "sv" },
                { "Italian", "it" },
                { "Indonesian", "id" },
                { "Macedonian", "mk" },
                { "Mandarin", "zh" },
            };

        public static int GetLangId(string languageString)
        {
            int langId = 50259;
            Dictionary<string, int> langToId = new Dictionary<string, int>
        {
            { "af", 50327 },
            { "am", 50334 },
            { "ar", 50272 },
            { "as", 50350 },
            { "az", 50304 },
            { "ba", 50355 },
            { "be", 50330 },
            { "bg", 50292 },
            { "bn", 50302 },
            { "bo", 50347 },
            { "br", 50309 },
            { "bs", 50315 },
            { "ca", 50270 },
            { "cs", 50283 },
            { "cy", 50297 },
            { "da", 50285 },
            { "de", 50261 },
            { "el", 50281 },
            { "en", 50259 },
            { "es", 50262 },
            { "et", 50307 },
            { "eu", 50310 },
            { "fa", 50300 },
            { "fi", 50277 },
            { "fo", 50338 },
            { "fr", 50265 },
            { "gl", 50319 },
            { "gu", 50333 },
            { "haw", 50352 },
            { "ha", 50354 },
            { "he", 50279 },
            { "hi", 50276 },
            { "hr", 50291 },
            { "ht", 50339 },
            { "hu", 50286 },
            { "hy", 50312 },
            { "id", 50275 },
            { "is", 50311 },
            { "it", 50274 },
            { "ja", 50266 },
            { "jw", 50356 },
            { "ka", 50329 },
            { "kk", 50316 },
            { "km", 50323 },
            { "kn", 50306 },
            { "ko", 50264 },
            { "la", 50294 },
            { "lb", 50345 },
            { "ln", 50353 },
            { "lo", 50336 },
            { "lt", 50293 },
            { "lv", 50301 },
            { "mg", 50349 },
            { "mi", 50295 },
            { "mk", 50308 },
            { "ml", 50296 },
            { "mn", 50314 },
            { "mr", 50320 },
            { "ms", 50282 },
            { "mt", 50343 },
            { "my", 50346 },
            { "ne", 50313 },
            { "nl", 50271 },
            { "nn", 50342 },
            { "no", 50288 },
            { "oc", 50328 },
            { "pa", 50321 },
            { "pl", 50269 },
            { "ps", 50340 },
            { "pt", 50267 },
            { "ro", 50284 },
            { "ru", 50263 },
            { "sa", 50344 },
            { "sd", 50332 },
            { "si", 50322 },
            { "sk", 50298 },
            { "sl", 50305 },
            { "sn", 50324 },
            { "so", 50326 },
            { "sq", 50317 },
            { "sr", 50303 },
            { "su", 50357 },
            { "sv", 50273 },
            { "sw", 50318 },
            { "ta", 50287 },
            { "te", 50299 },
            { "tg", 50331 },
            { "th", 50289 },
            { "tk", 50341 },
            { "tl", 50325 },
            { "tr", 50268 },
            { "tt", 50335 },
            { "ug", 50348 },
            { "uk", 50260 },
            { "ur", 50337 },
            { "uz", 50351 },
            { "vi", 50278 },
            { "xh", 50322 },
            { "yi", 50305 },
            { "yo", 50324 },
            { "zh", 50258 },
            { "zu", 50321 },
        };

            if (languageCodes.TryGetValue(languageString, out string langCode))
            {
                langId = langToId[langCode];
            }

            return langId;
        }

        public static List<WhisperTranscribedChunk> ProcessTranscriptionWithTimestamps(string transcription, double offsetSeconds = 0)
        {
            Regex pattern = new Regex(@"<\|([\d.]+)\|>([^<]+)<\|([\d.]+)\|>");
            MatchCollection matches = pattern.Matches(transcription);
            List<WhisperTranscribedChunk> list = new();
            for (int i = 0; i < matches.Count; i++)
            {
                // Parse the original start and end times
#pragma warning disable CA1305 // Specify IFormatProvider
                double start = double.Parse(matches[i].Groups[1].Value);
                double end = double.Parse(matches[i].Groups[3].Value);
#pragma warning restore CA1305 // Specify IFormatProvider
                string subtitle = string.IsNullOrEmpty(matches[i].Groups[2].Value) ? string.Empty : matches[i].Groups[2].Value.Trim();
                WhisperTranscribedChunk chunk = new()
                {
                    Text = subtitle,
                    Start = start + offsetSeconds,
                    End = end + offsetSeconds,
                };
                list.Add(chunk);
            }

            return list;
        }

        public static List<WhisperTranscribedChunk> MergeTranscribedChunks(List<WhisperTranscribedChunk> chunks)
        {
            List<WhisperTranscribedChunk> list = new();
            WhisperTranscribedChunk transcribedChunk = chunks[0];

            for (int i = 1; i < chunks.Count; i++)
            {
                char lastCharOfPrev = transcribedChunk.Text[transcribedChunk.Text.Length - 1];
                char firstCharOfNext = chunks[i].Text[0];

                // Approach 1: Get full sentences together
                // Approach 2: Sliding window of desired duration
                if (char.IsLower(firstCharOfNext) || (lastCharOfPrev != '.' && lastCharOfPrev != '?' && lastCharOfPrev != '!'))
                {
                    transcribedChunk.End = chunks[i].End;
                    transcribedChunk.Text += " " + chunks[i].Text;
                }
                else
                {
                    list.Add(transcribedChunk);
                    transcribedChunk = chunks[i];
                }
            }

            list.Add(transcribedChunk);

            return list;
        }
    }
}
