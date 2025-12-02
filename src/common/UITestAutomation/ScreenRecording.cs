// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.UITest
{
    /// <summary>
    /// Provides methods for recording the screen during UI tests.
    /// Requires FFmpeg to be installed and available in PATH.
    /// </summary>
    internal class ScreenRecording : IDisposable
    {
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

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out ScreenCapture.CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(IntPtr hdc, int x, int y, IntPtr hIcon, int cx, int cy, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        private const int CURSORSHOWING = 0x00000001;
        private const int DESKTOPHORZRES = 118;
        private const int DESKTOPVERTRES = 117;
        private const int DINORMAL = 0x0003;
        private const int TargetFps = 15; // 15 FPS for good balance of quality and size

        /// <summary>
        /// Initializes a new instance of the <see cref="ScreenRecording"/> class.
        /// </summary>
        /// <param name="outputDirectory">Directory where the recording will be saved.</param>
        public ScreenRecording(string outputDirectory)
        {
            this.outputDirectory = outputDirectory;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            framesDirectory = Path.Combine(outputDirectory, $"frames_{timestamp}");
            outputFilePath = Path.Combine(outputDirectory, $"recording_{timestamp}.mp4");
            capturedFrames = new List<string>();
            frameCount = 0;

            // Check if FFmpeg is available
            ffmpegPath = FindFfmpeg();
            if (ffmpegPath == null)
            {
                Console.WriteLine("FFmpeg not found. Screen recording will be disabled.");
                Console.WriteLine("To enable video recording, install FFmpeg: https://ffmpeg.org/download.html");
            }
        }

        /// <summary>
        /// Gets a value indicating whether screen recording is available (FFmpeg found).
        /// </summary>
        public bool IsAvailable => ffmpegPath != null;

        /// <summary>
        /// Starts recording the screen.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StartRecordingAsync()
        {
            await recordingLock.WaitAsync();
            try
            {
                if (isRecording || !IsAvailable)
                {
                    return;
                }

                // Create frames directory
                Directory.CreateDirectory(framesDirectory);

                recordingCancellation = new CancellationTokenSource();
                isRecording = true;
                recordingStopwatch.Start();

                // Start the recording task
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

        /// <summary>
        /// Stops recording and encodes video.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StopRecordingAsync()
        {
            await recordingLock.WaitAsync();
            try
            {
                if (!isRecording || recordingCancellation == null)
                {
                    return;
                }

                // Signal cancellation
                recordingCancellation.Cancel();

                // Wait for recording task to complete
                if (recordingTask != null)
                {
                    await recordingTask;
                }

                recordingStopwatch.Stop();
                isRecording = false;

                double duration = recordingStopwatch.Elapsed.TotalSeconds;
                Console.WriteLine($"Recording stopped. Captured {capturedFrames.Count} frames in {duration:F2} seconds");

                // Encode to video
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

        /// <summary>
        /// Records frames from the screen.
        /// </summary>
        private void RecordFrames(CancellationToken cancellationToken)
        {
            try
            {
                int frameInterval = 1000 / TargetFps;
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

                    // Sleep for remaining time to maintain target FPS
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
                // Expected when stopping
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during recording: {ex.Message}");
            }
        }

        /// <summary>
        /// Captures a single frame.
        /// </summary>
        private void CaptureFrame()
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            int screenWidth = GetDeviceCaps(hdc, DESKTOPHORZRES);
            int screenHeight = GetDeviceCaps(hdc, DESKTOPVERTRES);
            ReleaseDC(IntPtr.Zero, hdc);

            Rectangle bounds = new Rectangle(0, 0, screenWidth, screenHeight);
            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

                    ScreenCapture.CURSORINFO cursorInfo;
                    cursorInfo.CbSize = Marshal.SizeOf<ScreenCapture.CURSORINFO>();
                    if (GetCursorInfo(out cursorInfo) && cursorInfo.Flags == CURSORSHOWING)
                    {
                        IntPtr hdcDest = g.GetHdc();
                        DrawIconEx(hdcDest, cursorInfo.PTScreenPos.X, cursorInfo.PTScreenPos.Y, cursorInfo.HCursor, 0, 0, 0, IntPtr.Zero, DINORMAL);
                        g.ReleaseHdc(hdcDest);
                    }
                }

                string framePath = Path.Combine(framesDirectory, $"frame_{frameCount:D6}.jpg");
                bitmap.Save(framePath, ImageFormat.Jpeg);
                capturedFrames.Add(framePath);
                frameCount++;
            }
        }

        /// <summary>
        /// Encodes captured frames to video using ffmpeg.
        /// </summary>
        private async Task EncodeToVideoAsync()
        {
            if (capturedFrames.Count == 0)
            {
                Console.WriteLine("No frames captured");
                return;
            }

            try
            {
                // Build ffmpeg command with proper non-interactive flags
                string inputPattern = Path.Combine(framesDirectory, "frame_%06d.jpg");

                // -y: overwrite without asking
                // -nostdin: disable interaction
                // -loglevel error: only show errors
                // -stats: show encoding progress
                string args = $"-y -nostdin -loglevel error -stats -framerate {TargetFps} -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p -crf 23 \"{outputFilePath}\"";

                Console.WriteLine($"Encoding {capturedFrames.Count} frames to video...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath!,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true, // Important: redirect stdin to prevent hanging
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    // Close stdin immediately to ensure FFmpeg doesn't wait for input
                    process.StandardInput.Close();

                    // Read output streams asynchronously to prevent deadlock
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();

                    // Wait for process to exit
                    await process.WaitForExitAsync();

                    // Get the output
                    string stdout = await outputTask;
                    string stderr = await errorTask;

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

        /// <summary>
        /// Finds ffmpeg executable.
        /// </summary>
        private static string? FindFfmpeg()
        {
            // Check if ffmpeg is in PATH
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();

            foreach (var dir in pathDirs)
            {
                var ffmpegPath = Path.Combine(dir, "ffmpeg.exe");
                if (File.Exists(ffmpegPath))
                {
                    return ffmpegPath;
                }
            }

            // Check common installation locations
            var commonPaths = new[]
            {
                @"C:\.tools\ffmpeg\bin\ffmpeg.exe",
                @"C:\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
                @"C:\Program Files (x86)\ffmpeg\bin\ffmpeg.exe",
                @$"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\Microsoft\WinGet\Links\ffmpeg.exe",
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

        /// <summary>
        /// Gets the path to the recorded video file.
        /// </summary>
        public string OutputFilePath => outputFilePath;

        /// <summary>
        /// Gets the directory containing recordings.
        /// </summary>
        public string OutputDirectory => outputDirectory;

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        private void Cleanup()
        {
            recordingCancellation?.Dispose();
            recordingCancellation = null;
            recordingTask = null;

            // Clean up frames directory if it exists
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

        /// <summary>
        /// Disposes resources.
        /// </summary>
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
    }
}
