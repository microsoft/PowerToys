// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// Centralizes short UI sound playback. Callers only enqueue work; media preparation and
/// file loading are handled asynchronously and never awaited by UI or command execution paths.
/// </summary>
public sealed partial class AudioCueService : IAudioCueService, IRecipient<PlayAudioCueMessage>, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly MediaPlayer _mediaPlayer;
    private readonly BuiltInAudioCueSoundCache _builtInSoundCache = new();
    private readonly long[] _lastCueTicks = new long[Enum.GetValues<AudioCue>().Length];

    private MediaSource? _currentSource;
    private IRandomAccessStream? _currentStream;
    private long _requestId;
    private bool _isDisposed;

    public AudioCueService(ISettingsService settingsService, DispatcherQueue dispatcherQueue)
    {
        _settingsService = settingsService;
        _dispatcherQueue = dispatcherQueue;
        _mediaPlayer = new MediaPlayer
        {
            AutoPlay = false,
        };
        _mediaPlayer.CommandManager.IsEnabled = false;
        _mediaPlayer.MediaFailed += MediaPlayer_MediaFailed;
        WeakReferenceMessenger.Default.Register<PlayAudioCueMessage>(this);
        _ = Task.Run(_builtInSoundCache.PreloadAsync);
    }

    public void Play(AudioCue cue)
    {
        if (_isDisposed || ShouldThrottle(cue))
        {
            return;
        }

        var settings = _settingsService.Settings.AudioCues;
        var effect = settings.GetEffect(cue);
        if (!settings.IsEnabled || !CanPlay(effect))
        {
            return;
        }

        QueuePlayback(cue, effect, settings.Volume);
    }

    public void Preview(AudioCue cue)
    {
        if (_isDisposed)
        {
            return;
        }

        var settings = _settingsService.Settings.AudioCues;
        var effect = settings.GetEffect(cue);
        if (CanPlay(effect))
        {
            QueuePlayback(cue, effect, settings.Volume);
        }
    }

    public void Preview(AudioCue cue, string? soundId, string? customSoundPath = null)
    {
        if (_isDisposed || soundId is null)
        {
            return;
        }

        var effect = new AudioCueEffectSettings { Sound = soundId, CustomSoundPath = customSoundPath };
        if (CanPlay(effect))
        {
            QueuePlayback(cue, effect, _settingsService.Settings.AudioCues.Volume);
        }
    }

    public void Receive(PlayAudioCueMessage message) => Play(message.Cue);

    private bool ShouldThrottle(AudioCue cue)
    {
        var throttleMilliseconds = AudioCueCatalog.GetDefinition(cue).ThrottleMilliseconds;
        if (throttleMilliseconds <= 0)
        {
            return false;
        }

        var now = Environment.TickCount64;
        var previous = Interlocked.Exchange(ref _lastCueTicks[(int)cue], now);
        return previous != 0 && now - previous < throttleMilliseconds;
    }

    private static bool CanPlay(AudioCueEffectSettings effect) =>
        effect.IsEnabled &&
        (AudioCueCatalog.ResolveSoundId(effect.Sound) != AudioCueCatalog.CustomSoundId || !string.IsNullOrWhiteSpace(effect.CustomSoundPath));

    private void QueuePlayback(AudioCue cue, AudioCueEffectSettings effect, int volume)
    {
        var requestId = Interlocked.Increment(ref _requestId);
        _ = Task.Run(() => PrepareAndQueuePlaybackAsync(cue, effect, Math.Clamp(volume, 0, 100) / 100.0, requestId));
    }

    private async Task PrepareAndQueuePlaybackAsync(AudioCue cue, AudioCueEffectSettings effect, double volume, long requestId)
    {
        PreparedAudioSource? prepared = null;
        try
        {
            var soundId = AudioCueCatalog.ResolveSoundId(effect.Sound);
            prepared = soundId == AudioCueCatalog.CustomSoundId
                ? PrepareCustomSource(effect.CustomSoundPath!)
                : AudioCueCatalog.TryGetSystemSound(soundId, out var systemSound)
                    ? PrepareSystemSource(systemSound)
                    : await PrepareBuiltInSourceAsync(cue, soundId).ConfigureAwait(false);

            if (_isDisposed || requestId != Volatile.Read(ref _requestId))
            {
                prepared.Dispose();
                return;
            }

            if (!_dispatcherQueue.TryEnqueue(() => ApplyPreparedSource(prepared, volume, requestId)))
            {
                prepared.Dispose();
            }
        }
        catch (Exception ex)
        {
            prepared?.Dispose();
            Logger.LogError($"Failed to prepare audio cue {cue}", ex);
        }
    }

    private async Task<PreparedAudioSource> PrepareBuiltInSourceAsync(AudioCue cue, string soundId)
    {
        var bytes = await _builtInSoundCache.GetAsync(cue, soundId).ConfigureAwait(false);
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        return new(MediaSource.CreateFromStream(stream, "audio/wav"), stream);
    }

    private static PreparedAudioSource PrepareCustomSource(string customSoundPath)
    {
        var uri = new Uri(customSoundPath, UriKind.Absolute);
        return new(MediaSource.CreateFromUri(uri), null);
    }

    private static PreparedAudioSource PrepareSystemSource(AudioCueSystemSound sound)
    {
        var uri = new Uri(ResolveSystemSoundPath(sound), UriKind.Absolute);
        return new(MediaSource.CreateFromUri(uri), null);
    }

    /// <summary>
    /// Prefers the user's active sound scheme; falls back to the stock file under %WINDIR%\Media.
    /// </summary>
    private static string ResolveSystemSoundPath(AudioCueSystemSound sound)
    {
        if (sound.EventApp is not null && sound.EventName is not null)
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"AppEvents\Schemes\Apps\{sound.EventApp}\{sound.EventName}\.Current");
            if (key?.GetValue(null) is string current && !string.IsNullOrWhiteSpace(current))
            {
                var expanded = Environment.ExpandEnvironmentVariables(current);
                if (File.Exists(expanded))
                {
                    return expanded;
                }
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", sound.FallbackFileName);
    }

    private void ApplyPreparedSource(PreparedAudioSource prepared, double volume, long requestId)
    {
        if (_isDisposed || requestId != Volatile.Read(ref _requestId))
        {
            prepared.Dispose();
            return;
        }

        _mediaPlayer.Pause();
        _mediaPlayer.Source = null;

        _currentSource?.Dispose();
        _currentStream?.Dispose();
        _currentSource = prepared.Source;
        _currentStream = prepared.Stream;
        prepared.Detach();

        _mediaPlayer.Volume = volume;
        _mediaPlayer.Source = _currentSource;
        _mediaPlayer.Play();
    }

    private static void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Logger.LogError($"Audio cue playback failed: {args.ErrorMessage}");
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Interlocked.Increment(ref _requestId);
        _mediaPlayer.MediaFailed -= MediaPlayer_MediaFailed;
        WeakReferenceMessenger.Default.Unregister<PlayAudioCueMessage>(this);
        _mediaPlayer.Dispose();
        _currentSource?.Dispose();
        _currentStream?.Dispose();
    }

    private sealed partial class PreparedAudioSource(MediaSource source, IRandomAccessStream? stream) : IDisposable
    {
        private bool _isDetached;

        public MediaSource Source { get; } = source;

        public IRandomAccessStream? Stream { get; } = stream;

        public void Detach() => _isDetached = true;

        public void Dispose()
        {
            if (!_isDetached)
            {
                Source.Dispose();
                Stream?.Dispose();
            }
        }
    }
}
