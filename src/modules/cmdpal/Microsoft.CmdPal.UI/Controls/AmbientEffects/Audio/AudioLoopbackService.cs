// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Audio;

/// <summary>
/// Captures system audio via WASAPI loopback, runs FFT, and exposes
/// real-time frequency band levels. Runs on a dedicated background thread.
/// </summary>
internal sealed class AudioLoopbackService : IDisposable
{
    private const int FFTSize = 1024;
    private const int HalfFFT = FFTSize / 2;
    private const float AttackCoeff = 0.8f;
    private const float DecayCoeff = 0.12f;

    private static readonly Guid AudioClientGuid = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    private static readonly Guid AudioCaptureClientGuid = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");

    private readonly object _lock = new();
    private readonly float[] _smoothedBands;
    private readonly int _bandCount;
    private readonly int[] _bandBinEdges;

    private IAudioClient? _audioClient;
    private IAudioCaptureClient? _captureClient;
    private Thread? _captureThread;
    private volatile bool _running;
    private uint _sampleRate;
    private ushort _channels;
    private bool _isFloat;

    public bool IsCapturing => _running;

    public AudioLoopbackService(int bandCount = 48)
    {
        _bandCount = bandCount;
        _smoothedBands = new float[bandCount];
        _bandBinEdges = ComputeLogBandEdges(bandCount, HalfFFT, 20f, 20000f);
    }

