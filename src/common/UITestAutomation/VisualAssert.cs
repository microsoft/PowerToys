// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

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
            // Perform visual validation only in the pipeline
            if (!EnvironmentConfig.IsInPipeline)
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
                scenarioSubname = string.Join("_", callerClassName, callerName, EnvironmentConfig.Platform);
            }
            else
            {
                scenarioSubname = string.Join("_", callerClassName, callerName, scenarioSubname.Trim(), EnvironmentConfig.Platform);
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
        /// Test if two images are equal using ImageHash comparison
        /// </summary>
        /// <param name="baselineImage">baseline image</param>
        /// <param name="testImage">test image</param>
        /// <returns>true if are equal,otherwise false</returns>
        private static bool AreEqual(Bitmap baselineImage, Bitmap testImage)
        {
            try
            {
                // Define a threshold for similarity percentage
                const int SimilarityThreshold = 95;

                // Use CoenM.ImageHash for perceptual hash comparison
                var hashAlgorithm = new AverageHash();

                // Convert System.Drawing.Bitmap to SixLabors.ImageSharp.Image
                using var baselineImageSharp = ConvertBitmapToImageSharp(baselineImage);
                using var testImageSharp = ConvertBitmapToImageSharp(testImage);

                // Calculate hashes for both images
                var baselineHash = hashAlgorithm.Hash(baselineImageSharp);
                var testHash = hashAlgorithm.Hash(testImageSharp);

                // Compare hashes using CompareHash method
                // Returns similarity percentage (0-100, where 100 is identical)
                var similarity = CompareHash.Similarity(baselineHash, testHash);

                // Consider images equal if similarity is very high
                // Allow for minor rendering differences (threshold can be adjusted)
                return similarity >= SimilarityThreshold; // 95% similarity threshold
            }
            catch
            {
                // Fallback to pixel-by-pixel comparison if hash comparison fails
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

        /// <summary>
        /// Convert System.Drawing.Bitmap to SixLabors.ImageSharp.Image
        /// </summary>
        /// <param name="bitmap">The bitmap to convert</param>
        /// <returns>ImageSharp Image</returns>
        private static Image<Rgba32> ConvertBitmapToImageSharp(Bitmap bitmap)
        {
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);
            memoryStream.Position = 0;
            return SixLabors.ImageSharp.Image.Load<Rgba32>(memoryStream);
        }
    }
}
