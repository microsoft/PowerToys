// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest
{
    public static class VisualAssert
    {
        /// <summary>
        /// Asserts current visual state of the element is equal with base line image.
        /// To use this VisualAssert, you need to set Window Theme to Light-Mode to avoid Theme color difference in baseline image.
        /// Such limitation could be removed either Auto-generate baseline image for both Light & Dark mode
        /// </summary>
        /// <param name="testContext">TestContext object</param>
        /// <param name="element">Element object</param>
        /// <param name="scenarioSubname">additional scenario name if two or more scenarios in one test</param>
        [RequiresUnreferencedCode("This method uses reflection which may not be compatible with trimming.")]
        public static void AreEqual(TestContext? testContext, Element element, string scenarioSubname = "")
        {
            var pipelinePlatform = Environment.GetEnvironmentVariable("platform");

            // Perform visual validation only in the pipeline
            if (string.IsNullOrEmpty(pipelinePlatform))
            {
                Console.WriteLine("Skip visual validation in the local run.");
                return;
            }

            if (element == null)
            {
                Assert.Fail("Element object is null or invalid");
            }

            var stackTrace = new StackTrace();
            var callerFrame = stackTrace.GetFrame(1);
            var callerMethod = callerFrame?.GetMethod();

            var callerName = callerMethod?.Name;
            var callerClassName = callerMethod?.DeclaringType?.Name;

            if (string.IsNullOrEmpty(callerName) || string.IsNullOrEmpty(callerClassName))
            {
                Assert.Fail("Unable to determine the caller method and class name.");
            }

            if (string.IsNullOrWhiteSpace(scenarioSubname))
            {
                scenarioSubname = string.Join("_", callerClassName, callerName, pipelinePlatform);
            }
            else
            {
                scenarioSubname = string.Join("_", callerClassName, callerName, scenarioSubname.Trim(), pipelinePlatform);
            }

            var baselineImageResourceName = callerMethod!.DeclaringType!.Assembly.GetManifestResourceNames().Where(name => name.Contains(scenarioSubname)).FirstOrDefault();

            var tempTestImagePath = GetTempFilePath(scenarioSubname, "test", ".png");

            element.SaveToPngFile(tempTestImagePath);

            if (string.IsNullOrEmpty(baselineImageResourceName)
                || !Path.GetFileNameWithoutExtension(baselineImageResourceName).EndsWith(scenarioSubname))
            {
                testContext?.AddResultFile(tempTestImagePath);
                Assert.Fail($"Baseline image for scenario {scenarioSubname} can't be found, test image saved in file://{tempTestImagePath.Replace('\\', '/')}");
            }

            var tempBaselineImagePath = GetTempFilePath(scenarioSubname, "baseline", Path.GetExtension(baselineImageResourceName));

            bool isSame = false;

            using (var stream = callerMethod!.DeclaringType!.Assembly.GetManifestResourceStream(baselineImageResourceName))
            {
                if (stream == null)
                {
                    Assert.Fail($"Resource stream '{baselineImageResourceName}' is null.");
                }

                using (var baselineImage = new Bitmap(stream))
                {
                    using (var testImage = new Bitmap(tempTestImagePath))
                    {
                        isSame = VisualAssert.AreEqual(baselineImage, testImage);

                        if (!isSame)
                        {
                            // Copy baseline image to temp folder as well
                            baselineImage.Save(tempBaselineImagePath);
                        }
                    }
                }
            }

            if (!isSame)
            {
                if (testContext != null)
                {
                    testContext.AddResultFile(tempBaselineImagePath);
                    testContext.AddResultFile(tempTestImagePath);
                }

                Assert.Fail($"Fail to validate visual result for scenario {scenarioSubname}, baseline image can be found file://{tempBaselineImagePath.Replace('\\', '/')}, and test image can be found file://{tempTestImagePath.Replace('\\', '/')}");
            }
        }

        /// <summary>
        /// Get temp file path
        /// </summary>
        /// <param name="scenario">scenario name</param>
        /// <param name="imageType">baseline or test image</param>
        /// <param name="extension">image file extension</param>
        /// <returns>full temp file path</returns>
        private static string GetTempFilePath(string scenario, string imageType, string extension)
        {
            var tempFileFullName = $"{scenario}_{imageType}{extension}";

            // Remove invalid filename character if any
            Path.GetInvalidFileNameChars().ToList().ForEach(c => tempFileFullName = tempFileFullName.Replace(c, '-'));

            return Path.Combine(Path.GetTempPath(), tempFileFullName);
        }

        /// <summary>
        /// Test if two images are equal bit-by-bit
        /// </summary>
        /// <param name="baselineImage">baseline image</param>
        /// <param name="testImage">test image</param>
        /// <returns>true if are equal,otherwise false</returns>
        private static bool AreEqual(Bitmap baselineImage, Bitmap testImage)
        {
            if (baselineImage.Width != testImage.Width || baselineImage.Height != testImage.Height)
            {
                return false;
            }

            // WinAppDriver sometimes adds a border to the screenshot (around 2 pix width), and it is not always consistent.
            // So we exclude the border when comparing the images, and usually it is the edge of the windows, won't affect the comparison.
            int excludeBorderWidth = 5, excludeBorderHeight = 5;

            for (int x = excludeBorderWidth; x < baselineImage.Width - excludeBorderWidth; x++)
            {
                for (int y = excludeBorderHeight; y < baselineImage.Height - excludeBorderHeight; y++)
                {
                    if (!VisualHelper.PixIsSame(baselineImage.GetPixel(x, y), testImage.GetPixel(x, y)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
