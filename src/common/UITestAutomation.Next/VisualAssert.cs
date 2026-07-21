// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

public static class VisualAssert
{
    private const int HashSize = 8;
    private const int SimilarityThreshold = 95;

    /// <summary>
    /// Asserts that the current visual state of a session matches its embedded baseline image.
    /// Visual validation runs only in the pipeline, matching the legacy harness behavior.
    /// </summary>
    [RequiresUnreferencedCode("This method uses reflection which may not be compatible with trimming.")]
    public static void AreEqual(TestContext? testContext, Session session, string scenarioSubname = "")
    {
        if (!EnvironmentConfig.IsInPipeline)
        {
            Console.WriteLine("Skip visual validation in the local run.");
            return;
        }

        var callerMethod = new StackTrace().GetFrame(1)?.GetMethod();
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

        session.Screenshot(testImagePath);

        if (string.IsNullOrEmpty(baselineImageResourceName))
        {
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
        using var testImage = new Bitmap(testImagePath);
        if (AreImagesSimilar(baselineImage, testImage))
        {
            return;
        }

        baselineImage.Save(baselineImagePath);
        testContext?.AddResultFile(baselineImagePath);
        testContext?.AddResultFile(testImagePath);
        Assert.Fail(
            $"Visual result for scenario {scenarioSubname} did not match. " +
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

    private static bool AreImagesSimilar(Bitmap baselineImage, Bitmap testImage)
    {
        var baselineHash = ComputeAverageHash(baselineImage);
        var testHash = ComputeAverageHash(testImage);
        var matchingBits = HashSize * HashSize - System.Numerics.BitOperations.PopCount(baselineHash ^ testHash);
        var similarity = matchingBits * 100 / (HashSize * HashSize);
        return similarity >= SimilarityThreshold;
    }

    private static ulong ComputeAverageHash(Bitmap image)
    {
        using var scaledImage = new Bitmap(image, new Size(HashSize, HashSize));
        var luminance = new byte[HashSize * HashSize];
        var total = 0;

        for (var y = 0; y < HashSize; y++)
        {
            for (var x = 0; x < HashSize; x++)
            {
                var color = scaledImage.GetPixel(x, y);
                var value = (byte)((color.R * 299 + color.G * 587 + color.B * 114) / 1000);
                luminance[y * HashSize + x] = value;
                total += value;
            }
        }

        var average = total / luminance.Length;
        ulong hash = 0;
        for (var index = 0; index < luminance.Length; index++)
        {
            if (luminance[index] >= average)
            {
                hash |= 1UL << index;
            }
        }

        return hash;
    }
}