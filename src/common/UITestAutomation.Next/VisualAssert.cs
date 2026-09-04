// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
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
        var scenario = ResolveScenario(callerMethod, scenarioSubname);

        AssertAgainstBaseline(
            testContext,
            callerMethod!.DeclaringType!.Assembly,
            scenario,
            path => session.ScreenshotVisibleWindow(path));
    }

    /// <summary>
    /// Asserts that the current visual state of one element matches its embedded baseline image.
    /// This preserves the legacy harness's element-cropped visual assertion behavior.
    /// </summary>
    [RequiresUnreferencedCode("This method uses reflection which may not be compatible with trimming.")]
    public static void AreEqual(
        TestContext? testContext,
        Element element,
        string scenarioSubname = "",
        bool captureScreen = true)
    {
        if (!EnvironmentConfig.IsInPipeline)
        {
            Console.WriteLine("Skip visual validation in the local run.");
            return;
        }

        Assert.IsNotNull(element);
        Assert.IsNotNull(element.Owner, "Element is not bound to a Session.");

        var callerMethod = new StackTrace().GetFrame(1)?.GetMethod();
        var scenario = ResolveScenario(callerMethod, scenarioSubname);
        Action<string> capture = path =>
        {
            var owner = element.Owner!;
            owner.EnsureForeground();
            if (owner.Scope == Session.TargetScope.Window)
            {
                var foregroundFailure = $"HWND {owner.WindowHandle} did not become foreground before element screenshot capture. " +
                                        $"Current foreground: {WindowControl.GetForegroundWindowInfo()}";
                Assert.IsTrue(
                    WindowControl.WaitForForeground(new IntPtr(owner.WindowHandle), timeoutMS: 5_000),
                    foregroundFailure);
            }

            owner.Screenshot(path, element, captureScreen);
        };

        AssertAgainstBaseline(testContext, callerMethod!.DeclaringType!.Assembly, scenario, capture);
    }

    private static string ResolveScenario(MethodBase? callerMethod, string scenarioSubname)
    {
        var callerName = callerMethod?.Name;
        var callerClassName = callerMethod?.DeclaringType?.Name;
        if (string.IsNullOrEmpty(callerName) || string.IsNullOrEmpty(callerClassName))
        {
            Assert.Fail("Unable to determine the caller method and class name.");
        }

        return string.IsNullOrWhiteSpace(scenarioSubname)
            ? string.Join("_", callerClassName, callerName, EnvironmentConfig.Platform)
            : string.Join("_", callerClassName, callerName, scenarioSubname.Trim(), EnvironmentConfig.Platform);
    }

    private static void AssertAgainstBaseline(
        TestContext? testContext,
        System.Reflection.Assembly assembly,
        string scenario,
        Action<string> capture)
    {
        var baselineImageResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => Path.GetFileNameWithoutExtension(name).EndsWith(scenario, StringComparison.Ordinal));
        var testImagePath = GetTempFilePath(scenario, "test", ".png");

        if (string.IsNullOrEmpty(baselineImageResourceName))
        {
            capture(testImagePath);
            testContext?.AddResultFile(testImagePath);
            Assert.Fail($"Baseline image for scenario {scenario} can't be found; test image saved to {testImagePath}.");
        }

        var baselineImagePath = GetTempFilePath(scenario, "baseline", Path.GetExtension(baselineImageResourceName));
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
            capture(testImagePath);
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
            $"Visual result for scenario {scenario} did not reach {SimilarityThreshold}% similarity " +
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
