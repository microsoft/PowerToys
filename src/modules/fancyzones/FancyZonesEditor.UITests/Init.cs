// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Microsoft.VisualStudio.TestTools.UnitTesting.DoNotParallelize]

namespace Microsoft.FancyZonesEditor.UITests
{
    [TestClass]
    public class Init
    {
        [AssemblyCleanup]
        public static void CleanupAll()
        {
            FancyZonesEditorHelper.Files.Restore();
        }
    }
}
