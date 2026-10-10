// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.CompilerServices;
using ScreenRecorderLib;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Records a desktop or window to an MP4 during a UI test using ScreenRecorderLib, which encodes in realtime
/// with native Microsoft Media Foundation (H.264). Used by <see cref="UITestBase"/> and custom fixtures.
/// Unlike the old GDI + FFmpeg path there is nothing to probe for on PATH;
/// any runtime problem is surfaced through <c>OnRecordingFailed</c> and handled gracefully so the
/// failing test is never blocked — screenshots still cover the failure.
/// </summary>
public sealed class ScreenRecording : IDisposable
{
    // Deliberately light capture settings: on CI runners without a GPU, ScreenRecorderLib falls back
    // to software H.264, and a full 1080p/30fps realtime encode competes with the test for CPU. 15 fps
    // at 720p (~4x less pixel throughput than 1080p/30) is still plenty to see what a UI test did.
    // Tune these down further (e.g. 10 fps / 960x540) if a runner is still CPU-starved.
    private const int TargetFps = 15;
    private const int OutputWidth = 1280;
    private const int OutputHeight = 720;

    /// <summary>Upper bound on how long to wait for Media Foundation to flush the MP4 after <c>Stop()</c>.</summary>
    private static readonly TimeSpan FinalizeTimeout = TimeSpan.FromSeconds(30);

    private readonly string outputDirectory;
    private readonly string outputFilePath;
    private readonly IntPtr windowHandle;
    private readonly object syncRoot = new();

    private Recorder? recorder;
    private TaskCompletionSource<bool>? recordingFinished;
    private bool isRecording;

    public ScreenRecording(string outputDirectory)
        : this(outputDirectory, IntPtr.Zero)
    {
    }

    /// <summary>Records a specific window, or the main display when the handle is zero.</summary>
    public ScreenRecording(string outputDirectory, IntPtr windowHandle)
        : this(outputDirectory, windowHandle, TargetFps, OutputWidth, OutputHeight)
    {
    }

    /// <summary>Records with an explicitly bounded video profile for resource-constrained test workloads.</summary>
    public ScreenRecording(string outputDirectory, IntPtr windowHandle, int frameRate, int frameWidth, int frameHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(frameRate, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frameRate, 60);
        ArgumentOutOfRangeException.ThrowIfLessThan(frameWidth, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(frameHeight, 2);
        if ((frameWidth & 1) != 0 || (frameHeight & 1) != 0)
        {
            throw new ArgumentException("H.264 frame dimensions must be even.");
        }

        this.outputDirectory = outputDirectory;
        this.windowHandle = windowHandle;
        FrameRate = frameRate;
        FrameWidth = frameWidth;
        FrameHeight = frameHeight;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        outputFilePath = Path.Combine(outputDirectory, $"recording_{timestamp}_{Guid.NewGuid():N}.mp4");
    }

    /// <summary>
    /// True when recording can be attempted, i.e. the native encoder assembly actually loads.
    /// ScreenRecorderLib is a mixed-mode assembly importing VCRUNTIME140/MSVCP140, so a clean Windows
    /// image without the Visual C++ redistributable cannot load it at all.
    /// </summary>
    public bool IsAvailable => UnavailableReason is null;

    /// <summary>Null when the native encoder can load; otherwise why it cannot.</summary>
    public static string? UnavailableReason
    {
        get
        {
            try
            {
                ProbeNativeEncoder();
                return null;
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.Message}";
            }
        }
    }

    // Kept out of line so the JIT resolves ScreenRecorderLib when this is called rather than when the
    // caller is compiled - otherwise a missing native dependency throws past the caller's try/catch.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeNativeEncoder() => _ = new RecorderOptions();

    /// <summary>Path the encoded MP4 will be written to.</summary>
    public string OutputFilePath => outputFilePath;

    /// <summary>Directory containing the recording output.</summary>
    public string OutputDirectory => outputDirectory;

    /// <summary>Requested fixed recording frame rate.</summary>
    public int FrameRate { get; }

    /// <summary>Requested encoded frame width.</summary>
    public int FrameWidth { get; }

    /// <summary>Requested encoded frame height.</summary>
    public int FrameHeight { get; }

    /// <summary>Whether the recorder accepted a start request.</summary>
    public bool WasStarted { get; private set; }

    /// <summary>Whether the encoder finalized a nonempty MP4 successfully.</summary>
    public bool CompletedSuccessfully { get; private set; }

