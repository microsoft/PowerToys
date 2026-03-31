// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Controls.AmbientEffects.Audio;

/// <summary>
/// Minimal in-place Cooley-Tukey radix-2 FFT.
/// Operates on interleaved real/imaginary float arrays.
/// </summary>
internal static class SimpleFFT
{
    /// <summary>
    /// Applies a Hanning window to the real-valued samples in place.
    /// </summary>
    public static void ApplyHanningWindow(float[] samples, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var multiplier = 0.5f * (1f - MathF.Cos((2f * MathF.PI * i) / (count - 1)));
            samples[i] *= multiplier;
        }
    }

    /// <summary>
    /// Computes an in-place FFT on interleaved real/imaginary data.
    /// <paramref name="data"/> must have length = 2 * N where N is a power of 2.
    /// data[2*k] = real part, data[2*k+1] = imaginary part.
    /// </summary>
    public static void ComputeFFT(float[] data, int n)
    {
        // Bit-reversal permutation
        var j = 0;
        for (var i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (data[2 * i], data[2 * j]) = (data[2 * j], data[2 * i]);
                (data[(2 * i) + 1], data[(2 * j) + 1]) = (data[(2 * j) + 1], data[(2 * i) + 1]);
            }

            var m = n >> 1;
            while (m >= 1 && j >= m)
            {
                j -= m;
                m >>= 1;
            }

            j += m;
        }

        // Cooley-Tukey butterfly
        for (var step = 1; step < n; step <<= 1)
        {
            var angleStep = -MathF.PI / step;
            var wR = MathF.Cos(angleStep);
            var wI = MathF.Sin(angleStep);

            for (var group = 0; group < n; group += step << 1)
            {
                var twR = 1f;
                var twI = 0f;

                for (var pair = 0; pair < step; pair++)
                {
                    var even = group + pair;
                    var odd = even + step;

                    var oddR = data[2 * odd];
                    var oddI = data[(2 * odd) + 1];

                    var tR = (twR * oddR) - (twI * oddI);
                    var tI = (twR * oddI) + (twI * oddR);

                    data[2 * odd] = data[2 * even] - tR;
                    data[(2 * odd) + 1] = data[(2 * even) + 1] - tI;
                    data[2 * even] += tR;
                    data[(2 * even) + 1] += tI;

                    var newTwR = (twR * wR) - (twI * wI);
                    twI = (twR * wI) + (twI * wR);
                    twR = newTwR;
                }
            }
        }
    }

    /// <summary>
    /// Extracts magnitude spectrum from interleaved FFT result.
    /// Returns N/2 magnitudes (only positive frequencies).
    /// </summary>
    public static void GetMagnitudes(float[] fftData, float[] magnitudes, int n)
    {
        for (var i = 0; i < n / 2; i++)
        {
            var re = fftData[2 * i];
            var im = fftData[(2 * i) + 1];
            magnitudes[i] = MathF.Sqrt((re * re) + (im * im)) / n;
        }
    }
}
