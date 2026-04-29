// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Wox.Infrastructure.Image;

namespace Wox.Test
{
    [TestClass]
    public class ImageLoaderTest
    {
        [DataTestMethod]

        // Regular Windows paths should be returned unchanged
        [DataRow(@"C:\path\to\file.png", @"C:\path\to\file.png")]
        [DataRow(@"C:\Program Files\PowerToys\Assets\PowerLauncher\app_error.dark.png", @"C:\Program Files\PowerToys\Assets\PowerLauncher\app_error.dark.png")]

        // UNC paths should be returned unchanged
        [DataRow(@"\\server\share\path\file.png", @"\\server\share\path\file.png")]

        // Extended-length local paths (\\?\C:\...) should have the \\?\ prefix stripped
        [DataRow(@"\\?\C:\path\to\file.png", @"C:\path\to\file.png")]
        [DataRow(@"\\?\C:\Program Files\PowerToys\Assets\app_error.dark.png", @"C:\Program Files\PowerToys\Assets\app_error.dark.png")]

        // Extended-length UNC paths (\\?\UNC\server\...) should be converted to \\server\...
        [DataRow(@"\\?\UNC\server\share\path\file.png", @"\\server\share\path\file.png")]
        [DataRow(@"\\?\UNC\TH50\TH50_c\Program Files\PowerToys\Assets\PowerLauncher\app_error.dark.png", @"\\TH50\TH50_c\Program Files\PowerToys\Assets\PowerLauncher\app_error.dark.png")]

        // Case-insensitive matching for the prefix
        [DataRow(@"\\?\unc\server\share\path\file.png", @"\\server\share\path\file.png")]
        public void GetNormalizedPath_ShouldStripExtendedLengthPrefix(string input, string expected)
        {
            // Act
            string result = ImageLoader.GetNormalizedPath(input);

            // Assert
            Assert.AreEqual(expected, result);
        }
    }
}
