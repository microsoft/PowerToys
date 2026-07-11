# Copyright (c) Microsoft Corporation
# The Microsoft Corporation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

# Generates the bundled Command Palette UI sounds. The DSP engine below (embedded C#) supports
# modal synthesis with per-partial decays, two-operator FM, filtered-noise voices, attack
# transients, detuned oscillator pairs, a small Schroeder reverb, loudness matching, and TPDF
# dither. The timbre and spec tables are deliberately data-driven so tones, timing, layering,
# and stereo placement can be adjusted without touching the playback service.
# Each sound set pairs one file per cue: "{cue}-{set}.wav".

$ErrorActionPreference = 'Stop'

if (-not ('CmdPalSoundGen.Synth' -as [type]))
{
    Add-Type -TypeDefinition @'
using System;
using System.IO;

namespace CmdPalSoundGen
{
    public sealed class Partial
    {
        public double Ratio { get; set; } = 1.0;   // frequency multiple of the voice pitch
        public double Amp { get; set; } = 1.0;
        public double Decay { get; set; } = 1.0;   // 60 dB decay time as a fraction of voice duration
    }

    public sealed class Timbre
    {
        public string Mode { get; set; } = "modal";            // modal | fm | noise
        public Partial[] Partials { get; set; } = new Partial[0];
        public double Attack { get; set; } = 0.008;            // seconds
        public double DetuneCents { get; set; }                // total spread of the detuned oscillator pair
        public double AttackNoise { get; set; }                // contact-transient gain (mallet thump, key click)
        public double AttackNoiseTime { get; set; } = 0.006;   // transient decay constant, seconds
        public double Curve { get; set; }                      // optional extra (1-progress)^Curve amplitude shaping
        public bool Swell { get; set; }                        // modal only: sine-window rise-and-fall instead of strike-and-decay

        // fm mode
        public double FmModRatio { get; set; } = 2.0;
        public double FmIndex { get; set; } = 2.0;
        public double FmIndexDecay { get; set; } = 4.0;        // how fast brightness fades, in units of progress
        public double FmDecay { get; set; } = 0.8;             // 60 dB decay as a fraction of voice duration

        // noise mode
        public double NoiseBandwidth { get; set; } = 0.4;      // bandpass width relative to center frequency
        public bool NoiseSwell { get; set; }                   // true: rise-and-fall wash; false: struck decay
    }

    public sealed class Voice
    {
        public double Start { get; set; }
        public double Duration { get; set; }
        public double From { get; set; }                       // Hz (band center for noise voices)
        public double To { get; set; }
        public double Gain { get; set; } = 0.1;
        public double Pan { get; set; }
        public Timbre Timbre { get; set; }
    }

    public sealed class Spec
    {
        public string Name { get; set; } = "";
        public Voice[] Voices { get; set; } = new Voice[0];
        public double ReverbMix { get; set; } = 0.12;
        public double ReverbTime { get; set; } = 0.22;         // RT60, seconds
        public double LevelDb { get; set; }                    // loudness offset from the shared target
    }

    public static class Synth
    {
        public const int SampleRate = 44100;
        private const double Ln1000 = 6.907755278982137;       // 60 dB in nepers

        public static void Render(Spec spec, string directory, int seed)
        {
            double contentEnd = 0;
            foreach (Voice v in spec.Voices) { contentEnd = Math.Max(contentEnd, v.Start + v.Duration); }
            int count = (int)(SampleRate * (contentEnd + (spec.ReverbMix > 0 ? spec.ReverbTime * 1.6 : 0.03)));
            double[] left = new double[count];
            double[] right = new double[count];
            Random random = new Random(seed);

            foreach (Voice voice in spec.Voices) { RenderVoice(voice, left, right, random); }

            // Gentle saturation glues the layers and rounds transient peaks.
            for (int i = 0; i < count; i++)
            {
                left[i] = Math.Tanh(1.3 * left[i]) / 1.3;
                right[i] = Math.Tanh(1.3 * right[i]) / 1.3;
            }

            if (spec.ReverbMix > 0)
            {
                AddReverb(left, spec.ReverbTime, spec.ReverbMix, 1.0);
                AddReverb(right, spec.ReverbTime, spec.ReverbMix, 1.017);
            }

            int fade = (int)(SampleRate * 0.006);
            for (int i = Math.Max(0, count - fade); i < count; i++)
            {
                double f = (count - 1 - i) / (double)fade;
                left[i] *= f;
                right[i] *= f;
            }

            // Loudness match on the strongest 50 ms window, with a hard peak ceiling.
            double maxRms = MaxWindowRms(left, right);
            double peak = 0;
            for (int i = 0; i < count; i++) { peak = Math.Max(peak, Math.Max(Math.Abs(left[i]), Math.Abs(right[i]))); }
            double target = 0.089 * Math.Pow(10.0, spec.LevelDb / 20.0);
            double scale = maxRms > 0 ? target / maxRms : 1.0;
            if (peak * scale > 0.5) { scale = 0.5 / peak; }

            WriteWav(Path.Combine(directory, spec.Name), left, right, scale, random);
        }

        private static void RenderVoice(Voice voice, double[] left, double[] right, Random random)
        {
            Timbre timbre = voice.Timbre;
            int start = (int)(voice.Start * SampleRate);
            int end = Math.Min(start + (int)(voice.Duration * SampleRate), left.Length);
            double panL = Math.Sqrt((1.0 - voice.Pan) / 2.0);
            double panR = Math.Sqrt((1.0 + voice.Pan) / 2.0);
            double sweep = (voice.To - voice.From) / voice.Duration;
            double detHi = Math.Pow(2.0, timbre.DetuneCents / 2400.0);
            double detLo = 1.0 / detHi;
            double lowpass = 0;
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;

            for (int i = start; i < end; i++)
            {
                double lt = (i - start) / (double)SampleRate;
                double progress = lt / voice.Duration;
                double attack = Math.Min(1.0, lt / timbre.Attack);
                double release = Math.Min(1.0, (voice.Duration - lt) / 0.006);
                double phase = 2.0 * Math.PI * ((voice.From * lt) + (0.5 * sweep * lt * lt));
                double sample;

                if (timbre.Mode == "noise")
                {
                    // Resonant bandpass over white noise, center gliding From -> To.
                    double f = voice.From + ((voice.To - voice.From) * progress);
                    double q = 1.0 / Math.Max(0.05, timbre.NoiseBandwidth);
                    double w = 2.0 * Math.PI * f / SampleRate;
                    double alpha = Math.Sin(w) / (2.0 * q);
                    double x0 = (random.NextDouble() * 2.0) - 1.0;
                    double y0 = ((alpha * x0) - (alpha * x2) + (2.0 * Math.Cos(w) * y1) - ((1.0 - alpha) * y2)) / (1.0 + alpha);
                    x2 = x1; x1 = x0; y2 = y1; y1 = y0;
                    double env = timbre.NoiseSwell
                        ? Math.Pow(Math.Sin(Math.PI * Math.Min(1.0, progress)), 1.4)
                        : attack * Math.Pow(1.0 - progress, 2.0);
                    sample = y0 * env;
                }
                else if (timbre.Mode == "fm")
                {
                    double env = Math.Exp(-Ln1000 * lt / (voice.Duration * timbre.FmDecay));
                    double index = timbre.FmIndex * Math.Exp(-timbre.FmIndexDecay * progress);
                    double hi = Math.Sin((phase * detHi) + (index * Math.Sin(timbre.FmModRatio * phase * detHi)));
                    double lo = Math.Sin((phase * detLo) + (index * Math.Sin(timbre.FmModRatio * phase * detLo)));
                    sample = 0.5 * (hi + lo) * env * attack;
                }
                else
                {
                    // Modal: every partial rings with its own decay so highs die first.
                    double tone = 0;
                    for (int p = 0; p < timbre.Partials.Length; p++)
                    {
                        Partial partial = timbre.Partials[p];
                        double env = Math.Exp(-Ln1000 * lt / (voice.Duration * partial.Decay));
                        double ph = partial.Ratio * phase;
                        tone += partial.Amp * env * 0.5 * (Math.Sin(ph * detHi) + Math.Sin(ph * detLo));
                    }

                    if (timbre.Curve > 0) { tone *= Math.Pow(1.0 - progress, timbre.Curve); }

                    // Swell: gentle rise to mid-gesture and back down; the partial decays
                    // above still dull the spectrum over time.
                    double shape = timbre.Swell ? Math.Pow(Math.Sin(Math.PI * progress), 1.2) : attack;
                    sample = tone * shape;
                }

                if (timbre.AttackNoise > 0 && timbre.Mode != "noise")
                {
                    // Low-passed noise burst at onset reads as physical contact.
                    double burst = (random.NextDouble() * 2.0) - 1.0;
                    double k = Math.Min(0.9, 2.0 * Math.PI * Math.Min(8000.0, voice.From * 6.0) / SampleRate);
                    lowpass += k * (burst - lowpass);
                    sample += lowpass * timbre.AttackNoise * Math.Exp(-lt / timbre.AttackNoiseTime);
                }

                sample *= voice.Gain * release;
                left[i] += sample * panL;
                right[i] += sample * panR;
            }
        }

        private static void AddReverb(double[] x, double time, double mix, double stretch)
        {
            // Schroeder: four parallel combs into two series allpasses. The per-channel
            // stretch decorrelates left/right for a wider tail.
            int n = x.Length;
            double[] wet = new double[n];
            double[] combMs = { 29.7, 37.1, 41.1, 43.7 };
            foreach (double ms in combMs)
            {
                int delay = Math.Max(1, (int)(SampleRate * ms / 1000.0 * stretch));
                double g = Math.Pow(10.0, -3.0 * (ms / 1000.0) / time);
                double[] buf = new double[n];
                for (int i = 0; i < n; i++)
                {
                    double delayed = i >= delay ? buf[i - delay] : 0.0;
                    buf[i] = x[i] + (g * delayed);
                    wet[i] += buf[i] * 0.25;
                }
            }

            double[] allpassMs = { 5.0, 1.7 };
            foreach (double ms in allpassMs)
            {
                int delay = Math.Max(1, (int)(SampleRate * ms / 1000.0 * stretch));
                const double g = 0.7;
                double[] input = new double[n];
                double[] output = new double[n];
                for (int i = 0; i < n; i++)
                {
                    input[i] = wet[i];
                    double inDelayed = i >= delay ? input[i - delay] : 0.0;
                    double outDelayed = i >= delay ? output[i - delay] : 0.0;
                    output[i] = (-g * wet[i]) + inDelayed + (g * outDelayed);
                }

                Array.Copy(output, wet, n);
            }

            for (int i = 0; i < n; i++) { x[i] += wet[i] * mix; }
        }

        private static double MaxWindowRms(double[] left, double[] right)
        {
            int window = (int)(SampleRate * 0.05);
            int step = (int)(SampleRate * 0.01);
            double max = 0;
            for (int startIdx = 0; startIdx < left.Length; startIdx += step)
            {
                int endIdx = Math.Min(startIdx + window, left.Length);
                if (endIdx - startIdx < window / 2) { break; }

                double sum = 0;
                for (int i = startIdx; i < endIdx; i++) { sum += ((left[i] * left[i]) + (right[i] * right[i])) * 0.5; }
                max = Math.Max(max, Math.Sqrt(sum / (endIdx - startIdx)));
            }

            return max;
        }

        private static void WriteWav(string path, double[] left, double[] right, double scale, Random random)
        {
            using (FileStream stream = File.Open(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                int n = left.Length;
                int dataSize = n * 4;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataSize);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)2);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 4);
                writer.Write((short)4);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);
                for (int i = 0; i < n; i++)
                {
                    writer.Write(Quantize(left[i] * scale, random));
                    writer.Write(Quantize(right[i] * scale, random));
                }
            }
        }

        private static short Quantize(double value, Random random)
        {
            // TPDF dither: one LSB of triangular noise before truncating to 16-bit.
            double s = (value * 32767.0) + (random.NextDouble() - random.NextDouble());
            if (s > 32767.0) { s = 32767.0; }
            if (s < -32768.0) { s = -32768.0; }
            return (short)Math.Round(s);
        }
    }
}
'@
}

