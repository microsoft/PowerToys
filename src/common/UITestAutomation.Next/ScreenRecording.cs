// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Records the desktop to an MP4 during a UI test by sampling GDI frames and encoding them with
/// FFmpeg. Used only by the pipeline path of <see cref="UITestBase"/>. If FFmpeg isn't on PATH (or
/// in a few well-known locations) recording silently disables itself — screenshots still cover the
/// failure. Ported from the legacy harness.
/// </summary>
internal sealed class ScreenRecording : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorInfo(out ScreenCapture.CURSORINFO pci);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int cx, int cy, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

    private const int CURSORSHOWING = 0x00000001;
    private const int DESKTOPHORZRES = 118;
    private const int DESKTOPVERTRES = 117;
    private const int DINORMAL = 0x0003;
    private const int TargetFps = 15; // Balance of quality and size.

    private readonly string outputDirectory;
    private readonly string framesDirectory;
    private readonly string outputFilePath;
    private readonly List<string> capturedFrames;
    private readonly SemaphoreSlim recordingLock = new(1, 1);
    private readonly Stopwatch recordingStopwatch = new();
    private readonly string? ffmpegPath;
    private CancellationTokenSource? recordingCancellation;
    private Task? recordingTask;
    private bool isRecording;
    private int frameCount;

    public ScreenRecording(string outputDirectory)
    {
        this.outputDirectory = outputDirectory;
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        framesDirectory = Path.Combine(outputDirectory, $"frames_{timestamp}");
        outputFilePath = Path.Combine(outputDirectory, $"recording_{timestamp}.mp4");
        capturedFrames = new List<string>();
        frameCount = 0;

        ffmpegPath = FindFfmpeg();
        if (ffmpegPath is null)
        {
            Console.WriteLine("FFmpeg not found. Screen recording will be disabled.");
            Console.WriteLine("To enable video recording, install FFmpeg: https://ffmpeg.org/download.html");
        }
    }

    /// <summary>True when FFmpeg was located, so recording can actually produce an MP4.</summary>
    public bool IsAvailable => ffmpegPath is not null;

    /// <summary>Path the encoded MP4 will be written to.</summary>
    public string OutputFilePath => outputFilePath;

    /// <summary>Directory containing the recording output.</summary>
    public string OutputDirectory => outputDirectory;

    /// <summary>Start sampling frames on a background task.</summary>
    public async Task StartRecordingAsync()
    {
        await recordingLock.WaitAsync();
        try
        {
            if (isRecording || !IsAvailable)
            {
                return;
            }

            Directory.CreateDirectory(framesDirectory);

            recordingCancellation = new CancellationTokenSource();
            isRecording = true;
            recordingStopwatch.Start();

            recordingTask = Task.Run(() => RecordFrames(recordingCancellation.Token));
            Console.WriteLine($"Started screen recording at {TargetFps} FPS");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start recording: {ex.Message}");
            isRecording = false;
        }
        finally
        {
            recordingLock.Release();
        }
    }

    /// <summary>Stop sampling and encode the captured frames to an MP4.</summary>
    public async Task StopRecordingAsync()
    {
        await recordingLock.WaitAsync();
        try
        {
            if (!isRecording || recordingCancellation is null)
            {
                return;
            }

            recordingCancellation.Cancel();

            if (recordingTask is not null)
            {
                await recordingTask;
            }

            recordingStopwatch.Stop();
            isRecording = false;

            var duration = recordingStopwatch.Elapsed.TotalSeconds;
            Console.WriteLine($"Recording stopped. Captured {capturedFrames.Count} frames in {duration:F2} seconds");

            await EncodeToVideoAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping recording: {ex.Message}");
        }
        finally
        {
            Cleanup();
            recordingLock.Release();
        }
    }

    public void Dispose()
    {
        if (isRecording)
        {
            StopRecordingAsync().GetAwaiter().GetResult();
        }

        Cleanup();
        recordingLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private void RecordFrames(CancellationToken cancellationToken)
    {
        try
        {
            var frameInterval = 1000 / TargetFps;
            var frameTimer = Stopwatch.StartNew();

            while (!cancellationToken.IsCancellationRequested)
            {
                var frameStart = frameTimer.ElapsedMilliseconds;

                try
                {
                    CaptureFrame();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error capturing frame: {ex.Message}");
                }

                var frameTime = frameTimer.ElapsedMilliseconds - frameStart;
                var sleepTime = Math.Max(0, frameInterval - (int)frameTime);
                if (sleepTime > 0)
                {
                    Thread.Sleep(sleepTime);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when stopping.
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during recording: {ex.Message}");
        }
    }

    private void CaptureFrame()
    {
        var hdc = GetDC(IntPtr.Zero);
        var screenWidth = GetDeviceCaps(hdc, DESKTOPHORZRES);
        var screenHeight = GetDeviceCaps(hdc, DESKTOPVERTRES);
        ReleaseDC(IntPtr.Zero, hdc);

        var bounds = new Rectangle(0, 0, screenWidth, screenHeight);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            var cursorInfo = default(ScreenCapture.CURSORINFO);
            cursorInfo.CbSize = Marshal.SizeOf<ScreenCapture.CURSORINFO>();
            if (GetCursorInfo(out cursorInfo) && cursorInfo.Flags == CURSORSHOWING)
            {
                var hdcDest = g.GetHdc();
                DrawIconEx(hdcDest, cursorInfo.PTScreenPos.X, cursorInfo.PTScreenPos.Y, cursorInfo.HCursor, 0, 0, 0, IntPtr.Zero, DINORMAL);
                g.ReleaseHdc(hdcDest);
            }
        }

        var framePath = Path.Combine(framesDirectory, $"frame_{frameCount:D6}.jpg");
        bitmap.Save(framePath, ImageFormat.Jpeg);
        capturedFrames.Add(framePath);
        frameCount++;
    }

    private async Task EncodeToVideoAsync()
    {
        if (capturedFrames.Count == 0)
        {
            Console.WriteLine("No frames captured");
            return;
        }

        try
        {
            var inputPattern = Path.Combine(framesDirectory, "frame_%06d.jpg");

            // -y overwrite, -nostdin no interaction, -loglevel error quiet, -stats progress.
            var args = $"-y -nostdin -loglevel error -stats -framerate {TargetFps} -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p -crf 23 \"{outputFilePath}\"";

            Console.WriteLine($"Encoding {capturedFrames.Count} frames to video...");

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath!,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo);
            if (process is not null)
            {
                process.StandardInput.Close();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                _ = await outputTask;
                var stderr = await errorTask;

                if (process.ExitCode == 0 && File.Exists(outputFilePath))
                {
                    var fileInfo = new FileInfo(outputFilePath);
                    Console.WriteLine($"Video created: {outputFilePath} ({fileInfo.Length / 1024 / 1024:F1} MB)");
                }
                else
                {
                    Console.WriteLine($"FFmpeg encoding failed with exit code {process.ExitCode}");
                    if (!string.IsNullOrWhiteSpace(stderr))
                    {
                        Console.WriteLine($"FFmpeg error: {stderr}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error encoding video: {ex.Message}");
        }
    }

    private static string? FindFfmpeg()
    {
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
        foreach (var dir in pathDirs)
        {
            var candidate = Path.Combine(dir, "ffmpeg.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        var commonPaths = new[]
        {
            @"C:\.tools\ffmpeg\bin\ffmpeg.exe",
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
            $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\Microsoft\WinGet\Links\ffmpeg.exe",
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private void Cleanup()
    {
        recordingCancellation?.Dispose();
        recordingCancellation = null;
        recordingTask = null;

        try
        {
            if (Directory.Exists(framesDirectory))
            {
                Directory.Delete(framesDirectory, true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to cleanup frames directory: {ex.Message}");
        }
    }
}
