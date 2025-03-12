// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest
{
    public static class VisualAssert
    {
        /// <summary>
        /// Asserts current visual state of the element is equal with base line image.
        /// To use this VisualAssert, you need to set Window Theme to Light-Mode to avoid Theme color difference in baseline image.
        /// Such limiation could be removed either Auto-generate baseline image for both Light & Dark mode
        /// </summary>
        /// <param name="element">Element object</param>
        /// <param name="scenarioSubname">additional scenario name if two or more scenarios in one test</param>
        [RequiresUnreferencedCode("This method uses reflection which may not be compatible with trimming.")]
        public static void AreEqual(Element element, string scenarioSubname = "")
        {
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
                scenarioSubname = string.Join("_", callerClassName, callerName);
            }
            else
            {
                scenarioSubname = string.Join("_", callerClassName, callerName, scenarioSubname.Trim());
            }

            var callerAssembly = callerMethod!.DeclaringType!.Assembly;
            var baselineImageResourceName = callerAssembly.GetManifestResourceNames().Where(name => name.Contains(scenarioSubname)).FirstOrDefault();

            var tempTestImagePath = GetTempFilePath(scenarioSubname, "test", ".png");
            element.SaveToPngFile(tempTestImagePath);

            if (string.IsNullOrEmpty(baselineImageResourceName)
                || !Path.GetFileNameWithoutExtension(baselineImageResourceName).EndsWith(scenarioSubname))
            {
                Assert.Fail($"Baseline image for scenario {scenarioSubname} can not be found, test image saved in file://{tempTestImagePath.Replace('\\', '/')}");
            }

            var tempBaselineImagePath = GetTempFilePath(scenarioSubname, "baseline", Path.GetExtension(baselineImageResourceName));

            bool isSame = false;

            using (var baselineImage = new Bitmap(callerAssembly.GetManifestResourceStream(baselineImageResourceName)))
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

            if (!isSame)
            {
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
                    if (!VisualAssert.PixIsSame(baselineImage.GetPixel(x, y), testImage.GetPixel(x, y)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Compare two pixels with a fuzz factor
        /// </summary>
        /// <param name="c1">base color</param>
        /// <param name="c2">test color</param>
        /// <param name="fuzz">fuzz factor, default is 10</param>
        /// <returns>true if same, otherwise is false</returns>
        private static bool PixIsSame(Color c1, Color c2, int fuzz = 10)
        {
            return Math.Abs(c1.A - c2.A) <= fuzz && Math.Abs(c1.R - c2.R) <= fuzz && Math.Abs(c1.G - c2.G) <= fuzz && Math.Abs(c1.B - c2.B) <= fuzz;
        }
    }
}