    public bool Start()
    {
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorClass();
            var hr = enumerator.GetDefaultAudioEndpoint(
                AudioConstants.EDataFlow_eRender,
                AudioConstants.ERole_eConsole,
                out var device);

            if (hr != 0 || device == null)
            {
                return false;
            }

            var audioClientGuid = AudioClientGuid;
            hr = device.Activate(ref audioClientGuid, AudioConstants.CLSCTX_ALL, IntPtr.Zero, out var clientObj);
            if (hr != 0 || clientObj == null)
            {
                return false;
            }

            _audioClient = (IAudioClient)clientObj;

            hr = _audioClient.GetMixFormat(out var formatPtr);
            if (hr != 0 || formatPtr == IntPtr.Zero)
            {
                return false;
            }

            var format = Marshal.PtrToStructure<WaveFormatEx>(formatPtr);
            _sampleRate = format.nSamplesPerSec;
            _channels = format.nChannels;
            _isFloat = format.wFormatTag == 3 || (format.wFormatTag == 0xFFFE && format.wBitsPerSample == 32);

            hr = _audioClient.Initialize(
                0, // AUDCLNT_SHAREMODE_SHARED
                AudioConstants.AUDCLNT_STREAMFLAGS_LOOPBACK,
                1000000, // 100ms in 100-ns units
                0,
                formatPtr,
                IntPtr.Zero);

            Marshal.FreeCoTaskMem(formatPtr);

            if (hr != 0)
            {
                return false;
            }

            var captureGuid = AudioCaptureClientGuid;
            hr = _audioClient.GetService(ref captureGuid, out var captureObj);
            if (hr != 0 || captureObj == null)
            {
                return false;
            }

            _captureClient = (IAudioCaptureClient)captureObj;

            _audioClient.Start();
            _running = true;
            _captureThread = new Thread(CaptureLoop) { IsBackground = true, Name = "AudioLoopback" };
            _captureThread.Start();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to initialize audio loopback: {ex.Message}");
            return false;
        }
    }

    public void GetBandLevels(float[] output)
    {
        lock (_lock)
        {
            var count = Math.Min(output.Length, _smoothedBands.Length);
            Array.Copy(_smoothedBands, output, count);
        }
    }

    public void Dispose()
    {
        _running = false;
        _captureThread?.Join(500);
        _captureThread = null;

        try
        {
            _audioClient?.Stop();
        }
        catch
        {
        }

        if (_captureClient is IDisposable captureDisp)
        {
            captureDisp.Dispose();
        }

        if (_audioClient is IDisposable clientDisp)
        {
            clientDisp.Dispose();
        }

        _captureClient = null;
        _audioClient = null;
    }

    private void CaptureLoop()
    {
        var fftBuffer = new float[FFTSize * 2];
        var magnitudes = new float[HalfFFT];
        var sampleAccumulator = new float[FFTSize];
        var accumulatedCount = 0;

        while (_running)
        {
            Thread.Sleep(30);

            if (_captureClient == null)
            {
                break;
            }

            try
            {
                DrainSamples(sampleAccumulator, ref accumulatedCount);

                if (accumulatedCount >= FFTSize)
                {
                    ProcessFFT(sampleAccumulator, fftBuffer, magnitudes);
                    UpdateBands(magnitudes);
                    accumulatedCount = 0;
                }
            }
            catch
            {
                break;
            }
        }
    }

    private void DrainSamples(float[] accumulator, ref int count)
    {
        if (_captureClient == null)
        {
            return;
        }

        while (true)
        {
            var hr = _captureClient.GetNextPacketSize(out var packetSize);
            if (hr != 0 || packetSize == 0)
            {
                break;
            }

            hr = _captureClient.GetBuffer(out var dataPtr, out var numFrames, out var flags, out _, out _);
            if (hr != 0)
            {
                break;
            }

            var isSilent = (flags & AudioConstants.AUDCLNT_BUFFERFLAGS_SILENT) != 0;
            var channels = Math.Max(_channels, (ushort)1);

            for (uint f = 0; f < numFrames && count < FFTSize; f++)
            {
                if (isSilent)
                {
                    accumulator[count++] = 0f;
                }
                else if (_isFloat)
                {
                    var sum = 0f;
                    for (var ch = 0; ch < channels; ch++)
                    {
                        sum += Marshal.PtrToStructure<float>(dataPtr + ((((int)f * channels) + ch) * sizeof(float)));
                    }

                    accumulator[count++] = sum / channels;
                }
                else
                {
                    var sum = 0f;
                    for (var ch = 0; ch < channels; ch++)
                    {
                        var sample = Marshal.PtrToStructure<short>(dataPtr + ((((int)f * channels) + ch) * sizeof(short)));
                        sum += sample / 32768f;
                    }

                    accumulator[count++] = sum / channels;
                }
            }

            _captureClient.ReleaseBuffer(numFrames);
        }
    }

    private static void ProcessFFT(float[] samples, float[] fftBuffer, float[] magnitudes)
    {
        SimpleFFT.ApplyHanningWindow(samples, FFTSize);

        Array.Clear(fftBuffer);
        for (var i = 0; i < FFTSize; i++)
        {
            fftBuffer[2 * i] = samples[i];
        }

        SimpleFFT.ComputeFFT(fftBuffer, FFTSize);
        SimpleFFT.GetMagnitudes(fftBuffer, magnitudes, FFTSize);
    }

    private void UpdateBands(float[] magnitudes)
    {
        lock (_lock)
        {
            for (var b = 0; b < _bandCount; b++)
            {
                var startBin = _bandBinEdges[b];
                var endBin = _bandBinEdges[b + 1];
                endBin = Math.Max(endBin, startBin + 1);

                var sum = 0f;
                for (var i = startBin; i < endBin && i < magnitudes.Length; i++)
                {
                    sum += magnitudes[i];
                }

                var raw = sum / (endBin - startBin) * 25f;
                raw = Math.Clamp(raw, 0f, 1f);

                if (raw > _smoothedBands[b])
                {
                    _smoothedBands[b] += (raw - _smoothedBands[b]) * AttackCoeff;
                }
                else
                {
                    _smoothedBands[b] += (raw - _smoothedBands[b]) * DecayCoeff;
                }
            }
        }
    }

    private static int[] ComputeLogBandEdges(int bandCount, int totalBins, float minFreq, float maxFreq)
    {
        var edges = new int[bandCount + 1];
        var logMin = MathF.Log10(minFreq);
        var logMax = MathF.Log10(maxFreq);

        for (var i = 0; i <= bandCount; i++)
        {
            var logFreq = logMin + ((logMax - logMin) * i / bandCount);
            var freq = MathF.Pow(10, logFreq);
            var bin = (int)(freq / 20000f * totalBins);
            edges[i] = Math.Clamp(bin, 0, totalBins);
        }

        return edges;
    }
}