function New-Partial([double]$Ratio, [double]$Amp, [double]$Decay)
{
    [CmdPalSoundGen.Partial]@{ Ratio = $Ratio; Amp = $Amp; Decay = $Decay }
}

function New-Voice([double]$Start, [double]$Duration, [double]$From, [double]$To, [double]$Gain, [double]$Pan = 0, [object]$Timbre = $null)
{
    [CmdPalSoundGen.Voice]@{ Start = $Start; Duration = $Duration; From = $From; To = $To; Gain = $Gain; Pan = $Pan; Timbre = $Timbre }
}

function New-Spec([string]$Name, [object]$Timbre, [object[]]$Voices, [double]$ReverbMix = 0.12, [double]$ReverbTime = 0.22, [double]$LevelDb = 0)
{
    foreach ($voice in $Voices)
    {
        if ($null -eq $voice.Timbre) { $voice.Timbre = $Timbre }
    }

    [CmdPalSoundGen.Spec]@{ Name = $Name; Voices = $Voices; ReverbMix = $ReverbMix; ReverbTime = $ReverbTime; LevelDb = $LevelDb }
}

# <timbres>
$soft = [CmdPalSoundGen.Timbre]@{
    Partials = @((New-Partial 1 1.0 1.0), (New-Partial 2 0.10 0.55), (New-Partial 0.5 0.05 0.9))
    Attack = 0.010; DetuneCents = 4
}
$softSwell = [CmdPalSoundGen.Timbre]@{
    # Long open/hide gestures: rise-and-fall window, slow partial decay so the glide carries.
    Partials = @((New-Partial 1 1.0 2.5), (New-Partial 2 0.10 1.2), (New-Partial 0.5 0.05 2.2))
    Swell = $true; DetuneCents = 4
}
$warm = [CmdPalSoundGen.Timbre]@{
    Partials = @((New-Partial 1 1.0 1.0), (New-Partial 2 0.22 0.6), (New-Partial 3 0.08 0.4), (New-Partial 0.5 0.06 0.95))
    Attack = 0.016; DetuneCents = 5; AttackNoise = 0.025; AttackNoiseTime = 0.010
}
$warmSwell = [CmdPalSoundGen.Timbre]@{
    Partials = @((New-Partial 1 1.0 2.5), (New-Partial 2 0.22 1.2), (New-Partial 3 0.06 0.7), (New-Partial 0.5 0.07 2.4))
    Swell = $true; DetuneCents = 6
}
$glass = [CmdPalSoundGen.Timbre]@{
    # Inharmonic bell partials, near-instant strike, fast-dying shimmer.
    Partials = @((New-Partial 1 1.0 1.0), (New-Partial 2.76 0.45 0.5), (New-Partial 5.40 0.22 0.3), (New-Partial 8.93 0.09 0.18))
    Attack = 0.0015; DetuneCents = 2; AttackNoise = 0.06; AttackNoiseTime = 0.004
}
$calm = [CmdPalSoundGen.Timbre]@{
    # Felt-piano-ish: soft hammer, muffled upper harmonics.
    Partials = @((New-Partial 1 1.0 1.0), (New-Partial 2 0.30 0.4), (New-Partial 3 0.10 0.22), (New-Partial 4 0.04 0.12))
    Attack = 0.007; DetuneCents = 3; AttackNoise = 0.05; AttackNoiseTime = 0.012
}
$chip = [CmdPalSoundGen.Timbre]@{
    # Static odd-harmonic stack (square-ish); Curve shapes the blip instead of modal decay.
    Partials = @((New-Partial 1 1.0 60), (New-Partial 3 0.33 60), (New-Partial 5 0.20 60), (New-Partial 7 0.14 60))
    Attack = 0.002; Curve = 0.35
}
$electric = [CmdPalSoundGen.Timbre]@{
    Mode = 'fm'; FmModRatio = 1.401; FmIndex = 3.2; FmIndexDecay = 4.5; FmDecay = 0.75
    Attack = 0.004; DetuneCents = 6
}
$breezeSwell = [CmdPalSoundGen.Timbre]@{ Mode = 'noise'; NoiseBandwidth = 0.5; NoiseSwell = $true }
$breezeTap = [CmdPalSoundGen.Timbre]@{ Mode = 'noise'; NoiseBandwidth = 0.7; Attack = 0.004 }
# </timbres>

