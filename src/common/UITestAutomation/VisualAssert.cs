// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest
{
    public static class VisualAssert
    {
        /// <summary>
        /// Asserts current visual state of the element is equal to base line image.
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
        /// Test if two images are equal using structural similarity (SSIM).
        /// Mirrors Microsoft.PowerToys.UITest.Next.VisualAssert until legacy tests are migrated.
        /// </summary>
        /// <param name="baselineImage">baseline image</param>
        /// <param name="testImage">test image</param>
        /// <returns>true if are equal,otherwise false</returns>
        private static bool AreEqual(Bitmap baselineImage, Bitmap testImage)
        {
            const int SimilarityThreshold = 95;

            if (baselineImage.Size != testImage.Size)
            {
                return false;
            }

            return CalculateSimilarity(baselineImage, testImage) >= SimilarityThreshold;
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
}
