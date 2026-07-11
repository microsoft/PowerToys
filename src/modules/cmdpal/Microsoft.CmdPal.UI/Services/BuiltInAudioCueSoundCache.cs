// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.Services;

internal sealed class BuiltInAudioCueSoundCache
{
    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _soundDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");

    public Task<byte[]> GetAsync(AudioCue cue, string soundId)
    {
        var fileName = $"{AudioCueCatalog.GetDefinition(cue).Id}-{soundId}.wav";
        return _cache.GetOrAdd(
            fileName,
            name => new Lazy<Task<byte[]>>(
                () => File.ReadAllBytesAsync(Path.Combine(_soundDirectory, name)),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    public async Task PreloadAsync()
    {
        foreach (var cue in AudioCueCatalog.Cues)
        {
            foreach (var sound in AudioCueCatalog.BuiltInSounds)
            {
                try
                {
                    _ = await GetAsync(cue.Cue, sound.Id).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to cache built-in audio cue {cue.Id}/{sound.Id}", ex);
                }
            }
        }
    }
}