# Note frequencies: soft/warm sit on a G-major cluster (G3 196.00, A3 220.00, B3 246.94,
# C4 261.63, C#4 277.18, D4 293.66, E4 329.63, F4 349.23, F3 174.61, E3 164.81).
# glass uses E-major pentatonic two octaves up, calm sits an octave below soft,
# retro uses a C-major arcade set, electric rides A4/E5 zap sweeps, breeze values are
# noise band centers in Hz rather than pitches.
$specs = @(
    # <soft: light, airy sweeps a fourth above warm; open/hide swell instead of striking>
    (New-Spec 'open-palette-soft.wav' $softSwell @(
        (New-Voice 0.000 0.36 261.63 392.00 0.14 -0.20),
        (New-Voice 0.070 0.32 329.63 440.00 0.09 0.20)) 0.12 0.24)
    (New-Spec 'hide-palette-soft.wav' $softSwell @(
        (New-Voice 0.000 0.34 392.00 261.63 0.13 0.20),
        (New-Voice 0.060 0.30 329.63 220.00 0.08 -0.20)) 0.12 0.24)
    (New-Spec 'page-forward-soft.wav' $soft @(
        (New-Voice 0.000 0.12 329.63 392.00 0.14 -0.20),
        (New-Voice 0.022 0.10 392.00 440.00 0.07 0.20)) 0.10 0.20 -2)
    (New-Spec 'page-back-soft.wav' $soft @(
        (New-Voice 0.000 0.12 440.00 392.00 0.12 0.20),
        (New-Voice 0.022 0.10 392.00 329.63 0.08 -0.20)) 0.10 0.20 -2)
    (New-Spec 'action-execution-soft.wav' $soft @(
        (New-Voice 0.000 0.16 329.63 329.63 0.14 -0.15),
        (New-Voice 0.028 0.14 392.00 440.00 0.11 0.15)) 0.10 0.20)
    (New-Spec 'selection-change-soft.wav' $soft @(
        (New-Voice 0.000 0.055 293.66 261.63 0.095 0.00)) 0.06 0.15 -8)
    (New-Spec 'focus-change-soft.wav' $soft @(
        (New-Voice 0.000 0.045 329.63 293.66 0.08 0.00)) 0.05 0.14 -10)
    (New-Spec 'confirmation-popup-soft.wav' $soft @(
        (New-Voice 0.000 0.17 329.63 329.63 0.13 -0.20),
        (New-Voice 0.090 0.15 392.00 440.00 0.13 0.20)) 0.10 0.20)
    (New-Spec 'toast-shown-soft.wav' $soft @(
        (New-Voice 0.000 0.17 329.63 392.00 0.12 -0.20),
        (New-Voice 0.075 0.15 440.00 523.25 0.11 0.20)) 0.10 0.20)
    (New-Spec 'status-message-soft.wav' $soft @(
        (New-Voice 0.000 0.11 293.66 329.63 0.105 0.00)) 0.08 0.18 -3)

    # <warm: layered low sweeps, fuller harmonics, soft contact thump; open/hide swell>
    (New-Spec 'open-palette-warm.wav' $warmSwell @(
        (New-Voice 0.000 0.44 146.83 220.00 0.15 -0.30),
        (New-Voice 0.070 0.40 196.00 261.63 0.11 0.05),
        (New-Voice 0.140 0.34 246.94 329.63 0.07 0.30)) 0.15 0.28)
    (New-Spec 'hide-palette-warm.wav' $warmSwell @(
        (New-Voice 0.000 0.42 293.66 174.61 0.13 0.25),
        (New-Voice 0.070 0.38 246.94 146.83 0.10 -0.05),
        (New-Voice 0.140 0.34 196.00 130.81 0.06 -0.30)) 0.15 0.28)
    (New-Spec 'page-forward-warm.wav' $warm @(
        (New-Voice 0.000 0.13 196.00 246.94 0.14 -0.30),
        (New-Voice 0.018 0.12 246.94 293.66 0.10 0.30)) 0.13 0.24 -2)
    (New-Spec 'page-back-warm.wav' $warm @(
        (New-Voice 0.000 0.13 293.66 246.94 0.13 0.30),
        (New-Voice 0.018 0.12 246.94 196.00 0.10 -0.30)) 0.13 0.24 -2)
    (New-Spec 'action-execution-warm.wav' $warm @(
        (New-Voice 0.000 0.19 196.00 196.00 0.13 -0.30),
        (New-Voice 0.018 0.18 246.94 246.94 0.11 0.00),
        (New-Voice 0.045 0.16 293.66 329.63 0.09 0.30)) 0.13 0.24)
    (New-Spec 'selection-change-warm.wav' $warm @(
        (New-Voice 0.000 0.065 220.00 196.00 0.10 -0.10),
        (New-Voice 0.008 0.055 164.81 164.81 0.05 0.10)) 0.08 0.18 -8)
    (New-Spec 'focus-change-warm.wav' $warm @(
        (New-Voice 0.000 0.050 246.94 220.00 0.08 0.00)) 0.06 0.16 -10)
    (New-Spec 'confirmation-popup-warm.wav' $warm @(
        (New-Voice 0.000 0.21 196.00 196.00 0.12 -0.25),
        (New-Voice 0.070 0.19 246.94 261.63 0.11 0.00),
        (New-Voice 0.115 0.15 293.66 329.63 0.07 0.25)) 0.13 0.24)
    (New-Spec 'toast-shown-warm.wav' $warm @(
        (New-Voice 0.000 0.22 196.00 220.00 0.12 -0.30),
        (New-Voice 0.040 0.20 246.94 277.18 0.10 0.00),
        (New-Voice 0.100 0.15 329.63 349.23 0.07 0.30)) 0.13 0.24)
    (New-Spec 'status-message-warm.wav' $warm @(
        (New-Voice 0.000 0.12 164.81 196.00 0.10 -0.15),
        (New-Voice 0.025 0.10 220.00 246.94 0.07 0.15)) 0.11 0.22 -3)

    # <glass: bright bell strikes with shimmering inharmonic decay>
    (New-Spec 'open-palette-glass.wav' $glass @(
        (New-Voice 0.000 0.30 659.26 659.26 0.11 -0.20),
        (New-Voice 0.060 0.26 987.77 987.77 0.08 0.20),
        (New-Voice 0.110 0.22 1318.51 1318.51 0.05 0.00)) 0.16 0.30)
    (New-Spec 'hide-palette-glass.wav' $glass @(
        (New-Voice 0.000 0.26 987.77 987.77 0.10 0.20),
        (New-Voice 0.060 0.28 659.26 659.26 0.08 -0.20)) 0.16 0.30)
    (New-Spec 'page-forward-glass.wav' $glass @(
        (New-Voice 0.000 0.16 830.61 830.61 0.09 -0.15),
        (New-Voice 0.045 0.16 987.77 987.77 0.07 0.15)) 0.16 0.30 -2)
    (New-Spec 'page-back-glass.wav' $glass @(
        (New-Voice 0.000 0.16 987.77 987.77 0.09 0.15),
        (New-Voice 0.045 0.16 830.61 830.61 0.07 -0.15)) 0.16 0.30 -2)
    (New-Spec 'action-execution-glass.wav' $glass @(
        (New-Voice 0.000 0.24 659.26 659.26 0.10 -0.10),
        (New-Voice 0.020 0.20 1318.51 1318.51 0.06 0.10)) 0.16 0.30)
    (New-Spec 'selection-change-glass.wav' $glass @(
        (New-Voice 0.000 0.07 1108.73 1108.73 0.05 0.00)) 0.08 0.18 -8)
    (New-Spec 'focus-change-glass.wav' $glass @(
        (New-Voice 0.000 0.05 1318.51 1318.51 0.04 0.00)) 0.07 0.16 -10)
    (New-Spec 'confirmation-popup-glass.wav' $glass @(
        (New-Voice 0.000 0.20 830.61 830.61 0.10 -0.15),
        (New-Voice 0.090 0.22 1108.73 1108.73 0.08 0.15)) 0.16 0.30)
    (New-Spec 'toast-shown-glass.wav' $glass @(
        (New-Voice 0.000 0.20 987.77 987.77 0.09 -0.15),
        (New-Voice 0.070 0.24 1318.51 1318.51 0.07 0.15)) 0.16 0.30)
    (New-Spec 'status-message-glass.wav' $glass @(
        (New-Voice 0.000 0.14 739.99 739.99 0.08 0.00)) 0.14 0.26 -3)

    # <calm: felt-piano strikes an octave down, almost subliminal>
    (New-Spec 'open-palette-calm.wav' $calm @(
        (New-Voice 0.000 0.40 98.00 98.00 0.12 -0.15),
        (New-Voice 0.050 0.38 146.83 146.83 0.09 0.10),
        (New-Voice 0.100 0.34 196.00 196.00 0.06 0.00)) 0.18 0.32)
    (New-Spec 'hide-palette-calm.wav' $calm @(
        (New-Voice 0.000 0.34 196.00 196.00 0.10 0.15),
        (New-Voice 0.050 0.38 146.83 146.83 0.09 -0.05),
        (New-Voice 0.100 0.40 98.00 98.00 0.08 -0.15)) 0.18 0.32)
    (New-Spec 'page-forward-calm.wav' $calm @(
        (New-Voice 0.000 0.26 164.81 164.81 0.11 -0.15),
        (New-Voice 0.040 0.26 196.00 196.00 0.08 0.15)) 0.18 0.32 -2)
    (New-Spec 'page-back-calm.wav' $calm @(
        (New-Voice 0.000 0.26 196.00 196.00 0.11 0.15),
        (New-Voice 0.040 0.26 164.81 164.81 0.08 -0.15)) 0.18 0.32 -2)
    (New-Spec 'action-execution-calm.wav' $calm @(
        (New-Voice 0.000 0.30 130.81 130.81 0.11 -0.10),
        (New-Voice 0.030 0.28 196.00 196.00 0.08 0.10)) 0.18 0.32)
    (New-Spec 'selection-change-calm.wav' $calm @(
        (New-Voice 0.000 0.10 98.00 98.00 0.06 0.00)) 0.10 0.20 -8)
    (New-Spec 'focus-change-calm.wav' $calm @(
        (New-Voice 0.000 0.08 130.81 130.81 0.05 0.00)) 0.08 0.18 -10)
    (New-Spec 'confirmation-popup-calm.wav' $calm @(
        (New-Voice 0.000 0.28 164.81 164.81 0.11 -0.15),
        (New-Voice 0.100 0.30 220.00 220.00 0.09 0.15)) 0.18 0.32)
    (New-Spec 'toast-shown-calm.wav' $calm @(
        (New-Voice 0.000 0.28 146.83 146.83 0.10 -0.15),
        (New-Voice 0.080 0.30 196.00 196.00 0.08 0.15)) 0.18 0.32)
    (New-Spec 'status-message-calm.wav' $calm @(
        (New-Voice 0.000 0.18 164.81 164.81 0.09 0.00)) 0.14 0.26 -3)

    # <breeze: airy filtered-noise washes; From/To are band centers>
    (New-Spec 'open-palette-breeze.wav' $breezeSwell @(
        (New-Voice 0.000 0.35 350 1000 0.50 -0.10),
        (New-Voice 0.050 0.30 700 1600 0.25 0.15)) 0.14 0.25)
    (New-Spec 'hide-palette-breeze.wav' $breezeSwell @(
        (New-Voice 0.000 0.33 1000 320 0.50 0.10),
        (New-Voice 0.040 0.28 1600 650 0.25 -0.15)) 0.14 0.25)
    (New-Spec 'page-forward-breeze.wav' $breezeSwell @(
        (New-Voice 0.000 0.16 600 1100 0.45 -0.10)) 0.14 0.25 -2)
    (New-Spec 'page-back-breeze.wav' $breezeSwell @(
        (New-Voice 0.000 0.16 1100 550 0.45 0.10)) 0.14 0.25 -2)
    (New-Spec 'action-execution-breeze.wav' $breezeTap @(
        (New-Voice 0.000 0.12 900 700 0.50 -0.05),
        (New-Voice 0.050 0.14 1300 1100 0.35 0.10)) 0.14 0.25)
    (New-Spec 'selection-change-breeze.wav' $breezeTap @(
        (New-Voice 0.000 0.05 1600 1400 0.30 0.00)) 0.08 0.18 -8)
    (New-Spec 'focus-change-breeze.wav' $breezeTap @(
        (New-Voice 0.000 0.04 1900 1700 0.25 0.00)) 0.06 0.16 -10)
    (New-Spec 'confirmation-popup-breeze.wav' $breezeSwell @(
        (New-Voice 0.000 0.18 500 750 0.45 -0.10),
        (New-Voice 0.120 0.20 750 1050 0.40 0.10)) 0.14 0.25)
    (New-Spec 'toast-shown-breeze.wav' $breezeSwell @(
        (New-Voice 0.000 0.26 500 1300 0.50 0.00)) 0.14 0.25)
    (New-Spec 'status-message-breeze.wav' $breezeTap @(
        (New-Voice 0.000 0.10 950 800 0.45 0.00)) 0.12 0.22 -3)

    # <retro: dry chiptune blips stepping through tiny arpeggios>
    (New-Spec 'open-palette-retro.wav' $chip @(
        (New-Voice 0.000 0.050 523.25 523.25 0.070 -0.10),
        (New-Voice 0.055 0.050 659.26 659.26 0.070 0.00),
        (New-Voice 0.110 0.080 783.99 783.99 0.070 0.10)) 0.05 0.15 -2)
    (New-Spec 'hide-palette-retro.wav' $chip @(
        (New-Voice 0.000 0.050 783.99 783.99 0.070 0.10),
        (New-Voice 0.055 0.050 659.26 659.26 0.070 0.00),
        (New-Voice 0.110 0.080 523.25 523.25 0.070 -0.10)) 0.05 0.15 -2)
    (New-Spec 'page-forward-retro.wav' $chip @(
        (New-Voice 0.000 0.045 659.26 659.26 0.065 -0.10),
        (New-Voice 0.050 0.060 783.99 783.99 0.065 0.10)) 0.05 0.15 -3)
    (New-Spec 'page-back-retro.wav' $chip @(
        (New-Voice 0.000 0.045 783.99 783.99 0.065 0.10),
        (New-Voice 0.050 0.060 659.26 659.26 0.065 -0.10)) 0.05 0.15 -3)
    (New-Spec 'action-execution-retro.wav' $chip @(
        (New-Voice 0.000 0.050 523.25 523.25 0.070 0.00),
        (New-Voice 0.055 0.090 1046.50 1046.50 0.055 0.00)) 0.05 0.15 -2)
    (New-Spec 'selection-change-retro.wav' $chip @(
        (New-Voice 0.000 0.035 783.99 783.99 0.045 0.00)) 0.03 0.12 -9)
    (New-Spec 'focus-change-retro.wav' $chip @(
        (New-Voice 0.000 0.030 659.26 659.26 0.040 0.00)) 0.03 0.12 -11)
    (New-Spec 'confirmation-popup-retro.wav' $chip @(
        (New-Voice 0.000 0.070 392.00 392.00 0.070 -0.10),
        (New-Voice 0.080 0.140 523.25 523.25 0.065 0.10)) 0.05 0.15 -2)
    (New-Spec 'toast-shown-retro.wav' $chip @(
        (New-Voice 0.000 0.050 659.26 659.26 0.065 -0.10),
        (New-Voice 0.055 0.050 783.99 783.99 0.065 0.00),
        (New-Voice 0.110 0.090 1046.50 1046.50 0.050 0.10)) 0.05 0.15 -2)
    (New-Spec 'status-message-retro.wav' $chip @(
        (New-Voice 0.000 0.070 440.00 440.00 0.070 0.00)) 0.04 0.12 -4)

    # <electric: FM zaps and pings with a metallic 1.4:1 modulator>
    (New-Spec 'open-palette-electric.wav' $electric @(
        (New-Voice 0.000 0.22 440.00 880.00 0.11 -0.15),
        (New-Voice 0.040 0.18 660.00 1320.00 0.07 0.15)) 0.13 0.26)
    (New-Spec 'hide-palette-electric.wav' $electric @(
        (New-Voice 0.000 0.22 880.00 440.00 0.11 0.15),
        (New-Voice 0.040 0.18 1320.00 660.00 0.07 -0.15)) 0.13 0.26)
    (New-Spec 'page-forward-electric.wav' $electric @(
        (New-Voice 0.000 0.12 550.00 880.00 0.10 -0.10)) 0.13 0.26 -2)
    (New-Spec 'page-back-electric.wav' $electric @(
        (New-Voice 0.000 0.12 880.00 550.00 0.10 0.10)) 0.13 0.26 -2)
    (New-Spec 'action-execution-electric.wav' $electric @(
        (New-Voice 0.000 0.14 440.00 440.00 0.10 -0.05),
        (New-Voice 0.030 0.12 880.00 1760.00 0.07 0.10)) 0.13 0.26)
    (New-Spec 'selection-change-electric.wav' $electric @(
        (New-Voice 0.000 0.05 990.00 990.00 0.05 0.00)) 0.07 0.16 -8)
    (New-Spec 'focus-change-electric.wav' $electric @(
        (New-Voice 0.000 0.04 1320.00 1320.00 0.04 0.00)) 0.06 0.15 -10)
    (New-Spec 'confirmation-popup-electric.wav' $electric @(
        (New-Voice 0.000 0.15 587.33 587.33 0.10 -0.10),
        (New-Voice 0.090 0.18 880.00 880.00 0.08 0.10)) 0.13 0.26)
    (New-Spec 'toast-shown-electric.wav' $electric @(
        (New-Voice 0.000 0.20 700.00 1400.00 0.09 -0.10),
        (New-Voice 0.080 0.16 1050.00 2100.00 0.06 0.10)) 0.13 0.26)
    (New-Spec 'status-message-electric.wav' $electric @(
        (New-Voice 0.000 0.12 660.00 660.00 0.09 0.00)) 0.11 0.22 -3)
)

for ($i = 0; $i -lt $specs.Count; $i++)
{
    [CmdPalSoundGen.Synth]::Render($specs[$i], $PSScriptRoot, 1701 + $i)
}

Write-Host "Generated $($specs.Count) audio cue WAV files in $PSScriptRoot"
