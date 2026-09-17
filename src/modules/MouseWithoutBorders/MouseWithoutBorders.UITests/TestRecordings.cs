// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.MouseWithoutBorders.UITests;

internal sealed class TestRecordings : IDisposable
{
    private readonly TestContext context;
    private readonly string directory;
    private readonly List<Recording> recordings = [];
    private Recording? sandboxRecording;
    private bool acceptWindowUpdates = true;
    private bool completed;
    private int windowSequence;

    public TestRecordings(TestContext context, string directory)
    {
        this.context = context;
        this.directory = directory;
    }

    public void StartDesktop()
    {
        if (recordings.Count != 0 || completed)
        {
            throw new InvalidOperationException("Desktop recording must start once, before window capture.");
        }

        Start("desktop", IntPtr.Zero);
    }

    public void CaptureSandboxWindow(long handle)
    {
        if (!acceptWindowUpdates || handle == 0 || sandboxRecording?.WindowHandle == handle)
        {
            return;
        }

        Finish(sandboxRecording);
        sandboxRecording = Start($"sandbox-{++windowSequence:D2}", new IntPtr(handle));
    }

    public void StopSandbox()
    {
        acceptWindowUpdates = false;
        Finish(sandboxRecording);
    }

    public void Complete()
    {
        if (completed)
        {
            return;
        }

        completed = true;
        acceptWindowUpdates = false;
        foreach (var recording in recordings)
        {
            Finish(recording);
        }

        Directory.CreateDirectory(directory);
        var manifest = Path.Combine(directory, "recordings.json");
        RunFiles.Write(manifest, new
        {
            Recordings = recordings.Select(recording => new
            {
                recording.Name,
                recording.WindowHandle,
                recording.StartedUtc,
                recording.StoppedUtc,
                Started = recording.Recorder?.WasStarted ?? false,
                Completed = recording.Recorder?.CompletedSuccessfully ?? false,
                Available = recording.HasOutput,
                Error = recording.UnavailableReason ?? recording.Recorder?.FailureReason,
                File = recording.Recorder is null ? null : Path.GetRelativePath(directory, recording.Recorder.OutputFilePath),
            }).ToArray(),
        });
        context.AddResultFile(manifest);
        foreach (var recording in recordings)
        {
            if (recording.Recorder is { } recorder && recording.HasOutput)
            {
                context.AddResultFile(recorder.OutputFilePath);
            }
        }
    }

    public void Dispose()
    {
        acceptWindowUpdates = false;
        foreach (var recording in recordings)
        {
            Finish(recording);
        }
    }

    private Recording Start(string name, IntPtr handle)
    {
        var recording = new Recording(name, handle.ToInt64(), ScreenRecording.UnavailableReason);
        recordings.Add(recording);
        if (recording.UnavailableReason is not null)
        {
            context.WriteLine($"Video capture unavailable for {name}: {recording.UnavailableReason}");
            return recording;
        }

        recording.Recorder = new ScreenRecording(Path.Combine(directory, name), handle);
        recording.Recorder.StartRecordingAsync().GetAwaiter().GetResult();
        if (!recording.Recorder.WasStarted)
        {
            context.WriteLine($"Video capture did not start for {name}: {recording.Recorder.FailureReason}");
        }

        return recording;
    }

    private void Finish(Recording? recording)
    {
        if (recording is null || recording.StoppedUtc is not null)
        {
            return;
        }

        if (recording.Recorder is not null)
        {
            recording.Recorder.StopRecordingAsync().GetAwaiter().GetResult();
            recording.Recorder.Dispose();
            if (!recording.Recorder.CompletedSuccessfully)
            {
                context.WriteLine($"Video capture did not finalize for {recording.Name}: {recording.Recorder.FailureReason ?? "No finalized MP4 was produced."}");
            }
        }

        recording.StoppedUtc = DateTime.UtcNow;
    }

    private sealed class Recording(string name, long windowHandle, string? unavailableReason)
    {
        public string Name { get; } = name;

        public long WindowHandle { get; } = windowHandle;

        public string? UnavailableReason { get; } = unavailableReason;

        public DateTime StartedUtc { get; } = DateTime.UtcNow;

        public DateTime? StoppedUtc { get; set; }

        public ScreenRecording? Recorder { get; set; }

        public bool HasOutput => Recorder is not null && File.Exists(Recorder.OutputFilePath) && new FileInfo(Recorder.OutputFilePath).Length > 0;
    }
}
