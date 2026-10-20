// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

public static class VisualAssert
{
    private const int SimilarityThreshold = 95;
    private const int VisualRetryTimeoutMS = 15_000;
    private const int VisualRetryIntervalMS = 500;

    /// <summary>
    /// Asserts that the current visual state of a session matches its embedded baseline image.
    /// Visual validation runs only in the pipeline, matching the legacy harness behavior.
    /// Images must have equal dimensions and reach 95% full-image structural similarity.
    /// </summary>
    [RequiresUnreferencedCode("This method uses reflection which may not be compatible with trimming.")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AreEqual(TestContext? testContext, Session session, string scenarioSubname = "")
    {
        AreEqualCore(testContext, session, scenarioSubname, SimilarityThreshold, new StackTrace().GetFrame(1)?.GetMethod());
    }

    /// <summary>
    /// Asserts a visual match using an explicit similarity percentage from 1 through 100.
    /// Visual validation runs only in the pipeline, matching the default overload.
    /// </summary>
    [RequiresUnreferencedCode("This method uses reflection which may not be compatible with trimming.")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AreEqual(TestContext? testContext, Session session, string scenarioSubname, int similarityThreshold)
    {
        AreEqualCore(testContext, session, scenarioSubname, similarityThreshold, new StackTrace().GetFrame(1)?.GetMethod());
    }

    internal static bool IsSimilar(double similarity, int similarityThreshold = SimilarityThreshold)
    {
        ValidateSimilarityThreshold(similarityThreshold);
        return similarity >= similarityThreshold;
    }

    private static void ValidateSimilarityThreshold(int similarityThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(similarityThreshold, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(similarityThreshold, 100);
    }

    [RequiresUnreferencedCode("This method uses reflection which may not be compatible with trimming.")]
    private static void AreEqualCore(TestContext? testContext, Session session, string scenarioSubname, int similarityThreshold, MethodBase? callerMethod)
    {
        ValidateSimilarityThreshold(similarityThreshold);
        if (!EnvironmentConfig.IsInPipeline)
        {
            Console.WriteLine("Skip visual validation in the local run.");
            return;
        }

        var callerName = callerMethod?.Name;
        var callerClassName = callerMethod?.DeclaringType?.Name;

        if (string.IsNullOrEmpty(callerName) || string.IsNullOrEmpty(callerClassName))
        {
            Assert.Fail("Unable to determine the caller method and class name.");
        }

        scenarioSubname = string.IsNullOrWhiteSpace(scenarioSubname)
            ? string.Join("_", callerClassName, callerName, EnvironmentConfig.Platform)
            : string.Join("_", callerClassName, callerName, scenarioSubname.Trim(), EnvironmentConfig.Platform);

        var assembly = callerMethod!.DeclaringType!.Assembly;
        var baselineImageResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => Path.GetFileNameWithoutExtension(name).EndsWith(scenarioSubname, StringComparison.Ordinal));
        var testImagePath = GetTempFilePath(scenarioSubname, "test", ".png");

        if (string.IsNullOrEmpty(baselineImageResourceName))
        {
            session.ScreenshotVisibleWindow(testImagePath);
            testContext?.AddResultFile(testImagePath);
            Assert.Fail($"Baseline image for scenario {scenarioSubname} can't be found; test image saved to {testImagePath}.");
        }

        var baselineImagePath = GetTempFilePath(scenarioSubname, "baseline", Path.GetExtension(baselineImageResourceName));
        using var stream = assembly.GetManifestResourceStream(baselineImageResourceName);
        if (stream is null)
        {
            Assert.Fail($"Resource stream '{baselineImageResourceName}' is null.");
        }

        using var baselineImage = new Bitmap(stream!);
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(VisualRetryTimeoutMS);
        var similarity = 0d;
        var actualSize = Size.Empty;
        do
        {
            session.ScreenshotVisibleWindow(testImagePath);
            using var testImage = new Bitmap(testImagePath);
            actualSize = testImage.Size;
            if (actualSize == baselineImage.Size)
            {
                similarity = CalculateSimilarity(baselineImage, testImage);
                if (IsSimilar(similarity, similarityThreshold))
                {
                    return;
                }
            }

            if (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(VisualRetryIntervalMS);
            }
        }
        while (DateTime.UtcNow < deadline);

        baselineImage.Save(baselineImagePath);
        testContext?.AddResultFile(baselineImagePath);
        testContext?.AddResultFile(testImagePath);
        if (actualSize != baselineImage.Size)
        {
            Assert.Fail(
                $"Visual result for scenario {scenarioSubname} has size {actualSize.Width}x{actualSize.Height}; " +
                $"expected {baselineImage.Width}x{baselineImage.Height}. " +
                $"Baseline: {baselineImagePath}; test image: {testImagePath}.");
        }

        Assert.Fail(
            $"Visual result for scenario {scenarioSubname} did not reach {similarityThreshold}% structural similarity " +
            $"within {VisualRetryTimeoutMS / 1_000}s (last similarity: {similarity:F2}%). " +
            $"Baseline: {baselineImagePath}; test image: {testImagePath}.");
    }

    private static string GetTempFilePath(string scenario, string imageType, string extension)
    {
        var fileName = $"{scenario}_{imageType}{extension}";
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidCharacter, '-');
        }

        return Path.Combine(Path.GetTempPath(), fileName);
    }

