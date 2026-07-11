# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Validates the bundled Command Palette UI sounds: format, peak headroom, click-free edges,
# and loudness consistency. Loudness is compared per cue group ("open-palette-*.wav" against
# each other) because different cues are intentionally mixed at different levels (selection
# ticks are quieter than open/hide, for example) while every sound set for the same cue
# should land at a similar level. Useful when sounds are added or edited manually.
# Exits non-zero when any check fails.

param(
    [string]$Path = $PSScriptRoot,
    [double]$GroupToleranceDb = 3.0,   # warn when a file strays this far from its cue-group median
    [double]$GroupFailDb = 6.0         # fail at this deviation
)

$ErrorActionPreference = 'Stop'

if (-not ('CmdPalSoundValidate.Analyzer' -as [type]))
{
    Add-Type -TypeDefinition @'
using System;
using System.IO;

namespace CmdPalSoundValidate
{
    public sealed class WavStats
    {
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int BitsPerSample { get; set; }
        public double DurationSeconds { get; set; }
        public double PeakDb { get; set; }
        public double LoudnessDb { get; set; }     // strongest 50 ms window RMS
        public double EdgeStart { get; set; }      // |first sample|, linear
        public double EdgeEnd { get; set; }        // |last sample|, linear
    }

    public static class Analyzer
    {
        public static WavStats Analyze(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") { throw new InvalidDataException("Not a RIFF file"); }
                reader.ReadInt32();
                if (new string(reader.ReadChars(4)) != "WAVE") { throw new InvalidDataException("Not a WAVE file"); }

                int channels = 0, sampleRate = 0, bits = 0;
                double[] samples = null;

                while (stream.Position + 8 <= stream.Length)
                {
                    string chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();
                    long next = stream.Position + chunkSize + (chunkSize % 2);
                    if (chunkId == "fmt ")
                    {
                        int format = reader.ReadInt16();
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadInt16();
                        bits = reader.ReadInt16();
                        if (format != 1 || bits != 16) { throw new InvalidDataException("Expected 16-bit PCM, found format " + format + " / " + bits + " bits"); }
                    }
                    else if (chunkId == "data")
                    {
                        if (channels == 0) { throw new InvalidDataException("data chunk before fmt chunk"); }
                        int frames = chunkSize / (2 * channels);
                        samples = new double[frames];
                        byte[] raw = reader.ReadBytes(chunkSize);
                        for (int f = 0; f < frames; f++)
                        {
                            // Fold to mono: mean of channels.
                            double sum = 0;
                            for (int c = 0; c < channels; c++)
                            {
                                sum += BitConverter.ToInt16(raw, (f * channels + c) * 2) / 32768.0;
                            }

                            samples[f] = sum / channels;
                        }
                    }

                    stream.Position = next;
                }

                if (samples == null || samples.Length == 0) { throw new InvalidDataException("No data chunk"); }

                double peak = 0;
                for (int i = 0; i < samples.Length; i++) { peak = Math.Max(peak, Math.Abs(samples[i])); }

                int window = Math.Max(1, (int)(sampleRate * 0.05));
                int step = Math.Max(1, (int)(sampleRate * 0.01));
                double maxRms = 0;
                for (int start = 0; start < samples.Length; start += step)
                {
                    int end = Math.Min(start + window, samples.Length);
                    if (end - start < window / 2 && start > 0) { break; }

                    double sum = 0;
                    for (int i = start; i < end; i++) { sum += samples[i] * samples[i]; }
                    maxRms = Math.Max(maxRms, Math.Sqrt(sum / (end - start)));
                }

                WavStats stats = new WavStats();
                stats.SampleRate = sampleRate;
                stats.Channels = channels;
                stats.BitsPerSample = bits;
                stats.DurationSeconds = samples.Length / (double)sampleRate;
                stats.PeakDb = ToDb(peak);
                stats.LoudnessDb = ToDb(maxRms);
                stats.EdgeStart = Math.Abs(samples[0]);
                stats.EdgeEnd = Math.Abs(samples[samples.Length - 1]);
                return stats;
            }
        }

        private static double ToDb(double linear)
        {
            return linear > 0 ? 20.0 * Math.Log10(linear) : -120.0;
        }
    }
}
'@
}

$files = Get-ChildItem -Path $Path -Filter '*.wav' | Sort-Object Name
if ($files.Count -eq 0)
{
    Write-Error "No wav files found in $Path"
}

$results = foreach ($file in $files)
{
    $stats = [CmdPalSoundValidate.Analyzer]::Analyze($file.FullName)

    # Cue group = file name without the trailing "-{set}" token.
    $stem = [IO.Path]::GetFileNameWithoutExtension($file.Name)
    $cue = if ($stem.Contains('-')) { $stem.Substring(0, $stem.LastIndexOf('-')) } else { $stem }

    [pscustomobject]@{
        Name = $file.Name
        Cue = $cue
        Duration = [Math]::Round($stats.DurationSeconds, 3)
        PeakDb = [Math]::Round($stats.PeakDb, 1)
        LoudnessDb = [Math]::Round($stats.LoudnessDb, 1)
        SampleRate = $stats.SampleRate
        Channels = $stats.Channels
        EdgeStart = $stats.EdgeStart
        EdgeEnd = $stats.EdgeEnd
        Issues = [System.Collections.Generic.List[string]]::new()
    }
}

foreach ($result in $results)
{
    if ($result.PeakDb -ge -0.1) { $result.Issues.Add('FAIL: clipping (peak at full scale)') }
    elseif ($result.PeakDb -gt -3.0) { $result.Issues.Add('WARN: peak above -3 dBFS headroom') }

    if ($result.LoudnessDb -lt -40) { $result.Issues.Add('WARN: very quiet (below -40 dBFS)') }
    if ($result.EdgeStart -gt 0.02) { $result.Issues.Add(('WARN: non-silent start ({0:0.000}), may click' -f $result.EdgeStart)) }
    if ($result.EdgeEnd -gt 0.02) { $result.Issues.Add(('WARN: non-silent end ({0:0.000}), may click' -f $result.EdgeEnd)) }
    if ($result.SampleRate -ne 44100) { $result.Issues.Add("WARN: sample rate $($result.SampleRate), bundled sounds use 44100") }
}

# Loudness consistency within each cue group.
foreach ($group in ($results | Group-Object Cue | Where-Object { $_.Count -gt 1 }))
{
    $sorted = @($group.Group.LoudnessDb | Sort-Object)
    $median = $sorted[[int](($sorted.Count - 1) / 2)]
    foreach ($result in $group.Group)
    {
        $deviation = [Math]::Abs($result.LoudnessDb - $median)
        if ($deviation -gt $GroupFailDb)
        {
            $result.Issues.Add(('FAIL: {0:0.0} dB from cue-group median ({1:0.0} dBFS)' -f $deviation, $median))
        }
        elseif ($deviation -gt $GroupToleranceDb)
        {
            $result.Issues.Add(('WARN: {0:0.0} dB from cue-group median ({1:0.0} dBFS)' -f $deviation, $median))
        }
    }
}

$results | Format-Table Name, Duration, PeakDb, LoudnessDb, @{ Label = 'Issues'; Expression = { $_.Issues -join '; ' } } -AutoSize

$warnings = @($results | Where-Object { $_.Issues -match '^WARN' })
$failures = @($results | Where-Object { $_.Issues -match '^FAIL' })
Write-Host "$($results.Count) files checked: $($failures.Count) failing, $($warnings.Count) with warnings."
if ($failures.Count -gt 0) { exit 1 }