    /// <summary>The recording failure, if one occurred.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Start recording the selected display or window. Best-effort and non-blocking.</summary>
    public Task StartRecordingAsync()
    {
        lock (syncRoot)
        {
            if (isRecording)
            {
                return Task.CompletedTask;
            }

            try
            {
                WasStarted = false;
                CompletedSuccessfully = false;
                FailureReason = null;
                Directory.CreateDirectory(outputDirectory);

                var options = new RecorderOptions
                {
                    OutputOptions = new OutputOptions
                    {
                        RecorderMode = RecorderMode.Video,

                        // The default profile downscales 1080p to 720p; callers with
                        // nested workloads can reduce it further without changing capture semantics.
                        OutputFrameSize = new ScreenSize(FrameWidth, FrameHeight),
                        Stretch = StretchMode.Uniform,
                    },
                    VideoEncoderOptions = new VideoEncoderOptions
                    {
                        Framerate = FrameRate,

                        // Baseline is the cheapest H.264 profile to encode (no B-frames/CABAC); the
                        // library's own docs note lesser profiles "use less resources" — ideal for a
                        // throwaway diagnostic clip on a runner that falls back to software encoding.
                        Encoder = new H264VideoEncoder { EncoderProfile = H264Profile.Baseline },

                        // Force a constant frame rate. Without this, ScreenRecorderLib only sends a
                        // frame to the encoder when the screen *changes* (variable frame rate), while
                        // the MP4 still advertises TargetFps. Long static stretches (e.g. waiting for a
                        // module to launch) then collapse to a handful of frames and bursts of activity
                        // get packed together, so playback drifts out of sync with wall-clock time — the
                        // video runs fast/offset and the tail of the test looks cut off. Duplicating the
                        // previous frame keeps the timeline 1:1 with real time; H.264 compresses the
                        // repeated frames to almost nothing, so the file stays small. At 15 fps the extra
                        // duplicated idle frames are nearly free to encode.
                        IsFixedFramerate = true,

                        // Prefer encode speed over quality — this is a throwaway diagnostic clip, and a
                        // lower-latency encode leaves more CPU for the test itself on shared CI agents.
                        IsLowLatencyEnabled = true,
                    },

                    // UI tests don't need audio, and capturing it can fail on headless CI agents.
                    AudioOptions = new AudioOptions
                    {
                        IsAudioEnabled = false,
                    },

                    // Keep the cursor visible so a failed run shows what was being clicked.
                    MouseOptions = new MouseOptions
                    {
                        IsMousePointerEnabled = true,
                    },
                };

                if (windowHandle != IntPtr.Zero)
                {
                    options.SourceOptions = new SourceOptions
                    {
                        RecordingSources = [new WindowRecordingSource(windowHandle)],
                    };
                }

                recordingFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                recorder = Recorder.CreateRecorder(options);
                recorder.OnRecordingComplete += OnRecordingComplete;
                recorder.OnRecordingFailed += OnRecordingFailed;
                recorder.Record(outputFilePath);

                WasStarted = true;
                isRecording = true;
                Console.WriteLine($"Started screen recording at {FrameRate} FPS to {outputFilePath}");
            }
            catch (Exception ex)
            {
                FailureReason = $"{ex.GetType().Name}: {ex.Message}";
                Console.WriteLine($"Failed to start recording: {ex.Message}");
                DisposeRecorder();
                isRecording = false;
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>Stop recording and wait for Media Foundation to finalize the MP4. Best-effort.</summary>
    public async Task StopRecordingAsync()
    {
        Recorder? activeRecorder;
        TaskCompletionSource<bool>? finished;

        lock (syncRoot)
        {
            if (!isRecording || recorder is null)
            {
                return;
            }

            activeRecorder = recorder;
            finished = recordingFinished;
            isRecording = false;
        }

        try
        {
            activeRecorder.Stop();

            if (finished is not null)
            {
                // Bound the wait so a stuck encoder never hangs test teardown.
                var completed = await Task.WhenAny(finished.Task, Task.Delay(FinalizeTimeout)).ConfigureAwait(false);
                if (completed != finished.Task)
                {
                    FailureReason = "Timed out waiting for the recording to finalize.";
                    Console.WriteLine("Timed out waiting for the recording to finalize.");
                }
                else if (await finished.Task.ConfigureAwait(false) && File.Exists(outputFilePath) && new FileInfo(outputFilePath).Length > 0)
                {
                    CompletedSuccessfully = true;
                }
            }

            if (CompletedSuccessfully)
            {
                var fileInfo = new FileInfo(outputFilePath);
                Console.WriteLine($"Video created: {outputFilePath} ({fileInfo.Length / 1024.0 / 1024.0:F1} MB)");
            }
            else
            {
                FailureReason ??= "The encoder did not produce a finalized nonempty MP4.";
            }
        }
        catch (Exception ex)
        {
            FailureReason = $"{ex.GetType().Name}: {ex.Message}";
            Console.WriteLine($"Error stopping recording: {ex.Message}");
        }
        finally
        {
            DisposeRecorder();
        }
    }

    public void Dispose()
    {
        if (isRecording)
        {
            StopRecordingAsync().GetAwaiter().GetResult();
        }

        DisposeRecorder();
        GC.SuppressFinalize(this);
    }

    private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        recordingFinished?.TrySetResult(true);
    }

    private void OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        FailureReason = e.Error;
        Console.WriteLine($"Screen recording failed: {e.Error}");
        recordingFinished?.TrySetResult(false);
    }

    private void DisposeRecorder()
    {
        lock (syncRoot)
        {
            if (recorder is null)
            {
                return;
            }

            recorder.OnRecordingComplete -= OnRecordingComplete;
            recorder.OnRecordingFailed -= OnRecordingFailed;

            try
            {
                recorder.Dispose();
            }
            catch (Exception ex)
            {
                FailureReason = $"{ex.GetType().Name}: {ex.Message}";
                Console.WriteLine($"Failed to dispose recorder: {ex.Message}");
            }

            recorder = null;
        }
    }
}