    /// <summary>
    /// Mean 8x8-block SSIM, weighted by pixel count so partial edge blocks are included.
    /// RGB and alpha are compared independently; the lower score is returned.
    /// </summary>
    internal static double CalculateSimilarity(Bitmap baselineImage, Bitmap testImage)
    {
        ArgumentNullException.ThrowIfNull(baselineImage);
        ArgumentNullException.ThrowIfNull(testImage);
        if (baselineImage.Size != testImage.Size)
        {
            throw new ArgumentException("Visual comparison requires equal image dimensions.", nameof(testImage));
        }

        const int BlockSize = 8;
        const double LuminanceStabilizer = 6.5025; // (0.01 * 255)^2
        const double ContrastStabilizer = 58.5225; // (0.03 * 255)^2
        var baseline = ReadPixels(baselineImage);
        var actual = ReadPixels(testImage);
        var width = baselineImage.Width;
        var height = baselineImage.Height;
        double colorScore = 0;
        double alphaScore = 0;

        // Unlike a single 64-bit average hash, local statistics do not let a changed
        // window-frame pixel move the comparison threshold for unrelated image content.
        for (var top = 0; top < height; top += BlockSize)
        {
            for (var left = 0; left < width; left += BlockSize)
            {
                var right = Math.Min(left + BlockSize, width);
                var bottom = Math.Min(top + BlockSize, height);
                var count = (right - left) * (bottom - top);
                for (var channel = 0; channel < 4; channel++)
                {
                    double sumBaseline = 0;
                    double sumActual = 0;
                    double squaredBaseline = 0;
                    double squaredActual = 0;
                    double product = 0;
                    for (var y = top; y < bottom; y++)
                    {
                        for (var x = left; x < right; x++)
                        {
                            var index = (y * width) + x;
                            var first = (baseline[index] >> (channel * 8)) & 255;
                            var second = (actual[index] >> (channel * 8)) & 255;
                            sumBaseline += first;
                            sumActual += second;
                            squaredBaseline += first * first;
                            squaredActual += second * second;
                            product += first * second;
                        }
                    }

                    var meanBaseline = sumBaseline / count;
                    var meanActual = sumActual / count;
                    var varianceBaseline = Math.Max(0, (squaredBaseline / count) - (meanBaseline * meanBaseline));
                    var varianceActual = Math.Max(0, (squaredActual / count) - (meanActual * meanActual));
                    var covariance = (product / count) - (meanBaseline * meanActual);
                    var similarity =
                        (((2 * meanBaseline * meanActual) + LuminanceStabilizer) * ((2 * covariance) + ContrastStabilizer)) /
                        (((meanBaseline * meanBaseline) + (meanActual * meanActual) + LuminanceStabilizer) *
                        (varianceBaseline + varianceActual + ContrastStabilizer));
                    if (channel == 3)
                    {
                        alphaScore += similarity * count;
                    }
                    else
                    {
                        colorScore += similarity * count;
                    }
                }
            }
        }

        return Math.Clamp(Math.Min(colorScore / (baseline.Length * 3d), alphaScore / baseline.Length) * 100, 0, 100);
    }

    private static int[] ReadPixels(Bitmap image)
    {
        var pixels = new int[checked(image.Width * image.Height)];
        var data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (var row = 0; row < image.Height; row++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, row * data.Stride), pixels, row * image.Width, image.Width);
            }
        }
        finally
        {
            image.UnlockBits(data);
        }

        return pixels;
    }
}
