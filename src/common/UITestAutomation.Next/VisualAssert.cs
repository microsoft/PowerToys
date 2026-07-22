// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
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

        if (string.IsNullOrEmpty(baselineImageResourceName))
        {
            session.Screenshot(testImagePath);
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
        do
        {
            session.Screenshot(testImagePath);
            using var testImage = new Bitmap(testImagePath);
            similarity = CalculateSimilarity(baselineImage, testImage);
            if (similarity >= SimilarityThreshold)
            {
                return;
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
        Assert.Fail(
            $"Visual result for scenario {scenarioSubname} did not reach {SimilarityThreshold}% similarity " +
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

    private static double CalculateSimilarity(Bitmap baselineImage, Bitmap testImage)
    {
        var hashAlgorithm = new AverageHash();
        using var baselineStream = new MemoryStream();
        using var testStream = new MemoryStream();
        baselineImage.Save(baselineStream, System.Drawing.Imaging.ImageFormat.Png);
        testImage.Save(testStream, System.Drawing.Imaging.ImageFormat.Png);
        baselineStream.Position = 0;
        testStream.Position = 0;

        var baselineHash = hashAlgorithm.Hash(baselineStream);
        var testHash = hashAlgorithm.Hash(testStream);
        return CompareHash.Similarity(baselineHash, testHash);
    }
}